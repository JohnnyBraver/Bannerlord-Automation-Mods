using System.Collections.Generic;
using SettlementAutomationCore;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class InventoryLockHelperTests
    {
        [Fact]
        public void IsLocked_MatchesUnmodifiedItemLock()
        {
            var locks = new HashSet<string> { "hardwood" };

            Assert.True(InventoryLockHelper.IsLocked("hardwood", "", locks));
        }

        [Theory]
        [InlineData("iron_swordfine")]
        [InlineData("iron_sword::fine")]
        [InlineData("iron_sword:fine")]
        public void IsLocked_MatchesKnownModifiedItemLockShapes(string lockKey)
        {
            var locks = new HashSet<string> { lockKey };

            Assert.True(InventoryLockHelper.IsLocked("iron_sword", "fine", locks));
        }

        [Fact]
        public void IsLocked_DoesNotMatchDifferentModifier()
        {
            var locks = new HashSet<string> { "iron_sword::cracked" };

            Assert.False(InventoryLockHelper.IsLocked("iron_sword", "fine", locks));
        }
    }
}
