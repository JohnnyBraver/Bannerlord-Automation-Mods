using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmithingOptimizer
{
    public static class CraftingPatches
    {
        public static CraftingVM? ActiveCraftingVM { get; private set; }
        public static WeaponDesignVM? ActiveWeaponDesignVM { get; private set; }
        private static bool _isOptimizing;
        private static string? _lastOptimizerFingerprint;
        private static string? _lastTrainingRecommendationFingerprint;

        public static void OnWeaponDesignVMConstructed(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
            TriggerAutoOptimization();
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnCraftingLogicRefreshed")]
        [HarmonyPostfix]
        public static void OnCraftingLogicRefreshedPostfix(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
            TriggerAutoOptimization();
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnFinalize")]
        [HarmonyPrefix]
        public static void OnFinalizePrefix(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
            TriggerAutoOptimization();
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalizePostfix()
        {
            ActiveWeaponDesignVM = null;
        }

        [HarmonyPatch(typeof(CraftingVM), "ExecuteFinalizeCrafting")]
        [HarmonyPostfix]
        public static void ExecuteFinalizeCraftingPostfix(CraftingVM __instance)
        {
            ActiveCraftingVM = __instance;
            TriggerAutoOptimization(onlyIfCurrentDesignMatchesOptimizer: true);
        }

        [HarmonyPatch(typeof(CraftingVM), "OnCraftingLogicRefreshed")]
        [HarmonyPostfix]
        public static void OnCraftingVmLogicRefreshedPostfix(CraftingVM __instance)
        {
            ActiveCraftingVM = __instance;
            TriggerAutoOptimization();
        }

        [HarmonyPatch(typeof(CraftingVM), "UpdateCraftingHero")]
        [HarmonyPostfix]
        public static void UpdateCraftingHeroPostfix(CraftingVM __instance)
        {
            ActiveCraftingVM = __instance;
            TriggerAutoOptimization();
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnNewPieceUnlocked")]
        [HarmonyPostfix]
        public static void OnNewPieceUnlockedPostfix(WeaponDesignVM __instance, CraftingPiece piece)
        {
            ActiveWeaponDesignVM = __instance;
            var settings = Settings.Instance;
            if (settings != null && settings.AutoSwitchEnabled)
            {
                TriggerOptimization(silentOnNoImprovement: true, onlyIfCurrentDesignMatchesOptimizer: false);
            }
        }

        public static void ManualTrigger()
        {
            TriggerOptimization(silentOnNoImprovement: false, onlyIfCurrentDesignMatchesOptimizer: false);
        }

        private static void TriggerAutoOptimization(bool onlyIfCurrentDesignMatchesOptimizer = false)
        {
            var settings = Settings.Instance;
            if (settings != null && settings.AutoSwitchEnabled)
            {
                TriggerOptimization(silentOnNoImprovement: true, onlyIfCurrentDesignMatchesOptimizer);
            }
        }

        private static void TriggerOptimization(bool silentOnNoImprovement, bool onlyIfCurrentDesignMatchesOptimizer)
        {
            var settings = Settings.Instance;
            if (settings == null)
            {
                return;
            }

            if (_isOptimizing)
            {
                return;
            }

            if (ActiveWeaponDesignVM == null)
            {
                if (!silentOnNoImprovement)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Open the Forge screen first."));
                }
                return;
            }

            try
            {
                _isOptimizing = true;

                // 1. Get active Crafting logic via reflection
                var craftingLogic = GetActiveCraftingLogic();
                if (craftingLogic == null) return;
                var crafter = ResolveActiveCrafter(craftingLogic);

                if (onlyIfCurrentDesignMatchesOptimizer && !CurrentDesignMatchesLastOptimizerDesign(craftingLogic))
                {
                    return;
                }

                // 2. Run Optimizer
                var results = OptimizerEngine.Optimize(
                    craftingLogic,
                    crafter,
                    craftingLogic.CurrentCraftingTemplate,
                    settings.LimitToInventory,
                    settings.Goal
                );

                if (results == null || results.Count == 0)
                {
                    if (!silentOnNoImprovement)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: No valid designs fit your budget."));
                    }
                    MaybeRecommendTrainingAlternative(null, crafter, silentOnNoImprovement, settings.Goal);
                    return;
                }

                var best = results[0];

                // 3. Compare with current design to see if we improved
                if (IsBetterThanCurrent(craftingLogic, crafter, best, settings.Goal))
                {
                    ApplyDesign(ActiveWeaponDesignVM, craftingLogic, best);
                    _lastOptimizerFingerprint = GetCurrentDesignFingerprint(craftingLogic);

                    string goalText = FormatGoalText(best, settings.Goal);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Smithing Optimizer: Applied new optimal design! ({goalText})",
                        Color.FromUint(0x40FF40FF) // Green
                    ));
                }
                else
                {
                    _lastOptimizerFingerprint = GetCurrentDesignFingerprint(craftingLogic);

                    if (!silentOnNoImprovement)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Current design is already optimal."));
                    }
                }

                MaybeRecommendTrainingAlternative(best, crafter, silentOnNoImprovement, settings.Goal);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Smithing Optimizer Error: {ex.Message}"));
            }
            finally
            {
                _isOptimizing = false;
            }
        }

        private static Crafting? GetActiveCraftingLogic()
        {
            if (ActiveWeaponDesignVM != null)
            {
                var weaponDesignCraftingField = typeof(WeaponDesignVM).GetField("_crafting", BindingFlags.Instance | BindingFlags.NonPublic);
                var weaponDesignCrafting = weaponDesignCraftingField?.GetValue(ActiveWeaponDesignVM) as Crafting;
                if (weaponDesignCrafting != null)
                {
                    return weaponDesignCrafting;
                }
            }

            if (ActiveCraftingVM == null)
            {
                return null;
            }

            var craftingField = typeof(CraftingVM).GetField("_crafting", BindingFlags.Instance | BindingFlags.NonPublic);
            return craftingField?.GetValue(ActiveCraftingVM) as Crafting;
        }

        private static Hero ResolveActiveCrafter(Crafting craftingLogic)
        {
            try
            {
                var vmHero = ActiveCraftingVM?.CurrentCraftingHero?.Hero;
                if (vmHero != null)
                {
                    return vmHero;
                }
            }
            catch
            {
                // Fall through to the campaign behavior.
            }

            try
            {
                var behavior = Campaign.Current?.GetCampaignBehavior<CraftingCampaignBehavior>();
                var activeHero = behavior?.GetActiveCraftingHero();
                if (activeHero != null)
                {
                    return activeHero;
                }
            }
            catch
            {
                // Fall through to the main hero fallback.
            }

            return Hero.MainHero;
        }

        private static bool CurrentDesignMatchesLastOptimizerDesign(Crafting craftingLogic)
        {
            string? currentFingerprint = GetCurrentDesignFingerprint(craftingLogic);
            return !string.IsNullOrEmpty(currentFingerprint) &&
                string.Equals(currentFingerprint, _lastOptimizerFingerprint, StringComparison.Ordinal);
        }

        private static string? GetCurrentDesignFingerprint(Crafting craftingLogic)
        {
            var design = craftingLogic.CurrentWeaponDesign;
            if (design == null)
            {
                return null;
            }

            var usedPieces = design.UsedPieces;
            if (usedPieces == null)
            {
                return null;
            }

            string templateId = craftingLogic.CurrentCraftingTemplate?.StringId ?? string.Empty;
            var parts = new string[usedPieces.Length];
            for (int i = 0; i < usedPieces.Length; i++)
            {
                var element = usedPieces[i];
                string pieceId = element.CraftingPiece?.StringId ?? string.Empty;
                parts[i] = $"{pieceId}:{element.ScalePercentage}";
            }

            return $"{templateId}|{string.Join("|", parts)}";
        }

        private static bool IsBetterThanCurrent(Crafting craftingLogic, Hero crafter, CraftingDesignCandidate best, OptimizationGoal goal)
        {
            var currentDesign = craftingLogic.CurrentWeaponDesign;
            if (currentDesign == null) return true;

            // Compute current stats
            ItemObject currentItem = craftingLogic.GetCurrentCraftedItemObject(false, "optimizer_check");
            if (currentItem == null) return true;

            if (goal == OptimizationGoal.Damage)
            {
                int currentDmg = 0;
                if (currentItem.WeaponComponent != null && currentItem.WeaponComponent.PrimaryWeapon != null)
                {
                    var w = currentItem.WeaponComponent.PrimaryWeapon;
                    currentDmg = Math.Max(w.SwingDamage, w.ThrustDamage);
                }
                return best.MaxDamage > currentDmg;
            }
            else if (goal == OptimizationGoal.SellValue)
            {
                var currentScore = SmithingScoreEstimator.ScoreCraftingItem(currentItem, currentDesign, crafter);
                int currentValue = currentScore?.Value ?? currentItem.Value;
                return best.Value > currentValue;
            }
            else
            {
                var currentXpScore = SmithingScoreEstimator.ScoreCraftingItem(currentItem, currentDesign, crafter);
                return currentXpScore == null || best.XpPerStamina > currentXpScore.XpPerStamina + 0.001f;
            }
        }

        private static string FormatGoalText(CraftingDesignCandidate best, OptimizationGoal goal)
        {
            if (goal == OptimizationGoal.Damage)
            {
                return $"Damage: {best.MaxDamage}";
            }

            if (goal == OptimizationGoal.SellValue)
            {
                string modifier = string.IsNullOrWhiteSpace(best.ItemModifierName) ? string.Empty : $" ({best.ItemModifierName})";
                return $"Expected Value: {best.Value}d{modifier}";
            }

            return $"Raw XP/Stamina: {best.XpPerStamina:F2} ({best.RawXp} XP / {best.StaminaCost} stamina)";
        }

        private static void MaybeRecommendTrainingAlternative(CraftingDesignCandidate? bestCraft, Hero crafter, bool silentOnNoImprovement, OptimizationGoal goal)
        {
            if (goal != OptimizationGoal.XpEfficiency)
            {
                return;
            }

            var alternative = SmithingScoreEstimator.FindBestAlternative(crafter);
            if (alternative == null)
            {
                return;
            }

            if (bestCraft != null && alternative.XpPerStamina <= bestCraft.XpPerStamina + 0.001f)
            {
                return;
            }

            string message = bestCraft == null
                ? $"Smithing Optimizer: Best raw XP/stamina right now: {alternative.Description} ({alternative.RawXp} XP / {alternative.StaminaCost} stamina = {alternative.XpPerStamina:F2})."
                : SmithingScoreEstimator.FormatRecommendation(alternative, bestCraft);
            string fingerprint = $"{crafter?.Name?.ToString()}|{alternative.Kind}|{alternative.Description}|{alternative.RawXp}|{alternative.StaminaCost}|{bestCraft?.RawXp}|{bestCraft?.StaminaCost}";

            if (!silentOnNoImprovement || !string.Equals(fingerprint, _lastTrainingRecommendationFingerprint, StringComparison.Ordinal))
            {
                OptimizerEngine.WriteLog(message);
                InformationManager.DisplayMessage(new InformationMessage(message, Color.FromUint(0xF0C060FF)));
                _lastTrainingRecommendationFingerprint = fingerprint;
            }
        }

        private static void ApplyDesign(WeaponDesignVM vm, Crafting craftingLogic, CraftingDesignCandidate candidate)
        {
            var template = craftingLogic.CurrentCraftingTemplate;

            // SetDesignManually signature:
            // SetDesignManually(CraftingTemplate craftingTemplate, ValueTuple<CraftingPiece, int>[] pieces, bool forceChangeTemplate)
            var method = typeof(WeaponDesignVM).GetMethod("SetDesignManually", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) return;

            var emptyPiece = WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade).CraftingPiece;

            // We must use ValueTuple<CraftingPiece, int>
            var pieces = new ValueTuple<CraftingPiece, int>[4];
            pieces[0] = new ValueTuple<CraftingPiece, int>(candidate.Blade ?? emptyPiece, candidate.BladeScale);
            pieces[1] = new ValueTuple<CraftingPiece, int>(candidate.Guard ?? emptyPiece, candidate.GuardScale);
            pieces[2] = new ValueTuple<CraftingPiece, int>(candidate.Grip ?? emptyPiece, candidate.GripScale);
            pieces[3] = new ValueTuple<CraftingPiece, int>(candidate.Pommel ?? emptyPiece, candidate.PommelScale);

            method.Invoke(vm, new object[] { template, pieces, false });
        }
    }
}
