using System;
using System.Collections.Generic;
using System.Linq;
using SettlementAutomationCore;

namespace PartyManager
{
    internal sealed class PartyNeedsOptions
    {
        public bool AutoBuyFood { get; set; }
        public int CriticalFoodDays { get; set; }
        public int PartyFoodDaysToKeep { get; set; }
        public int MinPartySizeForVariety { get; set; }
        public bool AutoBuyMounts { get; set; }
        public RequestProfile CriticalFoodProfile { get; set; }
        public RequestProfile FoodVarietyProfile { get; set; }
        public RequestProfile FoodBufferProfile { get; set; }
        public RequestProfile RidingMountProfile { get; set; }

        public static PartyNeedsOptions FromSettings(Settings settings)
        {
            return new PartyNeedsOptions
            {
                AutoBuyFood = settings.AutoBuyFood,
                CriticalFoodDays = settings.CriticalFoodDays,
                PartyFoodDaysToKeep = settings.PartyFoodDaysToKeep,
                MinPartySizeForVariety = settings.MinPartySizeForVariety,
                AutoBuyMounts = settings.AutoBuyMounts,
                CriticalFoodProfile = settings.CriticalFoodRequestProfile,
                FoodVarietyProfile = settings.FoodVarietyRequestProfile,
                FoodBufferProfile = settings.FoodBufferRequestProfile,
                RidingMountProfile = settings.RidingMountRequestProfile
            };
        }
    }

    internal sealed class PartyNeedsSnapshot
    {
        public int PartySize { get; }
        public int Infantry { get; }
        public int RidingMounts { get; }
        public IReadOnlyList<string> KnownFoodItemIds { get; }

        public PartyNeedsSnapshot(int partySize, int infantry, int ridingMounts, IEnumerable<string> knownFoodItemIds)
        {
            PartySize = partySize;
            Infantry = infantry;
            RidingMounts = ridingMounts;
            KnownFoodItemIds = (knownFoodItemIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }
    }

    internal static class PartyNeedsPlanner
    {
        public static IReadOnlyList<AutomationRequest> BuildRequests(PartyNeedsSnapshot snapshot, PartyNeedsOptions options)
        {
            var requests = new List<AutomationRequest>();
            if (snapshot == null || options == null || snapshot.PartySize <= 0)
            {
                return requests;
            }

            if (options.AutoBuyFood)
            {
                int criticalTarget = CalculateFoodUnitsForDays(snapshot.PartySize, options.CriticalFoodDays);
                requests.Add(AutomationRequest.ForInventoryTarget(
                    "PartyManager",
                    RequestType.ItemCategory,
                    "Food",
                    criticalTarget,
                    options.CriticalFoodProfile,
                    9));

                int totalFoodTarget = CalculateFoodUnitsForDays(snapshot.PartySize, options.PartyFoodDaysToKeep);
                if (snapshot.PartySize >= options.MinPartySizeForVariety && snapshot.KnownFoodItemIds.Count > 0)
                {
                    int perFoodTarget = Math.Max(1, (int)Math.Ceiling(totalFoodTarget / (double)snapshot.KnownFoodItemIds.Count));
                    foreach (var foodItemId in snapshot.KnownFoodItemIds)
                    {
                        requests.Add(AutomationRequest.ForInventoryTarget(
                            "PartyManager",
                            RequestType.SpecificItem,
                            foodItemId,
                            perFoodTarget,
                            options.FoodVarietyProfile,
                            5));
                    }
                }

                requests.Add(AutomationRequest.ForInventoryTarget(
                    "PartyManager",
                    RequestType.ItemCategory,
                    "Food",
                    totalFoodTarget,
                    options.FoodBufferProfile,
                    5));
            }

            if (options.AutoBuyMounts && snapshot.Infantry > 0)
            {
                int missing = snapshot.Infantry - snapshot.RidingMounts;
                if (missing > 0)
                {
                    requests.Add(AutomationRequest.ForInventoryTarget(
                        "PartyManager",
                        RequestType.ItemCategory,
                        "Horse",
                        snapshot.Infantry,
                        options.RidingMountProfile,
                        CalculateMountPriority(snapshot.Infantry, snapshot.RidingMounts)));
                }
            }

            return requests;
        }

        public static int CalculateFoodUnitsForDays(int partySize, int days)
        {
            return Math.Max(1, (int)Math.Ceiling(partySize * Math.Max(1, days) / 20.0f));
        }

        public static int CalculateMountPriority(int infantry, int ridingMounts)
        {
            if (infantry <= 0) return 1;

            int missing = Math.Max(0, infantry - ridingMounts);
            if (missing <= 0) return 1;

            return Math.Max(1, Math.Min(9, (int)Math.Round(1 + 8 * ((float)missing / infantry))));
        }
    }
}

