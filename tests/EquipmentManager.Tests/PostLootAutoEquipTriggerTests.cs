using EquipmentManager;
using Xunit;

namespace EquipmentManager.Tests
{
    public class PostLootAutoEquipTriggerTests
    {
        [Fact]
        public void Trigger_QueuesRunOnlyAfterWinningBattleAndEquipmentLoot()
        {
            var trigger = new PostLootAutoEquipTrigger();

            Assert.False(trigger.OnItemsLooted(isMainParty: true, containsEquipment: true));

            trigger.OnBattleEnded(playerWonBattle: true);
            Assert.True(trigger.IsExpectingPostBattleLoot);
            Assert.False(trigger.OnItemsLooted(isMainParty: true, containsEquipment: false));
            Assert.False(trigger.IsPendingPostLootAutoEquip);

            Assert.True(trigger.OnItemsLooted(isMainParty: true, containsEquipment: true));
            Assert.False(trigger.IsExpectingPostBattleLoot);
            Assert.True(trigger.IsPendingPostLootAutoEquip);

            Assert.False(trigger.ShouldRunOnTick());
            Assert.True(trigger.ShouldRunOnTick());
            Assert.False(trigger.ShouldRunOnTick());
        }

        [Fact]
        public void Trigger_DoesNotQueueAfterLostOrNonPlayerBattle()
        {
            var trigger = new PostLootAutoEquipTrigger();

            trigger.OnBattleEnded(playerWonBattle: false);

            Assert.False(trigger.IsExpectingPostBattleLoot);
            Assert.False(trigger.OnItemsLooted(isMainParty: true, containsEquipment: true));
        }
    }
}

