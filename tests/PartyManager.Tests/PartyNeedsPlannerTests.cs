using System.Linq;
using PartyManager;
using SettlementAutomationCore;
using Xunit;

namespace PartyManager.Tests
{
    public class PartyNeedsPlannerTests
    {
        [Fact]
        public void BuildRequests_LayersCriticalVarietyAndRoutineFood()
        {
            var options = DefaultOptions();
            var snapshot = new PartyNeedsSnapshot(40, 10, 10, new[] { "dates", "grain", "olives", "fish" });

            var requests = PartyNeedsPlanner.BuildRequests(snapshot, options).ToList();

            Assert.Equal(6, requests.Count);
            Assert.Equal(RequestType.ItemCategory, requests[0].Type);
            Assert.Equal("Food", requests[0].TargetId);
            Assert.Equal(RequestProfile.Critical, requests[0].Profile);
            Assert.Equal(9, requests[0].Priority);
            Assert.Equal(4, requests[0].Quantity);

            var variety = requests.Skip(1).Take(4).ToList();
            Assert.All(variety, request => Assert.Equal(RequestType.SpecificItem, request.Type));
            Assert.Equal(new[] { "dates", "fish", "grain", "olives" }, variety.Select(r => r.TargetId).ToArray());
            Assert.All(variety, request => Assert.Equal(5, request.Quantity));
            Assert.All(variety, request => Assert.Equal(RequestProfile.Essential, request.Profile));

            var routine = requests.Last();
            Assert.Equal(RequestType.ItemCategory, routine.Type);
            Assert.Equal("Food", routine.TargetId);
            Assert.Equal(20, routine.Quantity);
            Assert.Equal(RequestProfile.Routine, routine.Profile);
        }

        [Fact]
        public void BuildRequests_StillAddsRoutineFoodWhenVarietyItemsAreMissing()
        {
            var snapshot = new PartyNeedsSnapshot(40, 10, 10, Enumerable.Empty<string>());

            var requests = PartyNeedsPlanner.BuildRequests(snapshot, DefaultOptions()).ToList();

            Assert.Equal(2, requests.Count);
            Assert.Equal(RequestProfile.Critical, requests[0].Profile);
            Assert.Equal(RequestProfile.Routine, requests[1].Profile);
            Assert.Equal("Food", requests[1].TargetId);
        }

        [Theory]
        [InlineData(10, 0, 9)]
        [InlineData(10, 5, 5)]
        [InlineData(10, 9, 2)]
        [InlineData(10, 10, 1)]
        public void CalculateMountPriority_ScalesWithMissingRidingMounts(int infantry, int riding, int expected)
        {
            Assert.Equal(expected, PartyNeedsPlanner.CalculateMountPriority(infantry, riding));
        }

        [Fact]
        public void BuildRequests_AddsMountRequestOnlyWhenInfantryNeedRidingMounts()
        {
            var snapshot = new PartyNeedsSnapshot(30, 12, 6, Enumerable.Empty<string>());
            var options = DefaultOptions();
            options.AutoBuyFood = false;

            var request = PartyNeedsPlanner.BuildRequests(snapshot, options).Single();

            Assert.Equal(RequestType.ItemCategory, request.Type);
            Assert.Equal("Horse", request.TargetId);
            Assert.Equal(12, request.Quantity);
            Assert.Equal(RequestProfile.Routine, request.Profile);
            Assert.Equal(5, request.Priority);
        }

        private static PartyNeedsOptions DefaultOptions()
        {
            return new PartyNeedsOptions
            {
                AutoBuyFood = true,
                CriticalFoodDays = 2,
                PartyFoodDaysToKeep = 10,
                MinPartySizeForVariety = 20,
                AutoBuyMounts = true,
                CriticalFoodProfile = RequestProfile.Critical,
                FoodVarietyProfile = RequestProfile.Essential,
                FoodBufferProfile = RequestProfile.Routine,
                RidingMountProfile = RequestProfile.Routine
            };
        }
    }
}

