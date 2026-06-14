using FiefManager;
using PartyManager;
using TradeOptimizer;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class SettingsVersionTests
    {
        [Fact]
        public void SettingsIds_UseV3ToAvoidLoadingOldSavedOptions()
        {
            Assert.Equal("SettlementAutomationCore_v3", new SettlementAutomationCore.Settings().Id);
            Assert.Equal("TradeOptimizer_v3", new TradeOptimizer.Settings().Id);
            Assert.Equal("PartyManager_v3", new PartyManager.Settings().Id);
            Assert.Equal("EquipmentManager_v3", new EquipmentManager.Settings().Id);
            Assert.Equal("FiefManager_v3", new FiefManager.Settings().Id);
        }

        [Fact]
        public void CoreOwnsCarryCapacityPolicy()
        {
            var coreSettings = new SettlementAutomationCore.Settings();

            Assert.True(coreSettings.LimitToInventoryCapacity);
        }
    }
}

