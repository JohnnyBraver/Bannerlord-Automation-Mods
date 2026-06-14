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
