using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace TradingOptimizer
{
    public enum AutoLoopState
    {
        Idle,
        Selling,
        WaitingForReopen,
        Buying
    }

    [HarmonyPatch]
    public static class TradingPatches
    {
        public static SPInventoryVM? ActiveInventoryVM { get; private set; }
        public static AutoLoopState LoopState { get; set; } = AutoLoopState.Idle;

        private static int _ticksToWait = -1;

        [HarmonyPatch(typeof(SPInventoryVM), MethodType.Constructor)]
        [HarmonyPostfix]
        public static void SPInventoryVMConstructorPostfix(SPInventoryVM __instance)
        {
            ActiveInventoryVM = __instance;

            try
            {
                var logicField = typeof(SPInventoryVM).GetField("_inventoryLogic", BindingFlags.Instance | BindingFlags.NonPublic);
                var logic = logicField?.GetValue(__instance) as InventoryLogic;

                if (logic != null && logic.IsTrading)
                {
                    if (Settings.Instance.FullyAutomaticMode)
                    {
                        if (LoopState == AutoLoopState.Idle)
                        {
                            LoopState = AutoLoopState.Selling;
                            _ticksToWait = 15; // Wait 15 frames for UI population
                        }
                        else if (LoopState == AutoLoopState.WaitingForReopen)
                        {
                            LoopState = AutoLoopState.Buying;
                            _ticksToWait = 15; // Wait 15 frames for UI population
                        }
                    }
                }
            }
            catch (Exception)
            {
                LoopState = AutoLoopState.Idle;
            }
        }

        [HarmonyPatch(typeof(SPInventoryVM), "OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalizePostfix()
        {
            ActiveInventoryVM = null;

            if (LoopState == AutoLoopState.Selling)
            {
                LoopState = AutoLoopState.WaitingForReopen;
            }
            else if (LoopState == AutoLoopState.Buying)
            {
                LoopState = AutoLoopState.Idle;
            }
        }

        public static void TickUpdate()
        {
            if (_ticksToWait > 0)
            {
                _ticksToWait--;
                if (_ticksToWait == 0)
                {
                    ExecuteActivePhase();
                }
            }
        }

        public static void ManualTrigger()
        {
            if (ActiveInventoryVM == null) return;
            try
            {
                TradingEngine.RunOptimization(ActiveInventoryVM, isSellPhase: true, isBuyPhase: true);
            }
            catch (Exception)
            {
                // Ignore manual error
            }
        }

        private static void ExecuteActivePhase()
        {
            if (ActiveInventoryVM == null) return;

            try
            {
                if (LoopState == AutoLoopState.Selling)
                {
                    TradingEngine.RunOptimization(ActiveInventoryVM, isSellPhase: true, isBuyPhase: false);
                    TriggerHandleDone();
                }
                else if (LoopState == AutoLoopState.Buying)
                {
                    TradingEngine.RunOptimization(ActiveInventoryVM, isSellPhase: false, isBuyPhase: true);
                    TriggerHandleDone();
                }
            }
            catch (Exception)
            {
                LoopState = AutoLoopState.Idle;
            }
        }

        private static void TriggerHandleDone()
        {
            if (ActiveInventoryVM == null) return;
            try
            {
                var method = typeof(SPInventoryVM).GetMethod("HandleDone", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(ActiveInventoryVM, null);
            }
            catch (Exception)
            {
                LoopState = AutoLoopState.Idle;
            }
        }
    }
}
