using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;

namespace TradingOptimizer
{
    public class TradingOptimizerProvider : ITradeOrderProvider
    {
        public string ProviderName => "TradingOptimizer";

        private List<TradeOrder> SimulateAndCollectOrders(MobileParty party, Settlement settlement, bool runSell, bool runBuy, HashSet<string>? excludedItems = null)
        {
            var orders = new List<TradeOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.AutoTradeOnEnterSettlement) return orders;

            // Check if enough campaign days have passed to let the initial economy stabilize
            if (CampaignTime.Now.ToDays < settings.InitialSettlementDaysDelay)
            {
                TradingEngine.WriteLog($"[Settling Period] Skipped auto-trade simulation for {settlement.Name} (Campaign Day {CampaignTime.Now.ToDays:F1} < Settling Limit {settings.InitialSettlementDaysDelay})");
                return orders;
            }

            var tempLogic = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement);
            if (tempLogic == null) return orders;

            Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
            var vm = new SPInventoryVM(tempLogic, false, dummyFunc);

            // Capture initial counts on both sides to determine net transfers later
            var initialPlayerCounts = new Dictionary<string, int>();
            var playerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
            for (int i = 0; i < playerElements.Count; i++)
            {
                var el = playerElements[i];
                if (el.EquipmentElement.Item != null)
                {
                    string key = el.EquipmentElement.Item.StringId;
                    initialPlayerCounts[key] = initialPlayerCounts.TryGetValue(key, out int count) ? count + el.Amount : el.Amount;
                }
            }

            // Map StringId to EquipmentElement to construct TradeOrder later
            var eqElementMap = new Dictionary<string, EquipmentElement>();
            for (int i = 0; i < playerElements.Count; i++)
            {
                var el = playerElements[i];
                if (el.EquipmentElement.Item != null)
                {
                    eqElementMap[el.EquipmentElement.Item.StringId] = el.EquipmentElement;
                }
            }
            var otherElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
            for (int i = 0; i < otherElements.Count; i++)
            {
                var el = otherElements[i];
                if (el.EquipmentElement.Item != null)
                {
                    eqElementMap[el.EquipmentElement.Item.StringId] = el.EquipmentElement;
                }
            }

            // Run the actual optimization on the temp logic (mutates tempLogic)
            var report = TradingEngine.RunOptimization(vm, runSell, runBuy, excludedItems);

            // Compute net difference to determine what actually got traded in the simulation
            var finalPlayerCounts = new Dictionary<string, int>();
            var finalPlayerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
            for (int i = 0; i < finalPlayerElements.Count; i++)
            {
                var el = finalPlayerElements[i];
                if (el.EquipmentElement.Item != null)
                {
                    string key = el.EquipmentElement.Item.StringId;
                    finalPlayerCounts[key] = finalPlayerCounts.TryGetValue(key, out int count) ? count + el.Amount : el.Amount;
                }
            }

            // Adjust final counts to subtract yields of slaughter arbitrage
            foreach (var slaughter in report.ArbitrageSlaughters)
            {
                var item = slaughter.EqElement.Item;
                if (item != null && item.HorseComponent != null)
                {
                    int meatYield = item.HorseComponent.MeatCount * slaughter.Amount;
                    int hidesYield = item.HorseComponent.HideCount * slaughter.Amount;

                    string meatId = DefaultItems.Meat.StringId;
                    string hidesId = DefaultItems.Hides.StringId;

                    if (meatYield > 0 && finalPlayerCounts.ContainsKey(meatId))
                    {
                        finalPlayerCounts[meatId] = Math.Max(0, finalPlayerCounts[meatId] - meatYield);
                    }
                    if (hidesYield > 0 && finalPlayerCounts.ContainsKey(hidesId))
                    {
                        finalPlayerCounts[hidesId] = Math.Max(0, finalPlayerCounts[hidesId] - hidesYield);
                    }
                }
            }

            // All unique items across initial & final player inventory
            var allKeys = initialPlayerCounts.Keys.Union(finalPlayerCounts.Keys).Distinct();

            foreach (var key in allKeys)
            {
                int initialCount = initialPlayerCounts.TryGetValue(key, out int c1) ? c1 : 0;
                int finalCount = finalPlayerCounts.TryGetValue(key, out int c2) ? c2 : 0;
                int diff = finalCount - initialCount; // positive means we bought, negative means we sold

                if (diff > 0)
                {
                    orders.Add(new TradeOrder(eqElementMap[key], diff, true));
                }
                else if (diff < 0)
                {
                    orders.Add(new TradeOrder(eqElementMap[key], -diff, false));
                }
            }

            // Manually append the buy and slaughter orders for arbitrage slaughters
            foreach (var slaughter in report.ArbitrageSlaughters)
            {
                orders.Add(new TradeOrder(slaughter.EqElement, slaughter.Amount, true, false));
                orders.Add(new TradeOrder(slaughter.EqElement, slaughter.Amount, false, true));
            }

            // Print summary to in-game logs if not simulation mode (or even if simulation mode, depending on user settings)
            if (orders.Count > 0 && Hero.MainHero != null)
            {
                int initialGold = Hero.MainHero.Gold;
                int netGoldChange = report.SoldItems.Sum(s => s.Gold) - report.BoughtItems.Sum(b => b.Gold);
                int projectedFinalGold = initialGold + netGoldChange;
                TradingPatches.PrintTradeReport(projectedFinalGold, initialGold, report, settlement.Name.ToString());
            }

            return orders;
        }

        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ShouldSplitTransactions)
            {
                return new List<TradeOrder>();
            }
            return SimulateAndCollectOrders(party, settlement, runSell: true, runBuy: false);
        }

        public List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic)
        {
            var settings = Settings.Instance;
            if (settings == null) return new List<TradeOrder>();

            if (settings.ShouldSplitTransactions)
            {
                var preSellOrders = SimulateAndCollectOrders(party, settlement, runSell: true, runBuy: false);
                var excluded = new HashSet<string>(preSellOrders.Where(o => !o.IsBuy).Select(o => o.EquipmentElement.Item.Name.ToString()));

                return SimulateAndCollectOrders(party, settlement, runSell: false, runBuy: true, excludedItems: excluded);
            }
            else
            {
                return SimulateAndCollectOrders(party, settlement, runSell: true, runBuy: true);
            }
        }

    }
}
