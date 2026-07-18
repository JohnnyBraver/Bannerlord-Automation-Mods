using System;
using System.Collections.Generic;
using System.Reflection;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TradeOptimizer
{
    public static class InventoryVMExtensions
    {
        private static readonly FieldInfo? InventoryLogicField = typeof(SPInventoryVM)
            .GetField("_inventoryLogic", BindingFlags.Instance | BindingFlags.NonPublic);

        public static InventoryLogic? GetInventoryLogic(this SPInventoryVM vm)
        {
            if (vm == null) return null;
            return InventoryLogicField?.GetValue(vm) as InventoryLogic;
        }
    }

    public class TradedItemInfo
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public int Gold { get; set; }
        public float MarketPrice { get; set; }
    }

    public class TradeTransactionReport
    {
        public List<TradedItemInfo> SoldItems { get; } = new List<TradedItemInfo>();
        public List<TradedItemInfo> BoughtItems { get; } = new List<TradedItemInfo>();
        public HashSet<string> SoldNormalItems { get; } = new HashSet<string>();
        public List<(EquipmentElement EqElement, int Amount)> ArbitrageSlaughters { get; } = new List<(EquipmentElement, int)>();
    }

    public static class TradingEngine
    {
        public static void WriteLog(string message)
        {
            SettlementAutomationCore.Helpers.Logger.WriteLog("TradeOptimizer", message);
        }

        internal static TradePlan PlanOptimization(
            SPInventoryVM vm,
            bool isSellPhase,
            bool isBuyPhase,
            TradeContext tradeContext,
            HashSet<string>? excludedItems = null,
            bool? applyTransfers = null)
        {
            if (tradeContext == null) throw new ArgumentNullException(nameof(tradeContext));

            var settings = Settings.Instance;
            var plan = new TradePlan();
            if (vm == null || settings == null)
            {
                return plan;
            }

            var request = new TradePlanningRequest(
                vm,
                isSellPhase,
                isBuyPhase,
                tradeContext,
                settings,
                excludedItems,
                applyTransfers ?? !settings.SimulationMode);

            return new TradePlanner().CreatePlan(request);
        }

        public static TradeTransactionReport RunOptimization(
            SPInventoryVM vm,
            bool isSellPhase,
            bool isBuyPhase,
            TradeContext tradeContext,
            HashSet<string>? excludedItems = null)
        {
            return PlanOptimization(vm, isSellPhase, isBuyPhase, tradeContext, excludedItems).ToTransactionReport();
        }

        internal static string GetTradeIdentityKey(EquipmentElement equipmentElement)
        {
            string itemId = equipmentElement.Item?.StringId ?? string.Empty;
            string modifierId = equipmentElement.ItemModifier?.StringId ?? string.Empty;
            return $"{itemId}::{modifierId}";
        }
    }
}
