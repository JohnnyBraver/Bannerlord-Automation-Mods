using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Naval;

namespace PartyManager.Helpers
{
    internal static class BoatHelper
    {
        public static void AutoBuyBoats(Settlement settlement, Settings settings)
        {
            if (settlement == null || !settlement.IsTown) return;
            if (settings == null || !settings.AutoBuyBoats) return;

            var limitModel = Campaign.Current?.Models?.PartyShipLimitModel;
            var costModel = Campaign.Current?.Models?.ShipCostModel;
            if (limitModel == null || costModel == null) return; // Safely exit if DLC types/models are null or uninitialized

            var town = settlement.Town;
            if (town == null) return;

            var availableShips = town.AvailableShips;
            if (availableShips == null || availableShips.Count == 0) return;

            var mainParty = MobileParty.MainParty;
            if (mainParty == null) return;

            int idealShips = limitModel.GetIdealShipNumber(mainParty);
            int currentShips = mainParty.Party?.Ships?.Count ?? 0;
            int missingShips = idealShips - currentShips;

            if (missingShips <= 0) return;

            // Purchase ships starting from the cheapest available
            var shipsToBuy = availableShips
                .OrderBy(s => costModel.GetShipTradeValue(s, s.Owner ?? town.Settlement.Party, mainParty.Party))
                .ToList();

            int boughtCount = 0;
            foreach (var ship in shipsToBuy)
            {
                if (boughtCount >= missingShips) break;

                var seller = ship.Owner ?? town.Settlement.Party;
                int cost = (int)costModel.GetShipTradeValue(ship, seller, mainParty.Party);

                // Ensure player gold is above the cost + configured reserve
                if (Hero.MainHero != null && Hero.MainHero.Gold >= (cost + settings.MinGoldReserveForBoats))
                {
                    ChangeShipOwnerAction.ApplyByTrade(mainParty.Party, ship);
                    boughtCount++;
                    TaleWorlds.Library.InformationManager.DisplayMessage(
                        new TaleWorlds.Library.InformationMessage($"[Party Manager] Auto-purchased ship: {ship.Name?.ToString() ?? "Ship"} for {cost} denars.")
                    );
                }
            }
        }
    }
}
