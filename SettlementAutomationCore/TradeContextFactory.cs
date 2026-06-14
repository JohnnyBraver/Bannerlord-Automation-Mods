using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    public static class TradeContextFactory
    {
        public static TradeContext Create(
            MobileParty party,
            Settlement settlement,
            InventoryLogic logic,
            IReadOnlyList<SellableItem>? sellableItems = null)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (logic == null) throw new ArgumentNullException(nameof(logic));

            var settings = Settings.Instance;
            var hero = Hero.MainHero;
            int dailyWage = party.TotalWage;
            int minimumGoldReserve = settings?.MinimumGoldReserve ?? 1000;
            int minDaysExpensesToKeep = settings?.MinDaysExpensesToKeep ?? 10;
            int expenseReserve = Math.Max(minimumGoldReserve, dailyWage * minDaysExpensesToKeep);
            int heroGold = hero?.Gold ?? 0;
            int availableGold = Math.Max(0, heroGold - expenseReserve);

            float currentWeight = Helpers.InventoryHelper.GetRosterWeight(party.ItemRoster);
            float freeCargo = Math.Max(0f, party.InventoryCapacity - currentWeight);
            int freeAnimalSlots = HerdingCalculator.GetRemainingAnimalSlots(party);

            return new TradeContext(
                settlement,
                party,
                logic,
                availableGold,
                freeCargo,
                settings?.LimitToInventoryCapacity ?? true,
                freeAnimalSlots,
                freeAnimalSlots,
                sellableItems ?? Array.Empty<SellableItem>());
        }
    }
}
