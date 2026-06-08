using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmithingOptimizer
{
    [HarmonyPatch]
    public static class CraftingPatches
    {
        public static WeaponDesignVM? ActiveWeaponDesignVM { get; private set; }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnCraftingLogicRefreshed")]
        [HarmonyPostfix]
        public static void OnCraftingLogicRefreshedPostfix(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalizePostfix()
        {
            ActiveWeaponDesignVM = null;
        }

        [HarmonyPatch(typeof(WeaponDesignVM), "OnNewPieceUnlocked")]
        [HarmonyPostfix]
        public static void OnNewPieceUnlockedPostfix(WeaponDesignVM __instance, CraftingPiece piece)
        {
            ActiveWeaponDesignVM = __instance;
            if (Settings.Instance.AutoSwitchEnabled)
            {
                TriggerOptimization(silentOnNoImprovement: true);
            }
        }

        public static void ManualTrigger()
        {
            TriggerOptimization(silentOnNoImprovement: false);
        }

        private static void TriggerOptimization(bool silentOnNoImprovement)
        {
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
                // 1. Get active Crafting logic via reflection
                var craftingField = typeof(WeaponDesignVM).GetField("_crafting", BindingFlags.Instance | BindingFlags.NonPublic);
                if (craftingField == null) return;
                var craftingLogic = craftingField.GetValue(ActiveWeaponDesignVM) as Crafting;
                if (craftingLogic == null) return;

                // 2. Run Optimizer
                var results = OptimizerEngine.Optimize(
                    craftingLogic,
                    Hero.MainHero,
                    craftingLogic.CurrentCraftingTemplate,
                    Settings.Instance.LimitToInventory,
                    Settings.Instance.Goal
                );

                if (results == null || results.Count == 0)
                {
                    if (!silentOnNoImprovement)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: No valid designs fit your budget."));
                    }
                    return;
                }

                var best = results[0];

                // 3. Compare with current design to see if we improved
                if (IsBetterThanCurrent(craftingLogic, best, Settings.Instance.Goal))
                {
                    ApplyDesign(ActiveWeaponDesignVM, craftingLogic, best);

                    string goalText = Settings.Instance.Goal == OptimizationGoal.Damage ? $"Damage: {best.MaxDamage}" : $"Value: {best.Value}d";
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Smithing Optimizer: Applied new optimal design! ({goalText})",
                        Color.FromUint(0x40FF40FF) // Green
                    ));
                }
                else
                {
                    if (!silentOnNoImprovement)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Current design is already optimal."));
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Smithing Optimizer Error: {ex.Message}"));
            }
        }

        private static bool IsBetterThanCurrent(Crafting craftingLogic, CraftingDesignCandidate best, OptimizationGoal goal)
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
            else // Profit
            {
                int currentValue = currentItem.Value;
                return best.Value > currentValue;
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
