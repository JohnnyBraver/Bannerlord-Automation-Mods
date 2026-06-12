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

        public void ProcessFiefAutomation(MobileParty party, Settlement settlement)
        {
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
                        var candidates = town.Buildings.Where(b => b != null && !b.IsCurrentlyDefault && b.CurrentLevel < 3);

                        var priority = settings.Priority;
                        if (priority == BuildingPriorityCategory.MilitaryFirst)
                        {
                            candidates = candidates.OrderByDescending(b => b.BuildingType.IsMilitaryProject);
                        }
                        else if (priority == BuildingPriorityCategory.EconomicFirst)
                        {
                            candidates = candidates.OrderBy(b => b.BuildingType.IsMilitaryProject);
                        }

                        var nextBuilding = candidates.FirstOrDefault();
                        if (nextBuilding != null)
                        {
                            town.BuildingsInProgress.Enqueue(nextBuilding);
                            string priorityLabel = priority == BuildingPriorityCategory.MilitaryFirst ? " [Military]" : (priority == BuildingPriorityCategory.EconomicFirst ? " [Economic]" : "");
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
                    int dailyCost = settlement.IsCastle ? 250 : 500;
                    int maxLimit = settlement.IsTown ? settings.MaxReserveLimitTown : settings.MaxReserveLimitCastle;
                    int targetReserve = Math.Min(settings.DaysOfFunding * dailyCost, maxLimit);
                    int currentReserve = town.BoostBuildingProcess;
                    int needed = targetReserve - currentReserve;
                    if (needed > 0)
                    {
                        int playerGold = Hero.MainHero.Gold;
                        int maxAllowedToSpend = playerGold - settings.MinPlayerGoldReserve;
                        if (maxAllowedToSpend > 0)
                        {
                            int toDeposit = Math.Min(needed, maxAllowedToSpend);

                            if (toDeposit > 0)
                            {
                                Hero.MainHero.Gold -= toDeposit;
                                town.BoostBuildingProcess += toDeposit;

                                string msg = $"Deposited {toDeposit} denars to project boost reserve at {settlement.Name} (Reserve: {town.BoostBuildingProcess}/{targetReserve}, Daily Cost: {dailyCost}).";
                                InformationManager.DisplayMessage(new InformationMessage($"[Automation] {msg}"));
                                SettlementAutomationCore.Helpers.Logger.WriteLog("FiefManager", msg);
                            }
                        }
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
