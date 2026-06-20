using System.Collections.Generic;
using EquipmentManager;
using SettlementAutomationCore;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentReportTests
    {
        [Fact]
        public void Settings_DefaultsToCategoryCountSalesAndPaidPriceSorting()
        {
            var settings = new Settings();

            Assert.Equal(EquipmentSaleReportDetailMode.CategoryCounts, settings.EquipmentSaleReportDetail);
            Assert.Equal(4, settings.MaxReportItemsToPrint);
            Assert.Equal(EquipmentReportSortMode.PaidPrice, settings.EquipmentReportSort);
            Assert.True(settings.ModEnabled);
            Assert.True(settings.AutoEquipBeforeSettlementTrade);
            Assert.True(settings.AutoEquipAfterSettlementPurchases);
            Assert.True(settings.AutoEquipAfterBattleLoot);
            Assert.Equal(ProjectileUpgradePreference.CountAndDamage, settings.AmmoUpgradePreferenceSetting);
            Assert.Equal(ProjectileUpgradePreference.CountAndDamage, settings.ThrowingWeaponUpgradePreferenceSetting);
            Assert.True(settings.IgnoreThrowingWeaponMeleeStats);
            Assert.False(settings.BuyHandSlotWeapons);
            Assert.True(settings.BuyOneHandedWeaponUpgrades);
            Assert.True(settings.BuyTwoHandedWeaponUpgrades);
            Assert.True(settings.BuyPolearmUpgrades);
            Assert.True(settings.BuyThrowingWeaponUpgrades);
            Assert.True(settings.BuyBowUpgrades);
            Assert.True(settings.BuyCrossbowUpgrades);
            Assert.True(settings.BuyShieldUpgrades);
            Assert.Equal(1, settings.MaxArmorUpgradesPerVisit);
            Assert.Equal(1, settings.MaxHandSlotWeaponUpgradesPerVisit);
            Assert.Equal(RequestProfile.Luxury, settings.WeaponRequestProfile);
        }

        [Fact]
        public void BuildAutomationReportLines_DefaultsSalesToCategoryCounts()
        {
            var lines = EquipmentManagerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.PreSell,
                "Marunath",
                new List<AutomationReportItem>(),
                new[]
                {
                    Item("Wrapped Headcloth", InventoryItemCategory.Armor, 2, 88, 60),
                    Item("Barbed Arrows", InventoryItemCategory.Weapon, 2, 289, 80),
                    Item("Leather Shoes", InventoryItemCategory.Armor, 2, 31, 24)
                },
                EquipmentSaleReportDetailMode.CategoryCounts,
                maxItems: 4,
                EquipmentReportSortMode.PaidPrice);

            Assert.Equal("[Equipment] Sold spare gear @ Marunath: Armor 4x (+119d), Weapons 2x (+289d)", Assert.Single(lines));
        }

        [Fact]
        public void BuildAutomationReportLines_CanPrintSortedSaleDetails()
        {
            var lines = EquipmentManagerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.PreSell,
                "Marunath",
                new List<AutomationReportItem>(),
                new[]
                {
                    Item("Wrapped Headcloth", InventoryItemCategory.Armor, 2, 88, 60),
                    Item("Barbed Arrows", InventoryItemCategory.Weapon, 2, 289, 80),
                    Item("Leather Shoes", InventoryItemCategory.Armor, 2, 31, 24)
                },
                EquipmentSaleReportDetailMode.FullItemList,
                maxItems: 2,
                EquipmentReportSortMode.PaidPrice);

            Assert.Equal("[Equipment] Sold spare gear @ Marunath: 2x Barbed Arrows (+289d), 2x Wrapped Headcloth (+88d), 1 more", Assert.Single(lines));
        }

        [Fact]
        public void BuildAutomationReportLines_LimitsAndSortsUpgradeDetails()
        {
            var lines = EquipmentManagerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.PriorityRequest,
                "Marunath",
                new[]
                {
                    Item("Blackened Armor", InventoryItemCategory.Armor, 1, 2096, 3000),
                    Item("Ridged Broadsword", InventoryItemCategory.Weapon, 1, 245, 1500)
                },
                new List<AutomationReportItem>(),
                EquipmentSaleReportDetailMode.CategoryCounts,
                maxItems: 1,
                EquipmentReportSortMode.MarketValue);

            Assert.Equal("[Equipment] Bought upgrades @ Marunath: 1x Blackened Armor (-2096d), 1 more", Assert.Single(lines));
        }

        [Theory]
        [InlineData(TaleWorlds.Core.EquipmentIndex.Head, "Head")]
        [InlineData(TaleWorlds.Core.EquipmentIndex.Body, "Torso")]
        [InlineData(TaleWorlds.Core.EquipmentIndex.Weapon0, "Weapon 1")]
        [InlineData(TaleWorlds.Core.EquipmentIndex.Weapon3, "Weapon 4")]
        public void GetUpgradeSlotLabel_UsesReadableSlotNames(TaleWorlds.Core.EquipmentIndex slot, string expected)
        {
            Assert.Equal(expected, EquipmentManagerProvider.GetUpgradeSlotLabel(slot));
        }

        private static AutomationReportItem Item(string name, InventoryItemCategory category, int quantity, int gold, int marketValue)
        {
            return new AutomationReportItem(name, category, quantity, gold, marketValue);
        }
    }
}
