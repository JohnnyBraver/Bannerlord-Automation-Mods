using System;
using System.Collections.Generic;
using System.Linq;

namespace FiefManager
{
    internal sealed class BuildingProjectCandidate
    {
        public int Index { get; }
        public bool IsDefaultProject { get; }
        public int CurrentLevel { get; }
        public bool IsMilitaryProject { get; }
        public int ConstructionCost { get; }
        public bool HasGovernorBuildSpeedPerk { get; }

        public BuildingProjectCandidate(
            int index,
            bool isDefaultProject,
            int currentLevel,
            bool isMilitaryProject,
            int constructionCost = 0,
            bool hasGovernorBuildSpeedPerk = false)
        {
            Index = index;
            IsDefaultProject = isDefaultProject;
            CurrentLevel = currentLevel;
            IsMilitaryProject = isMilitaryProject;
            ConstructionCost = Math.Max(0, constructionCost);
            HasGovernorBuildSpeedPerk = hasGovernorBuildSpeedPerk;
        }
    }

    internal sealed class BoostDepositPlan
    {
        public int AmountToDeposit { get; }
        public int TargetReserve { get; }
        public int DailyCost { get; }

        public BoostDepositPlan(int amountToDeposit, int targetReserve, int dailyCost)
        {
            AmountToDeposit = amountToDeposit;
            TargetReserve = targetReserve;
            DailyCost = dailyCost;
        }
    }

    internal static class FiefAutomationPlanner
    {
        public static bool HasUpgradeableBuildingProjects(IEnumerable<BuildingProjectCandidate> buildingCandidates)
        {
            return (buildingCandidates ?? Enumerable.Empty<BuildingProjectCandidate>())
                .Any(IsUpgradeableProject);
        }

        public static int? ChooseNextBuildingIndex(
            IEnumerable<BuildingProjectCandidate> buildingCandidates,
            BuildingPriorityCategory priority,
            BuildingUpgradeApproach upgradeApproach = BuildingUpgradeApproach.DefaultOrder)
        {
            var selected = ChooseBuildingQueueIndexes(
                buildingCandidates,
                priority,
                upgradeApproach,
                alreadyQueuedIndexes: null,
                maxQueuedProjects: 1);

            return selected.Count > 0 ? selected[0] : (int?)null;
        }

        public static IReadOnlyList<int> ChooseBuildingQueueIndexes(
            IEnumerable<BuildingProjectCandidate> buildingCandidates,
            BuildingPriorityCategory priority,
            BuildingUpgradeApproach upgradeApproach,
            IEnumerable<int>? alreadyQueuedIndexes,
            int maxQueuedProjects)
        {
            var queuedIndexes = new HashSet<int>(alreadyQueuedIndexes ?? Enumerable.Empty<int>());
            int projectLimit = Math.Max(1, maxQueuedProjects);

            if (queuedIndexes.Count >= projectLimit)
            {
                return Array.Empty<int>();
            }

            var candidates = ApplyPriority(
                    buildingCandidates ?? Enumerable.Empty<BuildingProjectCandidate>(),
                    priority,
                    upgradeApproach)
                .Where(IsUpgradeableProject)
                .Where(b => !queuedIndexes.Contains(b.Index));

            var selected = new List<int>();
            foreach (var candidate in candidates)
            {
                if (queuedIndexes.Count + selected.Count >= projectLimit)
                {
                    break;
                }

                selected.Add(candidate.Index);
            }

            return selected;
        }

        private static IEnumerable<BuildingProjectCandidate> ApplyPriority(
            IEnumerable<BuildingProjectCandidate> candidates,
            BuildingPriorityCategory priority,
            BuildingUpgradeApproach upgradeApproach)
        {
            var rankedCandidates = candidates.Select((candidate, originalIndex) => new RankedCandidate(candidate, originalIndex));
            IOrderedEnumerable<RankedCandidate> ordered = rankedCandidates
                .OrderByDescending(b => b.Candidate.HasGovernorBuildSpeedPerk);

            if (priority == BuildingPriorityCategory.MilitaryFirst)
            {
                ordered = ordered.ThenByDescending(b => b.Candidate.HasGovernorBuildSpeedPerk || b.Candidate.IsMilitaryProject);
            }
            else if (priority == BuildingPriorityCategory.EconomicFirst)
            {
                ordered = ordered.ThenBy(b => !b.Candidate.HasGovernorBuildSpeedPerk && b.Candidate.IsMilitaryProject);
            }

            if (upgradeApproach == BuildingUpgradeApproach.LowestLevelFirst)
            {
                ordered = ordered.ThenBy(b => b.Candidate.CurrentLevel);
            }
            else if (upgradeApproach == BuildingUpgradeApproach.CheapestFirst)
            {
                ordered = ordered.ThenBy(b => b.Candidate.ConstructionCost);
            }
            else if (upgradeApproach == BuildingUpgradeApproach.MostExpensiveFirst)
            {
                ordered = ordered.ThenByDescending(b => b.Candidate.ConstructionCost);
            }

            return ordered
                .ThenBy(b => b.OriginalIndex)
                .Select(b => b.Candidate);
        }

        private readonly struct RankedCandidate
        {
            public BuildingProjectCandidate Candidate { get; }
            public int OriginalIndex { get; }

            public RankedCandidate(BuildingProjectCandidate candidate, int originalIndex)
            {
                Candidate = candidate;
                OriginalIndex = originalIndex;
            }
        }

        private static bool IsUpgradeableProject(BuildingProjectCandidate candidate)
        {
            return !candidate.IsDefaultProject && candidate.CurrentLevel < 3;
        }

        public static BoostDepositPlan CalculateBoostDeposit(
            bool isTown,
            int daysOfFunding,
            int currentReserve,
            int maxReserveLimitTown,
            int maxReserveLimitCastle,
            int minPlayerGoldReserve,
            int playerGold,
            bool hasUpgradeableBuildingProjects,
            bool requireUpgradeableProjects)
        {
            if (requireUpgradeableProjects && !hasUpgradeableBuildingProjects)
            {
                int blockedDailyCost = isTown ? 500 : 250;
                return new BoostDepositPlan(0, 0, blockedDailyCost);
            }

            int dailyCost = isTown ? 500 : 250;
            int maxLimit = isTown ? maxReserveLimitTown : maxReserveLimitCastle;
            int targetReserve = Math.Min(Math.Max(1, daysOfFunding) * dailyCost, maxLimit);
            int needed = Math.Max(0, targetReserve - currentReserve);
            int maxAllowedToSpend = Math.Max(0, playerGold - minPlayerGoldReserve);
            return new BoostDepositPlan(Math.Min(needed, maxAllowedToSpend), targetReserve, dailyCost);
        }
    }
}

