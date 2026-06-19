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
        public void ChooseNextBuildingIndex_ReturnsNullWhenNoUpgradeProjectsRemain()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: true, currentLevel: 0, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 3, isMilitaryProject: false)
            };

            Assert.Null(FiefAutomationPlanner.ChooseNextBuildingIndex(candidates, BuildingPriorityCategory.Balanced));
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_ProjectLimitStopsSelection()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.Balanced,
                BuildingUpgradeApproach.DefaultOrder,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 2);

            Assert.Equal(new[] { 0, 1 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_CountsExistingQueueAgainstLimits()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.Balanced,
                BuildingUpgradeApproach.DefaultOrder,
                alreadyQueuedIndexes: new[] { 0 },
                maxQueuedProjects: 2);

            Assert.Equal(new[] { 1 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_CanPreferLowestLevelProjects()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 2, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 0, isMilitaryProject: false),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.Balanced,
                BuildingUpgradeApproach.LowestLevelFirst,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 3);

            Assert.Equal(new[] { 1, 2, 0 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_CategoryPriorityThenLowestLevel()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 0, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 2, isMilitaryProject: true),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true),
                new BuildingProjectCandidate(3, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.MilitaryFirst,
                BuildingUpgradeApproach.LowestLevelFirst,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 4);

            Assert.Equal(new[] { 2, 1, 0, 3 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_CanPreferCheapestProjects()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 900),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 300),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 600)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.Balanced,
                BuildingUpgradeApproach.CheapestFirst,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 3);

            Assert.Equal(new[] { 1, 2, 0 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_CanPreferMostExpensiveProjects()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 900),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 300),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 600)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.Balanced,
                BuildingUpgradeApproach.MostExpensiveFirst,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 3);

            Assert.Equal(new[] { 0, 2, 1 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_CategoryPriorityThenCost()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 100),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true, constructionCost: 900),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true, constructionCost: 300),
                new BuildingProjectCandidate(3, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 700)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.MilitaryFirst,
                BuildingUpgradeApproach.CheapestFirst,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 4);

            Assert.Equal(new[] { 2, 1, 0, 3 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_GovernorPerkCanBypassSelectedCategory()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 100),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true, constructionCost: 900, hasGovernorBuildSpeedPerk: true)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.EconomicFirst,
                BuildingUpgradeApproach.DefaultOrder,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 2);

            Assert.Equal(new[] { 1, 0 }, selected);
        }

        [Fact]
        public void ChooseBuildingQueueIndexes_GovernorPerkGroupUsesSelectedSorting()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: false, currentLevel: 1, isMilitaryProject: true, constructionCost: 900, hasGovernorBuildSpeedPerk: true),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 100, hasGovernorBuildSpeedPerk: true),
                new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 1, isMilitaryProject: false, constructionCost: 50)
            };

            var selected = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                candidates,
                BuildingPriorityCategory.MilitaryFirst,
                BuildingUpgradeApproach.CheapestFirst,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 3);

            Assert.Equal(new[] { 1, 0, 2 }, selected);
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
                playerGold: 13000,
                hasUpgradeableBuildingProjects: true,
                requireUpgradeableProjects: true);

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
                playerGold: 10000,
                hasUpgradeableBuildingProjects: true,
                requireUpgradeableProjects: true);

            Assert.Equal(0, plan.AmountToDeposit);
            Assert.Equal(2500, plan.TargetReserve);
            Assert.Equal(250, plan.DailyCost);
        }

        [Fact]
        public void HasUpgradeableBuildingProjects_IgnoresDefaultAndMaxedProjects()
        {
            var candidates = new List<BuildingProjectCandidate>
            {
                new BuildingProjectCandidate(0, isDefaultProject: true, currentLevel: 0, isMilitaryProject: false),
                new BuildingProjectCandidate(1, isDefaultProject: false, currentLevel: 3, isMilitaryProject: false)
            };

            Assert.False(FiefAutomationPlanner.HasUpgradeableBuildingProjects(candidates));

            candidates.Add(new BuildingProjectCandidate(2, isDefaultProject: false, currentLevel: 2, isMilitaryProject: true));

            Assert.True(FiefAutomationPlanner.HasUpgradeableBuildingProjects(candidates));
        }

        [Fact]
        public void CalculateBoostDeposit_CanRequireUpgradeableProjects()
        {
            var blocked = FiefAutomationPlanner.CalculateBoostDeposit(
                isTown: true,
                daysOfFunding: 10,
                currentReserve: 0,
                maxReserveLimitTown: 100000,
                maxReserveLimitCastle: 50000,
                minPlayerGoldReserve: 10000,
                playerGold: 20000,
                hasUpgradeableBuildingProjects: false,
                requireUpgradeableProjects: true);

            var allowed = FiefAutomationPlanner.CalculateBoostDeposit(
                isTown: true,
                daysOfFunding: 10,
                currentReserve: 0,
                maxReserveLimitTown: 100000,
                maxReserveLimitCastle: 50000,
                minPlayerGoldReserve: 10000,
                playerGold: 20000,
                hasUpgradeableBuildingProjects: false,
                requireUpgradeableProjects: false);

            Assert.Equal(0, blocked.AmountToDeposit);
            Assert.Equal(5000, allowed.AmountToDeposit);
        }
    }
}

