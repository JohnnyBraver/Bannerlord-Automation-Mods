using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using SettlementAutomationCore;
using SettlementAutomationCore.Helpers;

namespace FiefManager
{
    public class FiefManagerProvider : IFiefAutomationProvider
    {
        public string ProviderName => "FiefManager";

        public IReadOnlyList<FiefAutomationOrder> GetFiefAutomationOrders(MobileParty party, Settlement settlement, bool isSurplusPhase)
        {
            var orders = new List<FiefAutomationOrder>();

            // FiefManager fief tasks (build queue, boost deposit) run in the surplus phase,
            // after trade has completed and the party's gold is at its post-trade state.
            if (!isSurplusPhase) return orders;

            if (settlement == null || party == null || Hero.MainHero == null) return orders;

            // Only automate fiefs owned by the player's clan
            if (settlement.OwnerClan != Clan.PlayerClan) return orders;

            var town = settlement.Town;
            if (town == null) return orders;

            var settings = Settings.Instance;
            if (settings == null) return orders;

            var buildings = town.Buildings.ToList();
            var buildingCandidates = buildings
                .Select((b, index) => new BuildingProjectCandidate(
                    index,
                    b == null || b.IsCurrentlyDefault,
                    b?.CurrentLevel ?? 3,
                    b?.BuildingType?.IsMilitaryProject ?? false,
                    GetConstructionCost(b),
                    HasGovernorBuildSpeedPerk(town, b)))
                .ToList();
            bool hasUpgradeableBuildingProjects = FiefAutomationPlanner.HasUpgradeableBuildingProjects(buildingCandidates);

            // 1. Auto Set Build Queue
            if (settings.AutoSetBuildQueue)
            {
                try
                 {
                    if (town.BuildingsInProgress != null)
                    {
                        var queuedBuildings = town.BuildingsInProgress.ToList();
                        var queuedIndexes = queuedBuildings
                            .Select(b => buildings.IndexOf(b))
                            .Where(index => index >= 0)
                            .ToList();
                        var selectedBuildingIndexes = FiefAutomationPlanner.ChooseBuildingQueueIndexes(
                            buildingCandidates,
                            settings.Priority,
                            settings.UpgradeApproach,
                            queuedIndexes,
                            settings.MaxQueuedBuildProjects);
                        var selectedBuildings = new List<Building>();
                        var queuedNames = new List<string>();
                        foreach (int buildingIndex in selectedBuildingIndexes)
                        {
                            var nextBuilding = buildingIndex >= 0 && buildingIndex < buildings.Count ? buildings[buildingIndex] : null;
                            if (nextBuilding == null)
                            {
                                continue;
                            }

                            string projectName = nextBuilding.BuildingType?.Name?.ToString() ?? "Unknown Project";
                            selectedBuildings.Add(nextBuilding);
                            queuedNames.Add(projectName);
                        }

                        if (selectedBuildings.Count > 0)
                        {
                            string priorityLabel = settings.Priority == BuildingPriorityCategory.MilitaryFirst ? " [Military]" : (settings.Priority == BuildingPriorityCategory.EconomicFirst ? " [Economic]" : "");
                            string projectLabel = queuedNames.Count == 1 ? $"'{queuedNames[0]}'" : string.Join(", ", queuedNames.Select(name => $"'{name}'"));
                            string msg = $"Auto-queued {queuedNames.Count} project(s) {projectLabel}{priorityLabel} at {settlement.Name}.";
                            orders.Add(FiefAutomationOrder.QueueBuildings(selectedBuildings, msg));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("FiefManager", $"Error auto-setting build queue at {settlement.Name}: {ex}");
                }
            }

            // 2. Auto Deposit Project Boost Gold
            if (settings.AutoDepositProjectBoost)
            {
                try
                {
                    int currentReserve = town.BoostBuildingProcess;
                    int playerGold = Hero.MainHero.Gold;
                    var depositPlan = FiefAutomationPlanner.CalculateBoostDeposit(
                        settlement.IsTown,
                        settings.DaysOfFunding,
                        currentReserve,
                        settings.MaxReserveLimitTown,
                        settings.MaxReserveLimitCastle,
                        settings.MinPlayerGoldReserve,
                        playerGold,
                        hasUpgradeableBuildingProjects,
                        settings.OnlyDepositWithUpgradeableProjects);
                    if (depositPlan.AmountToDeposit > 0)
                    {
                        int projectedReserve = currentReserve + depositPlan.AmountToDeposit;
                        string msg = $"Deposited {depositPlan.AmountToDeposit} denars to project boost reserve at {settlement.Name} (Reserve: {projectedReserve}/{depositPlan.TargetReserve}, Daily Cost: {depositPlan.DailyCost}).";
                        orders.Add(FiefAutomationOrder.DepositBoostGold(depositPlan.AmountToDeposit, msg));
                    }
                }
                catch (Exception ex)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("FiefManager", $"Error auto-depositing boost gold at {settlement.Name}: {ex}");
                }
            }

            return orders;
        }

        private static int GetConstructionCost(Building? building)
        {
            if (building == null)
            {
                return 0;
            }

            try
            {
                return Math.Max(0, building.GetConstructionCost());
            }
            catch
            {
                return 0;
            }
        }

        private static bool HasGovernorBuildSpeedPerk(Town town, Building? building)
        {
            if (town?.Governor == null || building?.BuildingType == null)
            {
                return false;
            }

            var governor = town.Governor;
            var buildingType = building.BuildingType;

            if (buildingType.IsMilitaryProject && governor.GetPerkValue(DefaultPerks.TwoHanded.Confidence))
            {
                return true;
            }

            if (buildingType == DefaultBuildingTypes.SettlementMarketplace &&
                governor.GetPerkValue(DefaultPerks.Trade.SelfMadeMan))
            {
                return true;
            }

            if ((buildingType == DefaultBuildingTypes.SettlementFortifications ||
                 buildingType == DefaultBuildingTypes.CastleBarracks ||
                 buildingType == DefaultBuildingTypes.SettlementBarracks) &&
                governor.GetPerkValue(DefaultPerks.Engineering.Stonecutters))
            {
                return true;
            }

            return false;
        }
    }
}
