using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TradingOptimizer
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

    public class TradeTransactionReport
    {
        public List<(string Name, int Count, int Gold)> SoldItems { get; } = new List<(string, int, int)>();
        public List<(string Name, int Count, int Gold)> BoughtItems { get; } = new List<(string, int, int)>();
    }

    public static class TradingEngine
    {
        public static TradeTransactionReport RunOptimization(SPInventoryVM vm, bool isSellPhase, bool isBuyPhase)
        {
            var report = new TradeTransactionReport();
            if (vm == null) return report;

            var logic = vm.GetInventoryLogic();

            int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1;
            float netWeightAdded = 0f;

            // 1. Sell Phase: Sell profitable items from player inventory (RightItemListVM)
            if (isSellPhase && vm.RightItemListVM != null)
            {
                var playerItems = vm.RightItemListVM.ToList();
                foreach (var item in playerItems)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                    var itemObj = item.ItemRosterElement.EquipmentElement.Item;

                    // Only sell trade goods (commodities)
                    if (!itemObj.IsTradeGood) continue;

                    int minToKeep = 0;
                    if (itemObj.IsFood)
                    {
                        minToKeep = (int)Math.Ceiling(partySize * Settings.Instance.FoodDaysToKeepPerSoldier);
                    }

                    int maxSellable = item.ItemCount - minToKeep;
                    if (maxSellable <= 0) continue;

                    int sold = 0;
                    int totalGoldGained = 0;
                    float itemWeight = itemObj.Weight;

                    while (sold < maxSellable)
                    {
                        bool loopSell = false;
                        if (item.ProfitType == 1 || item.ProfitType == 2)
                        {
                            loopSell = true;
                        }
                        else if (Settings.Instance.UseAveragePriceFallback && logic != null)
                        {
                            float avgPrice = itemObj.Value * logic.GetAveragePriceFactorItemCategory(itemObj.ItemCategory);
                            int currentPrice = logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, false);
                            if (currentPrice >= avgPrice * Settings.Instance.SellPriceThresholdFactor)
                            {
                                loopSell = true;
                            }
                        }

                        if (!loopSell) break;

                        int price = logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, false) : itemObj.Value;
                        item.ExecuteSellSingle();
                        sold++;
                        totalGoldGained += price;
                        netWeightAdded -= itemWeight;
                    }

                    if (sold > 0)
                    {
                        report.SoldItems.Add((itemObj.Name.ToString(), sold, totalGoldGained));
                    }
                }
            }

            // 2. Buy Phase: Buy underpriced items from merchant inventory (LeftItemListVM)
            if (isBuyPhase && vm.LeftItemListVM != null)
            {
                var merchantItems = vm.LeftItemListVM.ToList();
                foreach (var item in merchantItems)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                    var itemObj = item.ItemRosterElement.EquipmentElement.Item;

                    // Only buy trade goods
                    if (!itemObj.IsTradeGood) continue;

                    // Check Settings filters (Livestock vs Mounts)
                    if (itemObj.IsAnimal && !itemObj.IsMountable)
                    {
                        if (!Settings.Instance.TradeLivestock) continue;
                    }
                    if (itemObj.IsMountable)
                    {
                        if (!Settings.Instance.TradeMounts) continue;
                    }

                    int bought = 0;
                    int totalGoldSpent = 0;
                    float itemWeight = itemObj.Weight;

                    while (bought < item.ItemCount)
                    {
                        // Check Carrying Capacity limit
                        if (Settings.Instance.LimitToInventoryCapacity && MobileParty.MainParty != null)
                        {
                            float currentWeight = GetRosterWeight(MobileParty.MainParty.ItemRoster);
                            float projectedWeight = currentWeight + netWeightAdded;
                            if (projectedWeight + itemWeight >= MobileParty.MainParty.InventoryCapacity)
                            {
                                break; // Overburdened
                            }
                        }

                        // Check Stack size and value limits
                        var playerItem = vm.RightItemListVM?.FirstOrDefault(r => r.ItemRosterElement.EquipmentElement.Item == itemObj);
                        int currentlyOwned = (playerItem != null ? playerItem.ItemCount : 0) + bought;

                        if (currentlyOwned >= Settings.Instance.MaxStackSizeToBuy)
                        {
                            break; // Hit size limit
                        }
                        if (currentlyOwned * (logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, true) : itemObj.Value) >= Settings.Instance.MaxStackValueToBuy)
                        {
                            break; // Hit value limit
                        }

                        // Check price condition
                        bool loopBuy = false;
                        if (item.ProfitType == 1 || item.ProfitType == 2)
                        {
                            loopBuy = true;
                        }
                        else if (Settings.Instance.UseAveragePriceFallback && logic != null)
                        {
                            float avgPrice = itemObj.Value * logic.GetAveragePriceFactorItemCategory(itemObj.ItemCategory);
                            int currentPrice = logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, true);
                            if (currentPrice <= avgPrice * Settings.Instance.BuyPriceThresholdFactor)
                            {
                                loopBuy = true;
                            }
                        }

                        if (!loopBuy) break;

                        int price = logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, true) : itemObj.Value;
                        item.ExecuteBuySingle();
                        bought++;
                        totalGoldSpent += price;
                        netWeightAdded += itemWeight;
                    }

                    if (bought > 0)
                    {
                        report.BoughtItems.Add((itemObj.Name.ToString(), bought, totalGoldSpent));
                    }
                }
            }

            return report;
        }

        private static float GetRosterWeight(ItemRoster roster)
        {
            if (roster == null) return 0f;
            float weight = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item != null)
                {
                    weight += element.EquipmentElement.Item.Weight * element.Amount;
                }
            }
            return weight;
        }
    }
}
