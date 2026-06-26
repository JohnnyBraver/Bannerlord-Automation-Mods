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
            int reservePercent = settings?.ReserveCarryCapacityPercent ?? 0;
            float usableCargoLimit = party.InventoryCapacity * (100 - reservePercent) / 100f;
            float cargoCapacityBalance = usableCargoLimit - currentWeight;
            int freeAnimalSlots = HerdingCalculator.GetRemainingAnimalSlots(party);

            return new TradeContext(
                settlement,
                party,
                logic,
                availableGold,
                cargoCapacityBalance,
                settings?.LimitToInventoryCapacity ?? true,
                freeAnimalSlots,
                freeAnimalSlots,
                sellableItems ?? Array.Empty<SellableItem>());
        }

        internal static float CalculateFreeCargoCapacity(float inventoryCapacity, float currentWeight, int reservePercent)
        {
            if (inventoryCapacity <= 0f) return 0f;

            int clampedReservePercent = Math.Max(0, Math.Min(100, reservePercent));
            float reservedCapacity = inventoryCapacity * clampedReservePercent / 100f;
            float usableCapacity = Math.Max(0f, inventoryCapacity - reservedCapacity);
            return Math.Max(0f, usableCapacity - Math.Max(0f, currentWeight));
        }
    }
}
