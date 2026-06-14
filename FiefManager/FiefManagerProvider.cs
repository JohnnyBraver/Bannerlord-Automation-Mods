using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using SettlementAutomationCore;
using SettlementAutomationCore.Helpers;

namespace FiefManager
{
    public class FiefManagerProvider : IFiefAutomationProvider
    {
        public string ProviderName => "FiefManager";

        public void ProcessFiefAutomation(MobileParty party, Settlement settlement, bool isSurplusPhase)
        {
            // FiefManager fief tasks (build queue, boost deposit) run in the surplus phase,
            // after trade has completed and the party's gold is at its post-trade state.
            if (!isSurplusPhase) return;

            if (settlement == null || party == null || Hero.MainHero == null) return;

            // Only automate fiefs owned by the player's clan
            if (settlement.OwnerClan != Clan.PlayerClan) return;

            var town = settlement.Town;
            if (town == null) return;

            var settings = Settings.Instance;
            if (settings == null) return;

            // 1. Auto Set Build Queue
            if (settings.AutoSetBuildQueue)
            {
                try
                 {
                    if (town.BuildingsInProgress != null && town.BuildingsInProgress.Count == 0)
                    {
                        var buildings = town.Buildings.ToList();
                        var nextBuildingIndex = FiefAutomationPlanner.ChooseNextBuildingIndex(
                            buildings.Select((b, index) => new BuildingProjectCandidate(
                                index,
                                b == null || b.IsCurrentlyDefault,
                                b?.CurrentLevel ?? 3,
                                b?.BuildingType?.IsMilitaryProject ?? false)),
                            settings.Priority);
                        var nextBuilding = nextBuildingIndex.HasValue ? buildings[nextBuildingIndex.Value] : null;
                        if (nextBuilding != null)
                        {
                            town.BuildingsInProgress.Enqueue(nextBuilding);
                            string priorityLabel = settings.Priority == BuildingPriorityCategory.MilitaryFirst ? " [Military]" : (settings.Priority == BuildingPriorityCategory.EconomicFirst ? " [Economic]" : "");
                            string msg = $"Auto-queued project '{nextBuilding.BuildingType.Name}'{priorityLabel} at {settlement.Name}.";
                            InformationManager.DisplayMessage(new InformationMessage($"[Automation] {msg}"));
                            SettlementAutomationCore.Helpers.Logger.WriteLog("FiefManager", msg);
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
                        playerGold);
                    if (depositPlan.AmountToDeposit > 0)
                    {
                        Hero.MainHero.Gold -= depositPlan.AmountToDeposit;
                        town.BoostBuildingProcess += depositPlan.AmountToDeposit;

                        string msg = $"Deposited {depositPlan.AmountToDeposit} denars to project boost reserve at {settlement.Name} (Reserve: {town.BoostBuildingProcess}/{depositPlan.TargetReserve}, Daily Cost: {depositPlan.DailyCost}).";
                        InformationManager.DisplayMessage(new InformationMessage($"[Automation] {msg}"));
                        SettlementAutomationCore.Helpers.Logger.WriteLog("FiefManager", msg);
                    }
                }
                catch (Exception ex)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("FiefManager", $"Error auto-depositing boost gold at {settlement.Name}: {ex}");
                }
            }
        }
    }
}
