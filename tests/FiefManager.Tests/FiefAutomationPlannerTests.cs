using System.Collections.Generic;
using FiefManager;
using Xunit;

namespace FiefManager.Tests
{
    public class FiefAutomationPlannerTests
    {
        [Fact]
        public void ChooseNextBuildingIndex_BalancedKeepsDefaultOrder()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: true, currentLevel: 0, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 3, isMilitaryProject: false),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false),
                new BuildingProjectCandidate(3, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true)
            };

            Assert.Equal(2, FiefAutomationPlanner.ChooseNextBuildingIndex(candidates, BuildingPriorityCategory.Balanced));
        }

        [Fact]
        public void ChooseNextBuildingIndex_CanPreferMilitaryOrEconomicProjects()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true)
            };

            Assert.Equal(1, FiefAutomationPlanner.ChooseNextBuildingIndex(candidates, BuildingPriorityCategory.MilitaryFirst));
            Assert.Equal(0, FiefAutomationPlanner.ChooseNextBuildingIndex(candidates, BuildingPriorityCategory.EconomicFirst));
        }

        [Fact]
        public void CalculateBoostDeposit_RespectsTargetLimitAndPlayerReserve()
        {
            var plan = FiefAutomationPlanner.CalculateBoostDeposit(
                isTown: true,
                daysOfFunding: 10,
                currentReserve: 1500,
                maxReserveLimitTown: 4000,
                maxReserveLimitCastle: 50000,
                minPlayerGoldReserve: 10000,
                playerGold: 13000);

            Assert.Equal(2500, plan.AmountToDeposit);
            Assert.Equal(4000, plan.TargetReserve);
            Assert.Equal(500, plan.DailyCost);
        }

        [Fact]
        public void CalculateBoostDeposit_ReturnsZeroWhenPlayerReserveWouldBeBreached()
        {
            var plan = FiefAutomationPlanner.CalculateBoostDeposit(
                isTown: false,
                daysOfFunding: 10,
                currentReserve: 0,
                maxReserveLimitTown: 100000,
                maxReserveLimitCastle: 50000,
                minPlayerGoldReserve: 10000,
                playerGold: 10000);

            Assert.Equal(0, plan.AmountToDeposit);
            Assert.Equal(2500, plan.TargetReserve);
            Assert.Equal(250, plan.DailyCost);
        }
    }
}

