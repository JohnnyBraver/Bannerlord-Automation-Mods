using FiefManager;
using PartyManager;
using TradeOptimizer;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class SettingsVersionTests
    {
        [Fact]
        public void SettingsIds_UseV05ToAvoidLoadingV04SavedOptions()
        {
            Assert.Equal("SettlementAutomationCore_v0_5", new SettlementAutomationCore.Settings().Id);
            Assert.Equal("TradeOptimizer_v0_5", new TradeOptimizer.Settings().Id);
            Assert.Equal("PartyManager_v0_5", new PartyManager.Settings().Id);
            Assert.Equal("EquipmentManager_v0_4", new EquipmentManager.Settings().Id);
            Assert.Equal("FiefManager_v0_4", new FiefManager.Settings().Id);
            Assert.Equal("SmithingOptimizer_v0_4", new SmithingOptimizer.Settings().Id);
        }

        [Fact]
        public void CoreOwnsCarryCapacityPolicy()
        {
            var coreSettings = new SettlementAutomationCore.Settings();

            Assert.True(coreSettings.LimitToInventoryCapacity);
            Assert.Equal(SettlementAutomationCore.IgnoreWeightLimitTier.AllRequests, coreSettings.IgnoreWeightLimitSetting);
        }

        [Fact]
        public void CoreDisableSettlementAutomation_DefaultsOff()
        {
            var coreSettings = new SettlementAutomationCore.Settings();

            Assert.False(coreSettings.DisableSettlementAutomation);
        }

        [Fact]
        public void FeatureModMasterToggles_DefaultEnabled()
        {
            Assert.True(new TradeOptimizer.Settings().ModEnabled);
            Assert.True(new PartyManager.Settings().ModEnabled);
            Assert.True(new EquipmentManager.Settings().ModEnabled);
            Assert.True(new FiefManager.Settings().ModEnabled);
        }

        [Fact]
        public void CoreMarketReporting_DefaultsToFull()
        {
            var coreSettings = new SettlementAutomationCore.Settings();

            Assert.Equal(SettlementAutomationCore.CoreReportingMode.Full, coreSettings.CoreReportingModeSetting);
        }

        [Fact]
        public void CoreRejectedOrderDetails_DefaultsOff()
        {
            var coreSettings = new SettlementAutomationCore.Settings();

            Assert.False(coreSettings.LogRejectedOrderDetails);
        }
    }
}
