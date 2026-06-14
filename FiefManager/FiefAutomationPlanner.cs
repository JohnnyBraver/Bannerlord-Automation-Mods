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

        public BuildingProjectCandidate(int index, bool isDefaultProject, int currentLevel, bool isMilitaryProject)
        {
            Index = index;
            IsDefaultProject = isDefaultProject;
            CurrentLevel = currentLevel;
            IsMilitaryProject = isMilitaryProject;
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
        public static int? ChooseNextBuildingIndex(
            IEnumerable<BuildingProjectCandidate> buildingCandidates,
            BuildingPriorityCategory priority)
        {
            var candidates = (buildingCandidates ?? Enumerable.Empty<BuildingProjectCandidate>())
                .Where(b => !b.IsDefaultProject && b.CurrentLevel < 3);

            if (priority == BuildingPriorityCategory.MilitaryFirst)
            {
                candidates = candidates.OrderByDescending(b => b.IsMilitaryProject);
            }
            else if (priority == BuildingPriorityCategory.EconomicFirst)
            {
                candidates = candidates.OrderBy(b => b.IsMilitaryProject);
            }

            return candidates.FirstOrDefault()?.Index;
        }

        public static BoostDepositPlan CalculateBoostDeposit(
            bool isTown,
            int daysOfFunding,
            int currentReserve,
            int maxReserveLimitTown,
            int maxReserveLimitCastle,
            int minPlayerGoldReserve,
            int playerGold)
        {
            int dailyCost = isTown ? 500 : 250;
            int maxLimit = isTown ? maxReserveLimitTown : maxReserveLimitCastle;
            int targetReserve = Math.Min(Math.Max(1, daysOfFunding) * dailyCost, maxLimit);
            int needed = Math.Max(0, targetReserve - currentReserve);
            int maxAllowedToSpend = Math.Max(0, playerGold - minPlayerGoldReserve);
            return new BoostDepositPlan(Math.Min(needed, maxAllowedToSpend), targetReserve, dailyCost);
        }
    }
}

