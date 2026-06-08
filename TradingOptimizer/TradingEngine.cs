using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TradingOptimizer
{
    public static class TradingEngine
    {
        public static void RunOptimization(SPInventoryVM vm, bool isSellPhase, bool isBuyPhase)
        {
            if (vm == null) return;

            int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1;
            float netWeightAdded = 0f;

            // 1. Sell Phase: Sell profitable items from player inventory (RightItemListVM)
            if (isSellPhase && vm.RightItemListVM != null)
            {
                var playerItems = vm.RightItemListVM.ToList();
                foreach (var item in playerItems)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                    // ProfitType: Default = 0, Profit = 1, HighProfit = 2
                    if (item.ProfitType == 1 || item.ProfitType == 2)
                    {
                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;
                        int minToKeep = 0;
                        if (itemObj.IsFood)
                        {
                            minToKeep = (int)Math.Ceiling(partySize * Settings.Instance.FoodDaysToKeepPerSoldier);
                        }

                        int maxSellable = item.ItemCount - minToKeep;
                        if (maxSellable <= 0) continue;

                        int sold = 0;
                        float itemWeight = itemObj.Weight;

                        while (sold < maxSellable && (item.ProfitType == 1 || item.ProfitType == 2))
                        {
                            item.ExecuteSellSingle();
                            sold++;
                            netWeightAdded -= itemWeight;
                        }
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

                    // ProfitType: Default = 0, Profit = 1, HighProfit = 2
                    if (item.ProfitType == 1 || item.ProfitType == 2)
                    {
                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;

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
                        float itemWeight = itemObj.Weight;

                        while (bought < item.ItemCount && (item.ProfitType == 1 || item.ProfitType == 2))
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

                            // Check Stack Limits
                            var playerItem = vm.RightItemListVM?.FirstOrDefault(r => r.ItemRosterElement.EquipmentElement.Item == itemObj);
                            int currentlyOwned = (playerItem != null ? playerItem.ItemCount : 0) + bought;

                            if (currentlyOwned >= Settings.Instance.MaxStackSizeToBuy)
                            {
                                break; // Hit size limit
                            }
                            if (currentlyOwned * item.ItemCost >= Settings.Instance.MaxStackValueToBuy)
                            {
                                break; // Hit value limit
                            }

                            item.ExecuteBuySingle();
                            bought++;
                            netWeightAdded += itemWeight;
                        }
                    }
                }
            }
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
