using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    internal static class RequestPolicy
    {
        public static int GetProfileOrder(RequestProfile profile)
        {
            return profile switch
            {
                RequestProfile.Critical => 0,
                RequestProfile.Essential => 1,
                RequestProfile.Routine => 2,
                RequestProfile.Opportunistic => 3,
                RequestProfile.Luxury => 4,
                _ => 99
            };
        }

        public static IReadOnlyList<AutomationRequest> SortRequests(IEnumerable<AutomationRequest> requests)
        {
            return (requests ?? Enumerable.Empty<AutomationRequest>())
                .OrderBy(r => GetProfileOrder(r.Profile))
                .ThenByDescending(r => r.Priority)
                .ToList();
        }

        public static bool CreatesImplicitSellReservation(AutomationRequest request)
        {
            return request.QuantityMode != RequestQuantityMode.PurchaseCount;
        }

        public static int GetGoldReserveForRequest(
            AutomationRequest request,
            int minimumGoldReserve,
            int minDaysExpensesToKeep,
            int dailyWage)
        {
            if (request.BudgetPolicy == BudgetPolicyKind.ExplicitReserve)
            {
                return request.ExplicitGoldReserve;
            }

            return System.Math.Max(minimumGoldReserve, dailyWage * minDaysExpensesToKeep);
        }

        public static bool IsPriceAllowedForRequest(
            AutomationRequest request,
            ItemObject item,
            int price,
            float routinePriceLimitMultiplier,
            float opportunisticPriceLimitMultiplier)
        {
            if (item == null) return false;

            return IsPriceAllowedForProfile(
                request.Profile,
                item.Value,
                price,
                routinePriceLimitMultiplier,
                opportunisticPriceLimitMultiplier);
        }

        public static bool IsPriceAllowedForProfile(
            RequestProfile profile,
            int itemValue,
            int price,
            float routinePriceLimitMultiplier,
            float opportunisticPriceLimitMultiplier)
        {
            float? maxMultiplier = profile switch
            {
                RequestProfile.Routine => routinePriceLimitMultiplier,
                RequestProfile.Opportunistic => opportunisticPriceLimitMultiplier,
                _ => null
            };

            if (maxMultiplier == null || itemValue <= 0)
            {
                return true;
            }

            return price <= itemValue * maxMultiplier.Value;
        }

        public static string DescribeRequest(AutomationRequest request)
        {
            string target = request.QuantityMode == RequestQuantityMode.PurchaseCount
                ? $"{request.MarketCandidates.Count} market candidates"
                : request.TargetId;
            string reserve = request.BudgetPolicy == BudgetPolicyKind.ExplicitReserve
                ? $"explicitReserve={request.ExplicitGoldReserve}"
                : "coreReserve";
            return $"{request.RequestorId} {request.Profile}/{request.Priority} {request.QuantityMode} {request.Type}:{target} qty={request.Quantity} {reserve}";
        }
    }
}

