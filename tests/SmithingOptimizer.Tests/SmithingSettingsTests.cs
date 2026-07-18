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

            Assert.Equal("SmithingOptimizer_v0_6_0", settings.Id);
            Assert.True(settings.AutoSwitchEnabled);
            Assert.True(settings.AutoBuySmithingSupplies);
            Assert.True(settings.LimitToInventory);
            Assert.Equal(OptimizationGoal.SellValue, settings.Goal);
            Assert.Equal(RequestProfile.Routine, settings.SupplyRequestProfile);
            Assert.Equal(10000, settings.SupplyGoldReserve);
            Assert.Equal(40, settings.DesiredHardwood);
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
