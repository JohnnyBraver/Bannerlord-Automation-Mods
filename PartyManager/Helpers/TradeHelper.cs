using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using SettlementAutomationCore;

namespace PartyManager.Helpers
{
    public static class TradeHelper
    {
        private class RosterElementInfo
        {
            public EquipmentElement EqElement { get; }
            public int Amount { get; }
            public int Price { get; }
            public RosterElementInfo(EquipmentElement eqElement, int amount, int price)
            {
                EqElement = eqElement;
                Amount = amount;
                Price = price;
            }
        }

        public static List<TradeOrder> GetPreSellOrders(MobileParty party, Settings settings)
        {
            var orders = new List<TradeOrder>();
            if (settings == null || !settings.PreventHerdingPenalty) return orders;

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                out var ridingItems, out var packItems, out var livestockItems);

            int totalAnimals = riding + pack + livestock;
            int maxAllowed = (infantry * 2) + (cavalry * 1);

            if (totalAnimals > maxAllowed)
            {
                int excess = totalAnimals - maxAllowed;
                bool slaughter = settings.SlaughterAnimalsForHerding;

                // 1. Process Livestock first
                foreach (var el in livestockItems)
                {
                    if (excess <= 0) break;
                    int toProcess = Math.Min(excess, el.Amount);
                    orders.Add(new TradeOrder(el.EquipmentElement, toProcess, false, slaughter));
                    excess -= toProcess;
                }

                // 2. Process Riding Mounts (respecting SellRidingMountsSetting)
                var ridingMode = settings.SellRidingMountsSetting;
                if (excess > 0 && ridingMode != SellRidingMountsMode.Never)
                {
                    int excessRiding = 0;
                    if (ridingMode == SellRidingMountsMode.ExcessOnly)
                    {
                        excessRiding = Math.Max(0, riding - infantry);
                    }
                    else if (ridingMode == SellRidingMountsMode.All)
                    {
                        excessRiding = riding;
                    }

                    if (excessRiding > 0)
                    {
                        foreach (var el in ridingItems)
                        {
                            if (excess <= 0 || excessRiding <= 0) break;
                            int available = el.Amount;
                            int toSell = Math.Min(Math.Min(excess, excessRiding), available);
                            orders.Add(new TradeOrder(el.EquipmentElement, toSell, false, false));
                            excess -= toSell;
                            excessRiding -= toSell;
                        }
                    }
                }

                // 3. Process Pack Animals if still over herding limit
                if (excess > 0)
                {
                    foreach (var el in packItems)
                    {
                        if (excess <= 0) break;
                        int toProcess = Math.Min(excess, el.Amount);
                        orders.Add(new TradeOrder(el.EquipmentElement, toProcess, false, slaughter));
                        excess -= toProcess;
                    }
                }
            }

            return orders;
        }

        public static void SubmitAutomationRequests(MobileParty party, Settings settings)
        {
            if (settings == null || party == null || Hero.MainHero == null) return;

            int partySize = party.MemberRoster.TotalManCount;
            if (partySize <= 0) return;

            // 1. Submit layered food requests.
            if (settings.AutoBuyFood)
            {
                int criticalTarget = CalculateFoodUnitsForDays(partySize, settings.CriticalFoodDays);
                AutomationRegistry.RegisterRequest(AutomationRequest.ForInventoryTarget(
                    "PartyManager",
                    RequestType.ItemCategory,
                    "Food",
                    criticalTarget,
                    settings.CriticalFoodRequestProfile,
                    9
                ));

                int totalFoodTarget = CalculateFoodUnitsForDays(partySize, settings.PartyFoodDaysToKeep);
                if (partySize >= settings.MinPartySizeForVariety)
                {
                    var knownFoodItems = GetKnownFoodItems();
                    if (knownFoodItems.Count > 0)
                    {
                        int perFoodTarget = Math.Max(1, (int)Math.Ceiling(totalFoodTarget / (double)knownFoodItems.Count));
                        foreach (var foodItem in knownFoodItems)
                        {
                            AutomationRegistry.RegisterRequest(AutomationRequest.ForInventoryTarget(
                                "PartyManager",
                                RequestType.SpecificItem,
                                foodItem.StringId,
                                perFoodTarget,
                                settings.FoodVarietyRequestProfile,
                                5
                            ));
                        }
                    }
                }

                AutomationRegistry.RegisterRequest(AutomationRequest.ForInventoryTarget(
                    "PartyManager",
                    RequestType.ItemCategory,
                    "Food",
                    totalFoodTarget,
                    settings.FoodBufferRequestProfile,
                    5
                ));
            }

            // 2. Submit mount requests.
            if (settings.AutoBuyMounts)
            {
                AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out int riding, out _, out _, out _, out _, out _);
                int missing = infantry - riding;
                if (missing > 0)
                {
                    // Scale priority by percentage of infantry that is currently unmounted.
                    int scalePriority = Math.Max(1, Math.Min(9, (int)Math.Round(1 + 8 * ((float)missing / infantry))));
                    AutomationRegistry.RegisterRequest(AutomationRequest.ForInventoryTarget(
                        "PartyManager",
                        RequestType.ItemCategory,
                        "Horse",
                        infantry, // Cumulative target is to reach fully mounted infantry
                        settings.RidingMountRequestProfile,
                        scalePriority
                    ));
                }
            }
        }

        private static int CalculateFoodUnitsForDays(int partySize, int days)
        {
            return Math.Max(1, (int)Math.Ceiling(partySize * Math.Max(1, days) / 20.0f));
        }

        private static List<ItemObject> GetKnownFoodItems()
        {
            return MBObjectManager.Instance
                .GetObjectTypeList<ItemObject>()
                .Where(item => item != null && item.IsFood && !string.IsNullOrEmpty(item.StringId))
                .GroupBy(item => item.StringId)
                .Select(group => group.First())
                .OrderBy(item => item.StringId)
                .ToList();
        }
    }
}
