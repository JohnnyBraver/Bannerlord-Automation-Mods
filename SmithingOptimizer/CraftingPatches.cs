using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmithingOptimizer
{
    public static class CraftingPatches
    {
        private enum OptimizationTrigger { Manual, AutoUnlock }

        private sealed class OptimizationSettingsSnapshot
        {
            public bool LimitToInventory { get; }
            public OptimizationGoal Goal { get; }
            public OptimizationEfficiency Efficiency { get; }
            public MinimumCraftQuality DamageMinimumQuality { get; }
            public int DamageMinimumQualityChance { get; }

            public OptimizationSettingsSnapshot(Settings settings)
            {
                LimitToInventory = settings.LimitToInventory;
                Goal = settings.Goal;
                Efficiency = settings.Efficiency;
                DamageMinimumQuality = settings.DamageMinimumQuality;
                DamageMinimumQualityChance = settings.DamageMinimumQualityChance;
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
            public bool ShowMessages => Trigger == OptimizationTrigger.Manual;

            public OptimizationContext(WeaponDesignVM viewModel, Crafting crafting, Hero crafter, OptimizationSettingsSnapshot settings, OptimizationTrigger trigger)
            {
                ViewModel = viewModel;
                Crafting = crafting;
                Template = crafting.CurrentCraftingTemplate;
                Crafter = crafter;
                Settings = settings;
                Trigger = trigger;
            }
        }

        private static readonly FieldInfo? CraftingField = typeof(WeaponDesignVM).GetField("_crafting", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo? SetDesignManuallyMethod = typeof(WeaponDesignVM).GetMethod("SetDesignManually", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int _isOptimizing;
        public static WeaponDesignVM? ActiveWeaponDesignVM { get; private set; }

        public static void OnWeaponDesignVMConstructed(WeaponDesignVM __instance)
        {
            ActiveWeaponDesignVM = __instance;
        }

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
            var settings = Settings.Instance;
            if (settings != null && settings.AutoSwitchEnabled)
            {
                RequestOptimization(OptimizationTrigger.AutoUnlock);
            }
        }

        public static void ManualTrigger()
        {
            RequestOptimization(OptimizationTrigger.Manual);
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

            if (Interlocked.Exchange(ref _isOptimizing, 1) != 0) return;
            try
            {
                var context = CaptureContext(ActiveWeaponDesignVM, settings, trigger);
                if (context == null) return;
                RunOptimization(context);
            }
            catch (Exception ex)
            {
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
            return new OptimizationContext(viewModel, crafting, crafter, new OptimizationSettingsSnapshot(settings), trigger);
        }

        private static void RunOptimization(OptimizationContext context)
        {
            var result = OptimizerEngine.Optimize(
                context.Crafting,
                context.Crafter,
                context.Template,
                context.Settings.LimitToInventory,
                context.Settings.Goal,
                context.Settings.Efficiency,
                context.Settings.DamageMinimumQuality,
                context.Settings.DamageMinimumQualityChance);
            if (result.ExpectedQualityScoringDisabled && context.ShowMessages)
            {
                InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Expected quality scoring and Damage reliability gating are unavailable for this game build; Sell Value is using base item value."));
            }
            if (result.Best == null)
            {
                if (context.ShowMessages) InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: No material-affordable complete design was found."));
                return;
            }

            if (result.Current != null && result.Best.Score <= result.Current.Score)
            {
                if (context.ShowMessages) InformationManager.DisplayMessage(new InformationMessage("Smithing Optimizer: Current design is already optimal."));
                return;
            }

            ApplyDesign(context.ViewModel, context.Crafting, result.Best);
            InformationManager.DisplayMessage(new InformationMessage(
                $"Smithing Optimizer: Applied new local optimum! ({FormatGoal(result.Best, context.Settings.Goal)})",
                Color.FromUint(0x40FF40FF)));
        }

        private static string FormatGoal(CraftingDesignCandidate candidate, OptimizationGoal goal)
        {
            return goal switch
            {
                OptimizationGoal.Damage => $"Damage: {candidate.MaxDamage} ({candidate.Score:F2} score)",
                OptimizationGoal.SmithingXp => $"Smithing XP: {candidate.SmithingXp} ({candidate.Score:F2} score)",
                _ => candidate.UsesExpectedQualityScore ? $"Expected value: {candidate.Value}d ({candidate.Score:F2} score)" : $"Value: {candidate.Value}d ({candidate.Score:F2} score)"
            };
        }

        private static void ApplyDesign(WeaponDesignVM vm, Crafting craftingLogic, CraftingDesignCandidate candidate)
        {
            var template = craftingLogic.CurrentCraftingTemplate;

            // SetDesignManually signature:
            // SetDesignManually(CraftingTemplate craftingTemplate, ValueTuple<CraftingPiece, int>[] pieces, bool forceChangeTemplate)
            if (SetDesignManuallyMethod == null) return;

            var emptyPiece = WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade).CraftingPiece;

            // We must use ValueTuple<CraftingPiece, int>
            var pieces = new ValueTuple<CraftingPiece, int>[4];
            pieces[0] = new ValueTuple<CraftingPiece, int>(candidate.Blade ?? emptyPiece, candidate.BladeScale);
            pieces[1] = new ValueTuple<CraftingPiece, int>(candidate.Guard ?? emptyPiece, candidate.GuardScale);
            pieces[2] = new ValueTuple<CraftingPiece, int>(candidate.Grip ?? emptyPiece, candidate.GripScale);
            pieces[3] = new ValueTuple<CraftingPiece, int>(candidate.Pommel ?? emptyPiece, candidate.PommelScale);

            SetDesignManuallyMethod.Invoke(vm, new object[] { template, pieces, false });
        }
    }
}
