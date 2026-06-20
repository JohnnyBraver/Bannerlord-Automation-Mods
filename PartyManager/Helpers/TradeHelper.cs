using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

        public static TradeProposal AnalyzeHerdingCleanup(
            TradeContext context,
            Settings settings)
        {
            var actions = new List<TradeAction>();
            if (context == null || context.Party == null || settings == null || !settings.PreventHerdingPenalty)
            {
                return new TradeProposal(actions);
            }

            var party = context.Party;

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                out var ridingItems, out var packItems, out var livestockItems);

            int totalAnimals = riding + pack + livestock;
            int maxAllowed = (infantry * 2) + (cavalry * 1);
            int freePartySlots = Math.Max(0, party.Party.PartySizeLimit - party.MemberRoster.TotalManCount);

            if (totalAnimals > maxAllowed)
            {
                int excess = totalAnimals - maxAllowed;
                bool slaughter = settings.SlaughterAnimalsForHerding;

                // 1. Process Livestock first
                foreach (var el in livestockItems)
                {
                    if (excess <= 0) break;
                    int available = GetSellableQuantity(el, context);
                    int toProcess = Math.Min(excess, available);
                    if (toProcess <= 0) continue;
                    actions.Add(new TradeAction(el.EquipmentElement, toProcess, slaughter ? TradeActionType.Slaughter : TradeActionType.Sell));
                    excess -= toProcess;
                }

                // 2. Process Pack Animals before riding mounts. Pack animals help cargo, but riding mounts
                // prevent immediate speed loss after recruiting more foot troops.
                if (excess > 0)
                {
                    foreach (var el in packItems)
                    {
                        if (excess <= 0) break;
                        int available = GetSellableQuantity(el, context);
                        int toProcess = Math.Min(excess, available);
                        if (toProcess <= 0) continue;
                        actions.Add(new TradeAction(el.EquipmentElement, toProcess, slaughter ? TradeActionType.Slaughter : TradeActionType.Sell));
                        excess -= toProcess;
                    }
                }

                // 3. Process Riding Mounts (respecting SellRidingMountsSetting and foot reserve)
                var ridingMode = settings.SellRidingMountsSetting;
                if (excess > 0 && ridingMode != SellRidingMountsMode.Never)
                {
                    int excessRiding = PartyLogisticsPlanner.CalculateProcessableRidingMounts(
                        riding,
                        infantry,
                        freePartySlots,
                        ridingMode,
                        settings.PreserveRidingMountsForFootReserve);

                    if (excessRiding > 0)
                    {
                        foreach (var el in ridingItems)
                        {
                            if (excess <= 0 || excessRiding <= 0) break;
                            int available = GetSellableQuantity(el, context);
                            int toSell = Math.Min(Math.Min(excess, excessRiding), available);
                            if (toSell <= 0) continue;
                            actions.Add(new TradeAction(el.EquipmentElement, toSell, TradeActionType.Sell));
                            excess -= toSell;
                            excessRiding -= toSell;
                        }
                    }
                }
            }

            return new TradeProposal(actions);
        }

        private static int GetSellableQuantity(ItemRosterElementInfo element, TradeContext context)
        {
            var sellable = context.SellableItems.FirstOrDefault(item => item.Matches(element.EquipmentElement));
            return Math.Min(element.Amount, sellable?.AvailableQuantity ?? 0);
        }

        public static void SubmitAutomationRequests(MobileParty party, Settings settings)
        {
            if (settings == null || party == null || Hero.MainHero == null) return;

            int partySize = party.MemberRoster.TotalManCount;
            if (partySize <= 0) return;

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out int riding, out _, out _, out _, out _, out _);
            var mountDemand = CalculateMountDemand(party);
            var snapshot = new PartyNeedsSnapshot(
                partySize,
                infantry,
                riding,
                CountUpgradeMounts(party),
                mountDemand.CurrentMountedTroops,
                mountDemand.TroopsWithMountedUpgrade,
                mountDemand.CavalryFinalTierUpgradeTroops,
                mountDemand.TroopsWithFinalMountedUpgrade,
                GetKnownFoodItems().Select(item => item.StringId));
            var requests = PartyNeedsPlanner.BuildRequests(snapshot, PartyNeedsOptions.FromSettings(settings));
            foreach (var request in requests)
            {
                AutomationRegistry.RegisterRequest(request);
            }
        }

        private static int CountUpgradeMounts(MobileParty party)
        {
            if (party?.ItemRoster == null) return 0;

            int count = 0;
            for (int i = 0; i < party.ItemRoster.Count; i++)
            {
                var element = party.ItemRoster.GetElementCopyAtIndex(i);
                var item = element.EquipmentElement.Item;
                if (item != null &&
                    element.Amount > 0 &&
                    item.IsMountable &&
                    item.HorseComponent != null &&
                    !item.HorseComponent.IsPackAnimal &&
                    string.Equals(item.ItemCategory?.StringId, "war_horse", StringComparison.OrdinalIgnoreCase))
                {
                    count += element.Amount;
                }
            }

            return count;
        }

        private static MountDemandCounts CalculateMountDemand(MobileParty party)
        {
            var counts = new MountDemandCounts();
            if (party?.MemberRoster == null) return counts;

            for (int i = 0; i < party.MemberRoster.Count; i++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(i);
                var troop = element.Character;
                int number = element.Number;
                if (troop == null || troop.IsHero || number <= 0)
                {
                    continue;
                }

                bool isMounted = troop.IsMounted;
                bool hasMountedUpgrade = HasMountedUpgrade(troop);
                bool hasFinalMountedUpgrade = HasFinalMountedUpgrade(troop);

                if (isMounted)
                {
                    counts.CurrentMountedTroops += number;
                }
                if (hasMountedUpgrade)
                {
                    counts.TroopsWithMountedUpgrade += number;
                }
                if (isMounted && hasFinalMountedUpgrade)
                {
                    counts.CavalryFinalTierUpgradeTroops += number;
                }
                if (hasFinalMountedUpgrade)
                {
                    counts.TroopsWithFinalMountedUpgrade += number;
                }
            }

            return counts;
        }

        private static bool HasMountedUpgrade(CharacterObject troop)
        {
            if (troop?.UpgradeTargets == null) return false;

            foreach (var target in troop.UpgradeTargets)
            {
                if (target == null) continue;
                if (target.IsMounted || HasMountedUpgrade(target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFinalMountedUpgrade(CharacterObject troop)
        {
            if (troop == null) return false;

            var leafTroops = new List<CharacterObject>();
            SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(troop, leafTroops);
            return leafTroops.Any(leaf => leaf != null && leaf.IsMounted);
        }

        private class MountDemandCounts
        {
            public int CurrentMountedTroops { get; set; }
            public int TroopsWithMountedUpgrade { get; set; }
            public int CavalryFinalTierUpgradeTroops { get; set; }
            public int TroopsWithFinalMountedUpgrade { get; set; }
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
