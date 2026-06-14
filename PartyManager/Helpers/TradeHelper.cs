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

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out int riding, out _, out _, out _, out _, out _);
            var snapshot = new PartyNeedsSnapshot(
                partySize,
                infantry,
                riding,
                GetKnownFoodItems().Select(item => item.StringId));
            var requests = PartyNeedsPlanner.BuildRequests(snapshot, PartyNeedsOptions.FromSettings(settings));
            foreach (var request in requests)
            {
                AutomationRegistry.RegisterRequest(request);
            }
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
