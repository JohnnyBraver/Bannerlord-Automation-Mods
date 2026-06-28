using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;
using PartyManager.Filters;
using PartyManager.Helpers;

namespace PartyManager
{
    public class PartyManagerProvider : ISettlementRecruitmentProvider, IGarrisonOrderProvider, IPrisonerDispositionProvider, IAutomationRequestProvider, ISettlementCleanupProvider, IPostBattleAutomationProvider, IAutomationReservationProvider
    {
        public string ProviderName => "PartyManager";

        // ----------------------------------------------------
        // IAutomationRequestProvider
        // ----------------------------------------------------
        public void SubmitAutomationRequests(AutomationRequestContext context)
        {
            var settings = Settings.Instance;
            if (settings != null && settings.ModEnabled)
            {
                TradeHelper.SubmitAutomationRequests(context.Party, settings);
            }
        }

        public TradeProposal AnalyzeSettlementCleanup(TradeContext context)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
            {
                return new TradeProposal(new List<TradeAction>());
            }

            return TradeHelper.AnalyzeHerdingCleanup(context, settings);
        }

        public PostBattleAutomationResult ProcessPostBattle(PostBattleAutomationContext context)
        {
            var result = new PostBattleAutomationResult();
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || context == null || context.Party == null)
            {
                return result;
            }

