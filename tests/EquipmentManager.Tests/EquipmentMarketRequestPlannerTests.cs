using System.Linq;
using EquipmentManager;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentMarketRequestPlannerTests
    {
        [Fact]
        public void BuildRequestGroups_DedupesBySnapshotIdAndSplitsByReserveAndProfile()
        {
            var armorA = MarketItem("armor_a");
            var armorADuplicate = MarketItem("armor_a");
            var armorB = MarketItem("armor_b");
            var stealth = MarketItem("stealth_hood");

            var groups = EquipmentMarketRequestPlanner.BuildRequestGroups(
                new[]
                {
                    new EquipmentMarketCandidateOrder(armorA, 1000, RequestProfile.Luxury, 10),
                    new EquipmentMarketCandidateOrder(armorADuplicate, 1000, RequestProfile.Luxury, 20),
                    new EquipmentMarketCandidateOrder(armorB, 1000, RequestProfile.Luxury, 15),
                    new EquipmentMarketCandidateOrder(stealth, 500, RequestProfile.Routine, 30)
                },
                "EquipmentManager",
                1).ToList();

            Assert.Equal(2, groups.Count);
            Assert.Equal(RequestProfile.Routine, groups[0].Profile);
            Assert.Equal(500, groups[0].ExplicitGoldReserve);
            Assert.Single(groups[0].Request.MarketCandidates);
            Assert.Equal("stealth_hood", groups[0].TopCandidate.Item.StringId);

            Assert.Equal(RequestProfile.Luxury, groups[1].Profile);
            Assert.Equal(2, groups[1].CandidateCount);
            Assert.Equal(new[] { "armor_a", "armor_b" }, groups[1].Request.MarketCandidates.Select(c => c.Item.StringId).ToArray());
        }

        [Fact]
        public void BuildRequestGroups_ForwardsConfiguredPurchaseCount()
        {
            var armorA = MarketItem("armor_a");
            var armorB = MarketItem("armor_b");

            var group = EquipmentMarketRequestPlanner.BuildRequestGroups(
                new[]
                {
                    new EquipmentMarketCandidateOrder(armorA, 1000, RequestProfile.Luxury, 20),
                    new EquipmentMarketCandidateOrder(armorB, 1000, RequestProfile.Luxury, 10)
                },
                "EquipmentManager",
                3).Single();

            Assert.Equal(3, group.Request.Quantity);
            Assert.Equal(new[] { "armor_a", "armor_b" }, group.Request.MarketCandidates.Select(c => c.Item.StringId).ToArray());
        }

        [Fact]
        public void BuildRequestGroups_ForwardsTrackPriorityToCore()
        {
            var armor = MarketItem("armor_a");

            var group = EquipmentMarketRequestPlanner.BuildRequestGroups(
                new[] { new EquipmentMarketCandidateOrder(armor, 1000, RequestProfile.Luxury, 20) },
                "EquipmentManager",
                1,
                requestPriority: 9).Single();

            Assert.Equal(9, group.Request.Priority);
        }

        [Theory]
        [InlineData(TaleWorlds.Core.EquipmentIndex.Weapon0, "Weapon 1")]
        [InlineData(TaleWorlds.Core.EquipmentIndex.Weapon1, "Weapon 2")]
        public void GetUpgradeSlotLabel_FormatsWeaponSlots(TaleWorlds.Core.EquipmentIndex slot, string expected)
        {
            Assert.Equal(expected, EquipmentManagerProvider.GetUpgradeSlotLabel(slot));
        }

        [Theory]
        [InlineData("blackened_hood", StealthGearPurchasePolicy.BlackenedOnly, true)]
        [InlineData("leather_hood", StealthGearPurchasePolicy.BlackenedOnly, false)]
        [InlineData("leather_hood", StealthGearPurchasePolicy.AnyStealthCompatible, true)]
        public void IsAllowedStealthBuyCandidate_AppliesPurchasePolicy(string itemId, StealthGearPurchasePolicy policy, bool expected)
        {
            var item = new ItemObject(itemId);
            var equipmentElement = new EquipmentElement(item, null, null!, false);

            Assert.Equal(expected, EquipmentManagerProvider.IsAllowedStealthBuyCandidate(equipmentElement, policy));
        }

        [Fact]
        public void IsUpgradeForAnyTarget_ReturnsFalseForStealthThrowingStone()
        {
            var item = new ItemObject("stealth_throwing_stone");
            var eqEl = new EquipmentElement(item, null, null!, false);
            var targets = new System.Collections.Generic.List<TaleWorlds.CampaignSystem.Hero>();
            var settings = new Settings();

            bool result = EquipmentManagerProvider.IsUpgradeForAnyTarget(eqEl, targets, settings);

            Assert.False(result);
        }

        private static InventoryItemView MarketItem(string id)
        {
            var item = new ItemObject(id);

            return new InventoryItemView(
                InventoryLogic.InventorySide.OtherInventory,
                new EquipmentElement(item, null, null!, false),
                1,
                100,
                InventoryItemCategory.Armor);
        }
    }
}
