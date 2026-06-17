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

        private static AutomationReportItem Item(string name, InventoryItemCategory category, int quantity, int gold, int marketValue)
        {
            return new AutomationReportItem(name, category, quantity, gold, marketValue);
        }
    }
}
