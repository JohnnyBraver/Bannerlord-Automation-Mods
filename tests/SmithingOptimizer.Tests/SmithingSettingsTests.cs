using SettlementAutomationCore;
using Xunit;

namespace SmithingOptimizer.Tests
{
    public class SmithingSettingsTests
    {
        [Fact]
        public void Settings_DefaultsKeepAutomationCompatible()
        {
            var settings = new Settings();

            Assert.Equal("SmithingOptimizer_v0_4", settings.Id);
            Assert.True(settings.AutoSwitchEnabled);
            Assert.True(settings.AutoBuySmithingSupplies);
            Assert.True(settings.LimitToInventory);
            Assert.Equal(OptimizationGoal.XpEfficiency, settings.Goal);
            Assert.Equal(RequestProfile.Routine, settings.SupplyRequestProfile);
            Assert.Equal(5, settings.SupplyRequestPriority);
            Assert.Equal(1000, settings.SupplyGoldReserve);
            Assert.Equal(40, settings.DesiredHardwood);
            Assert.Equal(20, settings.DesiredCharcoal);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(4, 0)]
        [InlineData(5, 10)]
        [InlineData(14, 10)]
        [InlineData(15, 20)]
        public void DesiredSupplyCounts_SnapToNearestTen(int input, int expected)
        {
            var settings = new Settings
            {
                DesiredHardwood = input,
                DesiredCharcoal = input
            };

            Assert.Equal(expected, settings.DesiredHardwood);
            Assert.Equal(expected, settings.DesiredCharcoal);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(499, 0)]
        [InlineData(500, 1000)]
        [InlineData(1499, 1000)]
        [InlineData(1500, 2000)]
        public void SupplyGoldReserve_SnapsToNearestThousand(int input, int expected)
        {
            var settings = new Settings
            {
                SupplyGoldReserve = input
            };

            Assert.Equal(expected, settings.SupplyGoldReserve);
        }
    }
}