            AddResultActivities(result, ProcessPostBattleSlaughter(context, settings));
            AddResultActivities(result, PrisonerHelper.ProcessPostBattleDiscard(context.Party, settings));
            AddPostBattleSpeedWarnings(result, context.Party, settings);
            return result;
        }

        public IReadOnlyList<ItemReservation> GetReservations(AutomationReservationContext context)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || context == null || context.Party == null || !settings.PreserveRidingMountsForFootReserve)
            {
                return new List<ItemReservation>();
            }

            AnimalCalculator.CalculatePartyAnimals(context.Party, out int infantry, out _, out _, out _, out _,
                out _, out _, out _);
            int freePartySlots = Math.Max(0, context.Party.Party.PartySizeLimit - context.Party.MemberRoster.TotalManCount);
            int reserve = PartyLogisticsPlanner.CalculateRidingMountReserve(infantry, freePartySlots);
            if (reserve <= 0)
            {
                return new List<ItemReservation>();
            }

            return new List<ItemReservation>
            {
                new ItemReservation(ProviderName, "Horse", true, reserve)
            };
        }

        private static void AddResultActivities(PostBattleAutomationResult target, PostBattleAutomationResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var activity in source.Activities)
            {
                target.AddActivity(activity.Message);
            }
        }

        private static PostBattleAutomationResult ProcessPostBattleSlaughter(PostBattleAutomationContext context, Settings settings)
        {
            var result = new PostBattleAutomationResult();
            var party = context.Party;
            if (settings.PostBattleSlaughterSetting == PostBattleSlaughterMode.None)
            {
                return result;
            }

            int maxAllowed = HerdingCalculator.GetMaxAnimalsAllowed(party);
            int currentAnimals = HerdingCalculator.GetCurrentAnimalsCount(party);
            if (currentAnimals <= maxAllowed)
            {
                return result;
            }

            int excess = currentAnimals - maxAllowed;
            var slaughtered = SlaughterAnimalsField(
                party,
                excess,
                settings.PostBattleSlaughterSetting,
                settings.PreserveRidingMountsForFootReserve,
                context.ItemReservations);
            foreach (var line in slaughtered)
            {
                result.AddActivity(line);
            }

            return result;
        }

        private static IReadOnlyList<string> SlaughterAnimalsField(
            MobileParty party,
            int excess,
            PostBattleSlaughterMode mode,
            bool preserveRidingMountReserve,
            IReadOnlyList<ItemReservation> itemReservations)
        {
            var itemRoster = party.ItemRoster;
            var candidates = new List<AnimalSlaughterCandidate>();
            var lockKeys = InventoryLockHelper.GetCurrentLockKeys();
            var reservationStates = BuildReservationStates(itemReservations);
            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out int riding, out _, out _,
                out _, out _, out _);
            int freePartySlots = Math.Max(0, party.Party.PartySizeLimit - party.MemberRoster.TotalManCount);
            int processableRiding = PartyLogisticsPlanner.CalculateProcessableRidingMounts(
                riding,
                infantry,
                freePartySlots,
                SellRidingMountsMode.All,
                preserveRidingMountReserve);

            for (int i = 0; i < itemRoster.Count; i++)
            {
                var el = itemRoster.GetElementCopyAtIndex(i);
                var item = el.EquipmentElement.Item;
                if (item == null || el.Amount <= 0) continue;
                if (InventoryLockHelper.IsLocked(el.EquipmentElement, lockKeys)) continue;
                int reserved = ConsumeReservedQuantity(item, el.Amount, reservationStates);
                int available = Math.Max(0, el.Amount - reserved);
                if (available <= 0) continue;

                bool isLivestock = item.IsAnimal;
                bool isPack = item.IsMountable && item.HorseComponent != null && item.HorseComponent.IsPackAnimal;
                bool isRiding = item.IsMountable && item.HorseComponent != null && !item.HorseComponent.IsPackAnimal;

                bool allowed = false;
                int categoryPriority = 99;

                if (isLivestock && (mode == PostBattleSlaughterMode.Livestock || mode == PostBattleSlaughterMode.LivestockAndPack || mode == PostBattleSlaughterMode.All))
                {
                    allowed = true;
                    categoryPriority = 1;
                }
                else if (isPack && (mode == PostBattleSlaughterMode.LivestockAndPack || mode == PostBattleSlaughterMode.All))
                {
                    allowed = true;
                    categoryPriority = 2;
                }
                else if (isRiding && mode == PostBattleSlaughterMode.All)
                {
                    allowed = processableRiding > 0;
                    categoryPriority = 3;
                }

                if (allowed)
                {
                    candidates.Add(new AnimalSlaughterCandidate(el.EquipmentElement, available, item.Value, categoryPriority));
                }
            }

            var messages = new List<string>();
            var sortedCandidates = candidates
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.Value)
                .ToList();

            foreach (var cand in sortedCandidates)
            {
                if (excess <= 0) break;
                int toSlaughter = Math.Min(excess, cand.Amount);
                if (toSlaughter <= 0)
                {
                    continue;
                }

                if (cand.IsRidingMount)
                {
                    toSlaughter = Math.Min(toSlaughter, processableRiding);
                    if (toSlaughter <= 0)
                    {
                        continue;
                    }
                    processableRiding -= toSlaughter;
                }

                itemRoster.AddToCounts(cand.Item, -toSlaughter);

                int meatCount = cand.Item.HorseComponent.MeatCount;
                int hideCount = cand.Item.HorseComponent.HideCount;
                if (meatCount > 0)
                {
                    itemRoster.AddToCounts(DefaultItems.Meat, meatCount * toSlaughter);
                }
                if (hideCount > 0)
                {
                    itemRoster.AddToCounts(DefaultItems.Hides, hideCount * toSlaughter);
                }

                excess -= toSlaughter;
                messages.Add($"field slaughtered {toSlaughter}x {cand.Item.Name} (yielded {meatCount * toSlaughter}x Meat, {hideCount * toSlaughter}x Hides)");
            }

            return messages;
        }

        private static List<ReservationState> BuildReservationStates(IReadOnlyList<ItemReservation> itemReservations)
        {
            return (itemReservations ?? new List<ItemReservation>())
                .Where(reservation => reservation != null && reservation.Quantity > 0)
                .Select(reservation => new ReservationState(reservation, reservation.Quantity))
                .ToList();
        }

        private static int ConsumeReservedQuantity(ItemObject item, int stackAmount, List<ReservationState> reservations)
        {
            int reserved = 0;
            foreach (var state in reservations)
            {
                int reservableInStack = Math.Max(0, stackAmount - reserved);
                if (reservableInStack <= 0 || state.Remaining <= 0 || !state.Reservation.MatchesItem(item))
                {
                    continue;
                }

                int applied = Math.Min(state.Remaining, reservableInStack);
                reserved += applied;
                state.Remaining -= applied;
            }

            return reserved;
        }

        private static void AddPostBattleSpeedWarnings(PostBattleAutomationResult result, MobileParty party, Settings settings)
        {
            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                out _, out _, out _);
            int partySize = infantry + cavalry;
            int herdSize = pack + livestock + Math.Max(0, riding - infantry);
            
            float effectiveSpeed = party.Speed;
            
            int herdingPenalty = PartyLogisticsPlanner.CalculateHerdingPenaltyPercent(partySize, herdSize);
            int actualHerdingPenalty = PartyLogisticsPlanner.CalculateActualPenaltyPercent(herdingPenalty, effectiveSpeed);
            if (settings.HerdingWarningThresholdPercent > 0 && actualHerdingPenalty >= settings.HerdingWarningThresholdPercent)
            {
                result.AddActivity($"warning: herding still slows the party by about {actualHerdingPenalty}% after cleanup; settings or item locks may be protecting the remaining animals");
            }

            int cargoPenalty = PartyLogisticsPlanner.CalculateOverburdenPenaltyPercent(
                party.TotalWeightCarried,
                party.InventoryCapacity);
            int actualCargoPenalty = PartyLogisticsPlanner.CalculateActualPenaltyPercent(cargoPenalty, effectiveSpeed);
            if (settings.CargoWarningThresholdPercent > 0 && actualCargoPenalty >= settings.CargoWarningThresholdPercent)
            {
                result.AddActivity($"warning: cargo overburden still slows the party by about {actualCargoPenalty}% after cleanup");
            }
        }



        // ----------------------------------------------------
        // ISettlementRecruitmentProvider (Recruitment Filter System)
        // ----------------------------------------------------
        public RecruitmentNotificationMode NotificationMode => 
            Settings.Instance?.RecruitmentNotificationModeSetting ?? RecruitmentNotificationMode.OneByOne;

        public IReadOnlyList<SettlementRecruitmentOrder> GetRecruitmentOrders(SettlementRecruitmentContext context)
        {
            var orders = new List<SettlementRecruitmentOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || context == null) return orders;

            // Stop recruiting at the configured party-size target, plus any garrison donation space.
            int currentSize = context.PartySize;
            int maxRecruitableSize = GetMaxRecruitablePartySize(context.Party, context.Settlement, settings);
            int remainingRecruitSlots = Math.Max(0, maxRecruitableSize - currentSize);
            if (remainingRecruitSlots <= 0) return orders;
            int normalRecruitSlots = Math.Max(0, context.PartySizeLimit - currentSize);

            var candidates = SortRecruitmentCandidates(
                context.Candidates.Where(candidate => IsRecruitmentCandidateAllowed(candidate, context, settings)),
                settings.RecruitHireOrderSetting);

            int selectedCount = 0;
            foreach (var candidate in candidates)
            {
                if (remainingRecruitSlots <= 0) break;
                int amount = Math.Min(candidate.AvailableCount, remainingRecruitSlots);
                if (candidate.Source == SettlementRecruitmentSource.NotableVolunteer)
                {
                    amount = Math.Min(amount, 1);
                }
                if (amount <= 0) continue;

                bool allowOverPartySize = selectedCount >= normalRecruitSlots || selectedCount + amount > normalRecruitSlots;
                orders.Add(new SettlementRecruitmentOrder(candidate, amount, allowOverPartySize));
                selectedCount += amount;
                remainingRecruitSlots -= amount;
            }

            return orders;
        }

        internal static IReadOnlyList<SettlementRecruitmentCandidate> SortRecruitmentCandidates(
            IEnumerable<SettlementRecruitmentCandidate> candidates,
            RecruitHireOrder order)
        {
            switch (order)
            {
                case RecruitHireOrder.BestValue:
                    return candidates
                        .OrderByDescending(candidate => candidate.UnitCost <= 0 ? double.MaxValue : (double)GetCandidateTier(candidate) / candidate.UnitCost)
                        .ThenByDescending(GetCandidateTier)
                        .ThenBy(candidate => candidate.Sequence)
                        .ToList();
                case RecruitHireOrder.CheapestFirst:
                    return candidates
                        .OrderBy(candidate => candidate.UnitCost)
                        .ThenByDescending(GetCandidateTier)
                        .ThenBy(candidate => candidate.Sequence)
                        .ToList();
                case RecruitHireOrder.VolunteersFirst:
                    return candidates
                        .OrderBy(candidate => candidate.Source == SettlementRecruitmentSource.NotableVolunteer ? 0 : 1)
                        .ThenBy(candidate => candidate.Sequence)
                        .ToList();
                case RecruitHireOrder.MercenariesFirst:
                    return candidates
                        .OrderBy(candidate => candidate.Source == SettlementRecruitmentSource.TavernMercenary ? 0 : 1)
                        .ThenBy(candidate => candidate.Sequence)
                        .ToList();
                default:
                    return candidates
                        .OrderByDescending(GetCandidateTier)
                        .ThenBy(candidate => candidate.UnitCost)
                        .ThenBy(candidate => candidate.Sequence)
                        .ToList();
            }
        }

        private static int GetCandidateTier(SettlementRecruitmentCandidate candidate)
        {
            return candidate.Troop?.Tier ?? 0;
        }

        private static bool IsRecruitmentCandidateAllowed(
            SettlementRecruitmentCandidate candidate,
            SettlementRecruitmentContext context,
            Settings settings)
        {
            if (candidate?.Troop == null)
            {
                return false;
            }

            if (candidate.Source == SettlementRecruitmentSource.NotableVolunteer)
            {
                return context.CanRecruitNotables &&
                       settings.AutoRecruitVolunteers &&
                       RecruitmentFilter.MatchTroopFilter(candidate.Troop, settings);
            }

            return context.CanRecruitMercenaries &&
                   settings.MercenaryRecruitSetting != MercenaryRecruitPolicy.None &&
                   RecruitmentFilter.MatchTroopFilter(candidate.Troop, settings);
        }




        // ----------------------------------------------------
        // IGarrisonOrderProvider (Garrison Influence Donation)
        // ----------------------------------------------------
        public List<GarrisonOrder> GetGarrisonOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<GarrisonOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.EnableGarrisonDonation || settlement.Town == null || settlement.Town.GarrisonParty == null)
            {
                return orders;
            }

            // Verify garrison size limit has not been hit
            int garrisonSize = settlement.Town.GarrisonParty.MemberRoster.TotalManCount;
            if (garrisonSize >= settings.MaxGarrisonSize) return orders;

            int partySize = party.MemberRoster.TotalManCount;
            int limit = party.Party.PartySizeLimit;

            // If we are over size limit, donate troops to the garrison Core selected.
            if (partySize > limit)
            {
                int excessCount = partySize - limit;

                // Find non-hero recruits to donate. Prioritize lowest tiers, or matching Garrison minimum tier.
                var candidates = new List<TroopRosterElementInfo>();
                var memberRoster = party.MemberRoster;
                for (int i = 0; i < memberRoster.Count; i++)
                {
                    var el = memberRoster.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero && el.Character.Tier >= settings.MinDonationTier)
                    {
                        candidates.Add(new TroopRosterElementInfo(el.Character, el.Number));
                    }
                }

                // Sort by tier ascending (donate lowest tier recruits first)
                candidates = candidates.OrderBy(c => c.Character.Tier).ToList();

                foreach (var candidate in candidates)
                {
                    if (excessCount <= 0 || garrisonSize >= settings.MaxGarrisonSize) break;
                    int toDonate = Math.Min(Math.Min(excessCount, candidate.Amount), settings.MaxGarrisonSize - garrisonSize);
                    if (toDonate > 0)
                    {
                        orders.Add(new GarrisonOrder(candidate.Character, toDonate));
                        excessCount -= toDonate;
                        garrisonSize += toDonate;
                    }
                }
            }

            return orders;
        }

        private class TroopRosterElementInfo
        {
            public CharacterObject Character { get; }
            public int Amount { get; }
            public TroopRosterElementInfo(CharacterObject ch, int amount)
            {
                Character = ch;
                Amount = amount;
            }
        }

        private class AnimalSlaughterCandidate
        {
            public EquipmentElement EquipmentElement { get; }
            public ItemObject Item { get; } = null!;
            public int Amount { get; }
            public int Value { get; }
            public int Priority { get; }
            public bool IsRidingMount { get; }

            public AnimalSlaughterCandidate(EquipmentElement equipmentElement, int amount, int value, int priority)
            {
                EquipmentElement = equipmentElement;
                Item = equipmentElement.Item;
                Amount = amount;
                Value = value;
                Priority = priority;
                IsRidingMount = Item != null && Item.IsMountable && Item.HorseComponent != null && !Item.HorseComponent.IsPackAnimal;
            }
        }

        private class ReservationState
        {
            public ItemReservation Reservation { get; }
            public int Remaining { get; set; }

            public ReservationState(ItemReservation reservation, int remaining)
            {
                Reservation = reservation;
                Remaining = remaining;
            }
        }

        // ----------------------------------------------------
        // IPrisonerDispositionProvider
        // ----------------------------------------------------
        public IReadOnlyList<PrisonerDispositionOrder> GetPrisonerDispositionOrders(PrisonerDispositionContext context)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled) return new List<PrisonerDispositionOrder>();
            return PrisonerHelper.GetPrisonerDispositionOrders(context, settings);
        }

        private static int GetMaxRecruitablePartySize(MobileParty party, Settlement settlement, Settings settings)
        {
            int partyLimit = party.Party.PartySizeLimit;
            int currentGarrisonSize = settlement.Town?.GarrisonParty?.MemberRoster.TotalManCount ?? 0;
            return RecruitmentCapacityPlanner.GetMaxRecruitablePartySize(
                partyLimit,
                settings.RecruitUpToPartySizePercent,
                settings.EnableGarrisonDonation && settlement.Town?.GarrisonParty != null,
                currentGarrisonSize,
                settings.MaxGarrisonSize);
        }

        private static int GetAvailableGarrisonDonationSpace(Settlement settlement, Settings settings)
        {
            int garrisonSize = settlement.Town?.GarrisonParty?.MemberRoster.TotalManCount ?? 0;
            return RecruitmentCapacityPlanner.GetAvailableGarrisonDonationSpace(
                settings.EnableGarrisonDonation && settlement.Town?.GarrisonParty != null,
                garrisonSize,
                settings.MaxGarrisonSize);
        }
    }
}
