using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmithingOptimizer
{
    public static class CraftingPatches
    {
        private enum OptimizationTrigger { Manual, AutoUnlock, AutoHeroChanged, AutoWeaponCategoryChanged, AutoOrderModeEntered, AutoOrderSelected }

        private sealed class OptimizationSettingsSnapshot
        {
            public bool LimitToInventory { get; }
            public OptimizationGoal Goal { get; }
            public OptimizationEfficiency Efficiency { get; }
            public MinimumCraftQuality DamageMinimumQuality { get; }
            public int DamageMinimumQualityChance { get; }
            public int OrderMinimumCompletionChance { get; }

            public OptimizationSettingsSnapshot(Settings settings)
            {
                LimitToInventory = settings.LimitToInventory;
                Goal = settings.Goal;
                Efficiency = settings.Efficiency;
                DamageMinimumQuality = settings.DamageMinimumQuality;
                DamageMinimumQualityChance = settings.DamageMinimumQualityChance;
                OrderMinimumCompletionChance = settings.OrderMinimumCompletionChance;
            }
        }

        private sealed class OptimizationContext
        {
            public WeaponDesignVM ViewModel { get; }
            public Crafting Crafting { get; }
            public CraftingTemplate Template { get; }
            public Hero Crafter { get; }
            public OptimizationSettingsSnapshot Settings { get; }
            public OptimizationTrigger Trigger { get; }
            public object? Order { get; }
            public bool HasAvailableOrderHero { get; }
            public bool IsOrderMode => Order != null;
            public bool ShowMessages => Trigger == OptimizationTrigger.Manual || Trigger == OptimizationTrigger.AutoOrderSelected;
            public bool ShowCheckMessage => Trigger == OptimizationTrigger.AutoHeroChanged ||
                                            Trigger == OptimizationTrigger.AutoWeaponCategoryChanged ||
                                            Trigger == OptimizationTrigger.AutoOrderModeEntered;
            public bool ShowAlternativeMessages => Trigger != OptimizationTrigger.AutoOrderSelected;

            public OptimizationContext(WeaponDesignVM viewModel, Crafting crafting, CraftingTemplate template, Hero crafter, OptimizationSettingsSnapshot settings, OptimizationTrigger trigger, object? order, bool hasAvailableOrderHero)
            {
                ViewModel = viewModel;
                Crafting = crafting;
                Template = template;
                Crafter = crafter;
                Settings = settings;
                Trigger = trigger;
                Order = order;
                HasAvailableOrderHero = hasAvailableOrderHero;
            }
        }

        private static readonly FieldInfo? CraftingField = typeof(WeaponDesignVM).GetField("_crafting", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo? SetDesignManuallyMethod = typeof(WeaponDesignVM).GetMethod("SetDesignManually", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? PrimaryUsagesField = typeof(WeaponDesignVM).GetField("_primaryUsages", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int _isOptimizing;
        private static string? _lastForgeState;
        private static readonly Dictionary<string, SellValueCacheEntry> SellValueCache = new Dictionary<string, SellValueCacheEntry>();
        public static WeaponDesignVM? ActiveWeaponDesignVM { get; private set; }

        private sealed class SellValueCacheEntry
        {
            public string PartFingerprint { get; }
            public string ContextFingerprint { get; }
            public CraftingDesignCandidate Best { get; }

            public SellValueCacheEntry(string partFingerprint, string contextFingerprint, CraftingDesignCandidate best)
            {
                PartFingerprint = partFingerprint;
                ContextFingerprint = contextFingerprint;
                Best = best;
            }
        }

        public static void OnWeaponDesignVMConstructed(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnCraftingLogicRefreshed")]
        [HarmonyPostfix]
        public static void OnCraftingLogicRefreshedPostfix(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
            ObserveForgeState(__instance);
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalizePostfix()
        {
            ActiveWeaponDesignVM = null;
            _lastForgeState = null;
            SellValueCache.Clear();
        }

        private static void ObserveForgeState(WeaponDesignVM viewModel)
        {
            var crafting = CraftingField?.GetValue(viewModel) as Crafting;
            var behavior = Campaign.Current?.GetCampaignBehavior<CraftingCampaignBehavior>();
            var crafter = behavior?.GetActiveCraftingHero();
            if (crafting?.CurrentCraftingTemplate == null || crafter == null) return;

            string template = crafting.CurrentCraftingTemplate.TemplateName?.ToString() ?? "unknown";
            string order = viewModel.IsInOrderMode
                ? viewModel.ActiveCraftingOrder?.CraftingOrder?.GetHashCode().ToString() ?? "orders-without-selection"
                : "free-build";
            string state = $"{crafter.StringId}|{template}|{order}";
            string? previous = _lastForgeState;
            _lastForgeState = state;
            if (previous == null || previous == state) return;

            var settings = Settings.Instance;
            if (settings == null) return;
            string[] before = previous.Split('|');
            if (before.Length == 3 && before[0] != crafter.StringId)
            {
                OptimizerEngine.WriteLog("Forge state changed: selected hero.");
                if (settings.AutoSwitchEnabled) RequestOptimization(OptimizationTrigger.AutoHeroChanged);
            }
            else if (before.Length == 3 && before[2] != order)
            {
                OptimizerEngine.WriteLog("Forge state changed: crafting order.");
                if (settings.AutoOptimizeCraftingOrders)
                    RequestOptimization(viewModel.ActiveCraftingOrder == null
                        ? OptimizationTrigger.AutoOrderModeEntered
                        : OptimizationTrigger.AutoOrderSelected);
            }
            else if (before.Length == 3 && before[1] != template)
            {
                OptimizerEngine.WriteLog("Forge state changed: weapon category.");
                if (settings.AutoSwitchEnabled) RequestOptimization(OptimizationTrigger.AutoWeaponCategoryChanged);
            }
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnNewPieceUnlocked")]
        [HarmonyPostfix]
        public static void OnNewPieceUnlockedPostfix(WeaponDesignVM __instance, CraftingPiece piece)
        {
            ActiveWeaponDesignVM = __instance;
            var settings = Settings.Instance;
            if (settings != null && settings.AutoSwitchEnabled)
            {
                RequestOptimization(OptimizationTrigger.AutoUnlock);
            }
        }

        public static void ManualTrigger()
        {
            OptimizerEngine.WriteLog("Manual Optimize button pressed.");
            RequestOptimization(OptimizationTrigger.Manual);
        }

        [HarmonyPatch(typeof(CraftingVM), "UpdateCraftingHero")]
        [HarmonyPostfix]
        public static void UpdateCraftingHeroPostfix()
        {
            var settings = Settings.Instance;
            OptimizerEngine.WriteLog($"Forge hero changed; automatic optimization is {(settings?.AutoSwitchEnabled == true ? "enabled" : "disabled")}.");
            if (settings != null && settings.AutoSwitchEnabled)
                RequestOptimization(OptimizationTrigger.AutoHeroChanged);
        }

        [HarmonyPatch(typeof(CraftingVM), "ExecuteSelectWeaponClass")]
        [HarmonyPostfix]
        public static void ExecuteSelectWeaponClassPostfix()
        {
            var settings = Settings.Instance;
            OptimizerEngine.WriteLog($"Weapon category changed; automatic optimization is {(settings?.AutoSwitchEnabled == true ? "enabled" : "disabled")}.");
            if (settings != null && settings.AutoSwitchEnabled)
                RequestOptimization(OptimizationTrigger.AutoWeaponCategoryChanged);
        }

        [HarmonyPatch(typeof(CraftingVM), "ExecuteOpenOrdersTab")]
        [HarmonyPostfix]
        public static void ExecuteOpenOrdersTabPostfix()
        {
            var settings = Settings.Instance;
            OptimizerEngine.WriteLog($"Crafting orders opened; automatic order optimization is {(settings?.AutoOptimizeCraftingOrders == true ? "enabled" : "disabled")}.");
            if (settings != null && settings.AutoOptimizeCraftingOrders)
                RequestOptimization(OptimizationTrigger.AutoOrderModeEntered);
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnCraftingOrderSelected")]
        [HarmonyPostfix]
        public static void OnCraftingOrderSelectedPostfix(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
            var settings = Settings.Instance;
            if (settings != null && settings.AutoOptimizeCraftingOrders)
            {
                RequestOptimization(OptimizationTrigger.AutoOrderSelected);
            }
        }

        private static void RequestOptimization(OptimizationTrigger trigger)
        {
            bool showMessages = trigger == OptimizationTrigger.Manual;
            var settings = Settings.Instance;
            if (settings == null) return;

            if (ActiveWeaponDesignVM == null)
            {
                if (showMessages)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Open the Forge screen first."));
                }
                return;
            }

            if (trigger == OptimizationTrigger.AutoOrderModeEntered &&
                (!ActiveWeaponDesignVM.IsInOrderMode || ActiveWeaponDesignVM.ActiveCraftingOrder == null))
            {
                OptimizerEngine.WriteLog("Entered crafting orders without a selected order.");
                InformationManager.DisplayMessage(new InformationMessage(
                    "Smithing Optimizer: Select an order to check its design."));
                return;
            }

            if (Interlocked.Exchange(ref _isOptimizing, 1) != 0) return;
            try
            {
                var context = CaptureContext(ActiveWeaponDesignVM, settings, trigger);
                if (context == null)
                {
                    OptimizerEngine.WriteLog($"{trigger} optimization could not capture the forge context.");
                    if (showMessages)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Smithing Optimizer: Could not read the current forge design or selected smith."));
                    }
                    return;
                }
                if (context.ShowCheckMessage)
                {
                    string reason = context.Trigger switch
                    {
                        OptimizationTrigger.AutoHeroChanged => "the forge hero changed",
                        OptimizationTrigger.AutoWeaponCategoryChanged => "the weapon category changed",
                        _ => "crafting orders opened"
                    };
                    OptimizerEngine.WriteLog($"Checking design because {reason}.");
                    InformationManager.DisplayMessage(new InformationMessage($"Smithing Optimizer: Checking the design because {reason}."));
                }
                RunOptimization(context);
            }
            catch (Exception ex)
            {
                OptimizerEngine.WriteLog($"{trigger} optimization failed: {ex}");
                InformationManager.DisplayMessage(new InformationMessage($"Smithing Optimizer Error: {ex.Message}"));
            }
            finally { Volatile.Write(ref _isOptimizing, 0); }
        }

        private static OptimizationContext? CaptureContext(WeaponDesignVM viewModel, Settings settings, OptimizationTrigger trigger)
        {
            var crafting = CraftingField?.GetValue(viewModel) as Crafting;
            var behavior = Campaign.Current?.GetCampaignBehavior<CraftingCampaignBehavior>();
            var crafter = behavior?.GetActiveCraftingHero();
            if (crafting == null || crafter == null || crafting.CurrentCraftingTemplate == null) return null;
            object? order = null;
            bool hasAvailableOrderHero = true;
            CraftingTemplate template = crafting.CurrentCraftingTemplate;
            if (viewModel.IsInOrderMode && viewModel.ActiveCraftingOrder != null)
            {
                order = viewModel.ActiveCraftingOrder.CraftingOrder;
                hasAvailableOrderHero = viewModel.ActiveCraftingOrder.HasAvailableHeroes;
                object? orderDesignTemplate = order?.GetType().GetProperty("WeaponDesignTemplate")?.GetValue(order);
                if (orderDesignTemplate?.GetType().GetProperty("Template")?.GetValue(orderDesignTemplate) is CraftingTemplate orderTemplate)
                    template = orderTemplate;
            }
            return new OptimizationContext(viewModel, crafting, template, crafter, new OptimizationSettingsSnapshot(settings), trigger, order, hasAvailableOrderHero);
        }

        private static void RunOptimization(OptimizationContext context)
        {
            var result = context.IsOrderMode
                ? OptimizerEngine.OptimizeOrder(
                    context.Crafting,
                    context.Crafter,
                    context.Template,
                    context.Order!,
                    context.Settings.LimitToInventory,
                    context.Settings.OrderMinimumCompletionChance)
                : OptimizerEngine.Optimize(
                context.Crafting,
                context.Crafter,
                context.Template,
                context.Settings.LimitToInventory,
                context.Settings.Goal,
                context.Settings.Efficiency,
                context.Settings.DamageMinimumQuality,
                context.Settings.DamageMinimumQualityChance);
            string outcome = result.Best == null
                ? "no eligible design"
                : $"best score {result.Best.Score:F2}";
            OptimizerEngine.WriteLog(
                $"{context.Trigger} optimization completed for {context.Template.TemplateName}: {outcome}; " +
                $"quality evaluation {(result.ExpectedQualityScoringDisabled ? "unavailable" : "available")}.");
            if (context.IsOrderMode)
            {
                PresentOrderResult(context, result);
                return;
            }
            if (context.Settings.Goal == OptimizationGoal.SellValue || context.Settings.Goal == OptimizationGoal.SmithingXp)
                PresentBetterTemplateAlternative(context, result.Best);
            if (result.ExpectedQualityScoringDisabled && context.ShowMessages)
            {
                InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Deterministic quality evaluation is unavailable in this game build; Sell Value is using base item value."));
            }
            if (result.Best == null)
            {
                if (context.ShowMessages) InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: No material-affordable complete design was found."));
                MaybeRecommendTrainingAlternative(context, null);
                return;
            }

            if (result.Current != null && result.Best.Score <= result.Current.Score)
            {
                if (context.ShowMessages) InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Current design is already optimal."));
                MaybeRecommendTrainingAlternative(context, result.Current);
                return;
            }

            ApplyDesign(context.ViewModel, context.Crafting, result.Best.Template ?? context.Template, result.Best);
            InformationManager.DisplayMessage(new InformationMessage(
                $"Smithing Optimizer: Updated the design. ({FormatGoal(result.Best, context.Settings.Goal, context.Settings.Efficiency, context.Settings.DamageMinimumQuality)})",
                Color.FromUint(0x40FF40FF)));
            MaybeRecommendTrainingAlternative(context, result.Best);
        }

        private static void PresentBetterTemplateAlternative(OptimizationContext context, CraftingDesignCandidate? currentTemplateBest)
        {
            var behavior = Campaign.Current?.GetCampaignBehavior<CraftingCampaignBehavior>();
            if (behavior == null || currentTemplateBest == null || !context.ShowAlternativeMessages) return;

            var templates = GetPrimaryUsageTemplates(context.ViewModel, context.Template);
            string contextFingerprint = $"{context.Settings.Goal}|{GetSmithingCapabilityFingerprint(context.Crafter)}|{context.Settings.LimitToInventory}|{context.Settings.Efficiency}|{GetMaterialFingerprint(context.Settings.LimitToInventory)}";
            CraftingDesignCandidate? bestAlternative = null;
            foreach (var template in templates)
            {
                if (ReferenceEquals(template, context.Template)) continue;
                string templateKey = template.TemplateName?.ToString() ?? template.GetHashCode().ToString();
                string partFingerprint = GetUnlockedPartFingerprint(template, behavior);
                CraftingDesignCandidate? best;
                bool hasEligibleCacheEntry = SellValueCache.TryGetValue(templateKey, out var cached) &&
                    cached.PartFingerprint == partFingerprint &&
                    cached.ContextFingerprint == contextFingerprint &&
                    OptimizerEngine.IsCandidateEligible(cached.Best, template, behavior);
                if (hasEligibleCacheEntry)
                {
                    best = cached.Best;
                }
                else
                {
                    SellValueCache.Remove(templateKey);
                    var templateResult = OptimizerEngine.Optimize(
                        context.Crafting, context.Crafter, template, context.Settings.LimitToInventory,
                        context.Settings.Goal, context.Settings.Efficiency,
                        context.Settings.DamageMinimumQuality, context.Settings.DamageMinimumQualityChance);
                    best = templateResult.Best;
                    if (best != null)
                        SellValueCache[templateKey] = new SellValueCacheEntry(partFingerprint, contextFingerprint, best);
                }

                if (best != null && (bestAlternative == null || best.Score > bestAlternative.Score)) bestAlternative = best;
            }
            if (bestAlternative != null && bestAlternative.Score > currentTemplateBest.Score)
            {
                string name = bestAlternative.Template?.TemplateName?.ToString() ?? "another weapon type";
                string target = context.Settings.Goal == OptimizationGoal.SmithingXp ? "Smithing XP" : "Sell Value";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Smithing Optimizer: {name} has a better {target} design ({bestAlternative.Score:F2} score). Kept the current weapon type."));
            }
        }

        private static List<CraftingTemplate> GetPrimaryUsageTemplates(WeaponDesignVM viewModel, CraftingTemplate current)
        {
            var templates = new List<CraftingTemplate>();
            if (PrimaryUsagesField?.GetValue(viewModel) is IEnumerable<CraftingTemplate> primaryUsages)
            {
                foreach (var template in primaryUsages)
                    if (template != null) templates.Add(template);
            }
            bool containsCurrent = false;
            foreach (var template in templates)
                if (ReferenceEquals(template, current)) { containsCurrent = true; break; }
            if (!containsCurrent) templates.Add(current);
            return templates;
        }

        private static string GetUnlockedPartFingerprint(CraftingTemplate template, CraftingCampaignBehavior behavior)
        {
            var parts = new List<string>();
            foreach (var piece in template.Pieces)
                if (behavior.IsOpened(piece, template)) parts.Add(piece.StringId);
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static string GetMaterialFingerprint(bool limitToInventory)
        {
            if (!limitToInventory) return "unlimited";
            var roster = MobileParty.MainParty?.ItemRoster;
            var model = Campaign.Current.Models.SmithingModel;
            var values = new string[9];
            for (int index = 0; index < values.Length; index++)
            {
                var item = model.GetCraftingMaterialItem((CraftingMaterials)index);
                values[index] = roster == null || item == null ? "0" : roster.GetItemNumber(item).ToString();
            }
            return string.Join(",", values);
        }

        private static string GetSmithingCapabilityFingerprint(Hero crafter)
        {
            var perkIds = new List<string>();
            foreach (var field in typeof(DefaultPerks.Crafting).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is PerkObject perk && crafter.GetPerkValue(perk))
                    perkIds.Add(perk.StringId);
            }
            perkIds.Sort(StringComparer.Ordinal);
            return $"{crafter.GetSkillValue(DefaultSkills.Crafting)}|{string.Join(",", perkIds)}";
        }

        private static void MaybeRecommendTrainingAlternative(OptimizationContext context, CraftingDesignCandidate? bestCraft)
        {
            if (!context.ShowAlternativeMessages || context.Settings.Goal != OptimizationGoal.SmithingXp)
                return;

            float craftScore = bestCraft == null ? 0f : GetCraftingXpScore(bestCraft, context.Settings.Efficiency);
            var alternative = SmithingTrainingAdvisor.FindBetterAlternative(context.Crafter, context.Settings.Efficiency, craftScore);
            if (alternative == null) return;

            string count = alternative.AvailableCount > 1 ? $" ({alternative.AvailableCount} available)" : string.Empty;
            string research = alternative.PartResearchGain > 0 ? $", {alternative.PartResearchGain} part research" : string.Empty;
            string logVersus = bestCraft == null
                ? string.Empty
                : $"; current craft: {bestCraft.SmithingXp} XP / {bestCraft.StaminaCost} stamina " +
                  $"= {bestCraft.SmithingXp / (float)Math.Max(1, bestCraft.StaminaCost):F2} XP/stamina";
            string displayVersus = bestCraft == null
                ? string.Empty
                : $"; current craft: {bestCraft.SmithingXp / (float)Math.Max(1, bestCraft.StaminaCost):F2} XP/stamina";
            OptimizerEngine.WriteLog(
                $"Training recommendation: {alternative.Description}; XP={alternative.RawXp}, " +
                $"Stamina={alternative.StaminaCost}, XP/Stamina={alternative.XpPerStamina:F2}{logVersus}.");
            InformationManager.DisplayMessage(new InformationMessage(
                $"Smithing Optimizer: Better training: {alternative.Description}{count} " +
                $"({alternative.XpPerStamina:F2} XP/stamina{research}){displayVersus}."));
        }

        private static float GetCraftingXpScore(CraftingDesignCandidate candidate, OptimizationEfficiency efficiency) => efficiency switch
        {
            OptimizationEfficiency.PerStamina => candidate.SmithingXp / (float)Math.Max(1, candidate.StaminaCost),
            OptimizationEfficiency.PerMaterialValue => candidate.SmithingXp / (float)Math.Max(1, candidate.MaterialReferenceValue),
            _ => candidate.SmithingXp
        };

        private static string GetXpBasisText(OptimizationEfficiency efficiency) => efficiency switch
        {
            OptimizationEfficiency.PerStamina => "XP per stamina",
            OptimizationEfficiency.PerMaterialValue => "XP per material value",
            _ => "Smithing XP"
        };

        private static void PresentOrderResult(OptimizationContext context, OptimizationRunResult result)
        {
            if (!context.ShowMessages) return;
            if (result.OrderEvaluationUnavailable)
            {
                InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Order completion checks are unavailable for this game build; no design was changed."));
                return;
            }
            if (!context.HasAvailableOrderHero)
            {
                InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: No available forge hero can complete the selected order."));
                return;
            }
            if (result.Best == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: No material-affordable complete design is available for the selected order."));
                return;
            }

            int requiredChance = context.Settings.OrderMinimumCompletionChance;
            int bestChance = (int)Math.Round(result.Best.OrderCompletionChance * 100f);
            if (requiredChance > 0 && result.Best.OrderCompletionChance < requiredChance / 100f)
            {
                string currentChance = result.Current == null
                    ? "the current design could not be evaluated"
                    : $"the current design has {(int)Math.Round(result.Current.OrderCompletionChance * 100f)}%";
                InformationManager.DisplayMessage(new InformationMessage($"Smithing Optimizer: No switch: {currentChance}; the best unlocked, affordable design reaches only {bestChance}% for {context.Crafter.Name} (requires {requiredChance}%). More suitable parts are needed."));
                return;
            }
            if (result.Current != null && !OptimizerEngine.IsOrderCandidateBetter(result.Best, result.Current, requiredChance))
            {
                int currentChance = (int)Math.Round(result.Current.OrderCompletionChance * 100f);
                InformationManager.DisplayMessage(new InformationMessage($"Smithing Optimizer: No switch: the current design is already the cheapest eligible order design ({currentChance}% completion chance)."));
                return;
            }

            ApplyDesign(context.ViewModel, context.Crafting, result.Best.Template ?? context.Template, result.Best);
            InformationManager.DisplayMessage(new InformationMessage(
                $"Smithing Optimizer: Applied order design ({bestChance}% completion chance, {result.Best.MaterialReferenceValue}d materials).",
                Color.FromUint(0x40FF40FF)));
        }

        private static string FormatGoal(CraftingDesignCandidate candidate, OptimizationGoal goal, OptimizationEfficiency efficiency, MinimumCraftQuality damageMinimumQuality)
        {
            string efficiencySuffix = efficiency switch
            {
                OptimizationEfficiency.PerStamina => $" ({candidate.Score:F2} per stamina)",
                OptimizationEfficiency.PerMaterialValue => $" ({candidate.Score:F2} per material value)",
                _ => string.Empty
            };
            return goal switch
            {
                OptimizationGoal.Damage => $"Damage: {candidate.MaxDamage} ({damageMinimumQuality}+ chance: {candidate.QualityChance:P0})",
                OptimizationGoal.SmithingXp => $"Smithing XP: {candidate.SmithingXp}{efficiencySuffix}",
                _ => candidate.UsesExpectedQualityScore ? $"Expected value: {candidate.Value}d{efficiencySuffix}" : $"Value: {candidate.Value}d{efficiencySuffix}"
            };
        }

        private static void ApplyDesign(WeaponDesignVM vm, Crafting craftingLogic, CraftingTemplate template, CraftingDesignCandidate candidate)
        {
            // SetDesignManually signature:
            // SetDesignManually(CraftingTemplate craftingTemplate, ValueTuple<CraftingPiece, int>[] pieces, bool forceChangeTemplate)
            if (SetDesignManuallyMethod == null) return;

            // We must use ValueTuple<CraftingPiece, int>
            var pieces = new ValueTuple<CraftingPiece, int>[4];
            pieces[0] = new ValueTuple<CraftingPiece, int>(candidate.Blade ?? WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade).CraftingPiece, candidate.BladeScale);
            pieces[1] = new ValueTuple<CraftingPiece, int>(candidate.Guard ?? WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Guard).CraftingPiece, candidate.GuardScale);
            pieces[2] = new ValueTuple<CraftingPiece, int>(candidate.Grip ?? WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Handle).CraftingPiece, candidate.GripScale);
            pieces[3] = new ValueTuple<CraftingPiece, int>(candidate.Pommel ?? WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Pommel).CraftingPiece, candidate.PommelScale);

            SetDesignManuallyMethod.Invoke(vm, new object[] { template, pieces, false });
        }
    }
}
