using System;
using System.Collections.Generic;
using System.Linq;
using SettlementAutomationCore;

namespace EquipmentManager
{
    internal sealed class EquipmentMarketCandidateOrder
    {
        public InventoryItemView Candidate { get; }
        public int ExplicitGoldReserve { get; }
        public RequestProfile Profile { get; }
        public float ScoreIncrease { get; }

        public EquipmentMarketCandidateOrder(
            InventoryItemView candidate,
            int explicitGoldReserve,
            RequestProfile profile,
            float scoreIncrease)
        {
            Candidate = candidate;
            ExplicitGoldReserve = explicitGoldReserve;
            Profile = profile;
            ScoreIncrease = scoreIncrease;
        }
    }

    internal sealed class EquipmentMarketRequestGroup
    {
        public AutomationRequest Request { get; }
        public InventoryItemView TopCandidate { get; }
        public int CandidateCount { get; }
        public int ExplicitGoldReserve { get; }
        public RequestProfile Profile { get; }
        public float TopScoreIncrease { get; }

        public EquipmentMarketRequestGroup(
            AutomationRequest request,
            InventoryItemView topCandidate,
            int candidateCount,
            int explicitGoldReserve,
            RequestProfile profile,
            float topScoreIncrease)
        {
            Request = request;
            TopCandidate = topCandidate;
            CandidateCount = candidateCount;
            ExplicitGoldReserve = explicitGoldReserve;
            Profile = profile;
            TopScoreIncrease = topScoreIncrease;
        }
    }

    internal static class EquipmentMarketRequestPlanner
    {
        public static IReadOnlyList<EquipmentMarketRequestGroup> BuildRequestGroups(
            IEnumerable<EquipmentMarketCandidateOrder> candidateOrders,
            string requestorId,
            int purchaseCount)
        {
            var orderedOrders = (candidateOrders ?? Enumerable.Empty<EquipmentMarketCandidateOrder>())
                .Where(o => o.Candidate != null)
                .OrderByDescending(o => o.ScoreIncrease)
                .GroupBy(o => o.Candidate.SnapshotId, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            var groups = new List<EquipmentMarketRequestGroup>();
            foreach (var reserveGroup in orderedOrders
                         .GroupBy(o => new { o.ExplicitGoldReserve, o.Profile })
                         .OrderByDescending(g => g.Max(o => o.ScoreIncrease)))
            {
                var groupOrders = reserveGroup
                    .OrderByDescending(o => o.ScoreIncrease)
                    .ToList();
                if (groupOrders.Count == 0) continue;

                var request = AutomationRequest.ForMarketItems(
                    requestorId,
                    groupOrders.Select(o => o.Candidate),
                    purchaseCount,
                    reserveGroup.Key.Profile,
                    5,
                    reserveGroup.Key.ExplicitGoldReserve);

                groups.Add(new EquipmentMarketRequestGroup(
                    request,
                    groupOrders[0].Candidate,
                    groupOrders.Count,
                    reserveGroup.Key.ExplicitGoldReserve,
                    reserveGroup.Key.Profile,
                    groupOrders[0].ScoreIncrease));
            }

            return groups;
        }
    }
}

