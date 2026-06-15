using System.Collections.Generic;
using System.Linq;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class RequestPolicyTests
    {
        [Fact]
        public void SortRequests_OrdersByProfileThenPriorityDescending()
        {
            var requests = new[]
            {
                Request("routine-low", RequestProfile.Routine, 1),
                Request("critical-low", RequestProfile.Critical, 1),
                Request("critical-high", RequestProfile.Critical, 9),
                Request("luxury-high", RequestProfile.Luxury, 9),
                Request("essential", RequestProfile.Essential, 5),
                Request("opportunistic", RequestProfile.Opportunistic, 5)
            };

            var sorted = RequestPolicy.SortRequests(requests).Select(r => r.RequestorId).ToList();

            Assert.Equal(
                new[] { "critical-high", "critical-low", "essential", "routine-low", "opportunistic", "luxury-high" },
                sorted);
        }

        [Theory]
        [InlineData(RequestProfile.Critical, 250, true)]
        [InlineData(RequestProfile.Essential, 250, true)]
        [InlineData(RequestProfile.Routine, 150, true)]
        [InlineData(RequestProfile.Routine, 151, false)]
        [InlineData(RequestProfile.Opportunistic, 110, true)]
        [InlineData(RequestProfile.Opportunistic, 111, false)]
        [InlineData(RequestProfile.Luxury, 500, true)]
        public void IsPriceAllowedForProfile_AppliesCapsOnlyToRoutineAndOpportunistic(RequestProfile profile, int price, bool expected)
        {
            Assert.Equal(expected, RequestPolicy.IsPriceAllowedForProfile(profile, 100, price, 1.5f, 1.1f));
        }

        [Fact]
        public void GetGoldReserveForRequest_UsesExplicitReserveForLuxuryArmorStyleRequests()
        {
            var request = AutomationRequest.ForMarketItems(
                "EquipmentManager",
                new[] { MarketItem("armor_a") },
                1,
                RequestProfile.Luxury,
                5,
                25000);

            int reserve = RequestPolicy.GetGoldReserveForRequest(request, 1000, 10, 400);

            Assert.Equal(25000, reserve);
        }

        [Fact]
        public void CreatesImplicitSellReservation_IgnoresPurchaseCountMarketRequests()
        {
            var market = AutomationRequest.ForMarketItems("EquipmentManager", new[] { MarketItem("armor_a") }, 1);
            var food = AutomationRequest.ForInventoryTarget("PartyManager", RequestType.ItemCategory, "Food", 10, RequestProfile.Critical);

            Assert.False(RequestPolicy.CreatesImplicitSellReservation(market));
            Assert.True(RequestPolicy.CreatesImplicitSellReservation(food));
        }

        [Fact]
        public void InventoryItemView_MatchesExactItemAndModifierIdentity()
        {
            var item = Item("armor_a");
            var fine = Modifier("fine");
            var cracked = Modifier("cracked");
            var view = new InventoryItemView(
                InventoryLogic.InventorySide.OtherInventory,
                new EquipmentElement(item, fine, null!, false),
                1,
                100,
                InventoryItemCategory.Armor);

            Assert.True(view.MatchesEquipmentElement(new EquipmentElement(item, fine, null!, false)));
            Assert.False(view.MatchesEquipmentElement(new EquipmentElement(item, cracked, null!, false)));
            Assert.False(view.MatchesEquipmentElement(new EquipmentElement(Item("armor_b"), fine, null!, false)));
        }

        [Fact]
        public void MarketActivitySummary_AggregatesAndLimitsInGameSummary()
        {
            var summary = new MarketActivitySummary();
            summary.AddBought("Grain", 3, 60);
            summary.AddBought("Grain", 2, 40);
            summary.AddSold("Sword", 1, 250);
            summary.AddSlaughtered("Hog", 4);
            summary.AddGoldDelta(-125);

            var inGame = summary.BuildInGameLines("Sargot", "123 / 456 capacity (27%)");
            string log = summary.BuildLogSummary("Sargot");

            Assert.Contains(inGame, line => line.Contains("Purchases: -100d"));
            Assert.Contains(inGame, line => line.Contains("Sales: +250d"));
            Assert.Contains(inGame, line => line.Contains("Bought: 5x Grain (-100d)"));
            Assert.Contains(inGame, line => line.Contains("Sold: 1x Sword (+250d)"));
            Assert.Contains(inGame, line => line.Contains("Slaughtered: 4x Hog"));
            Assert.Contains(inGame, line => line.Contains("Cargo: 123 / 456 capacity (27%)"));
            Assert.Contains("Gold change: -125d", log);
            Assert.Contains("Bought: 5x Grain (-100d)", log);
            Assert.Contains("Sold: 1x Sword (+250d)", log);
        }

        private static AutomationRequest Request(string id, RequestProfile profile, int priority)
        {
            return AutomationRequest.ForInventoryTarget(id, RequestType.ItemCategory, "Food", 1, profile, priority);
        }

        private static InventoryItemView MarketItem(string id)
        {
            return new InventoryItemView(
                InventoryLogic.InventorySide.OtherInventory,
                new EquipmentElement(Item(id), null, null!, false),
                1,
                100,
                InventoryItemCategory.Armor);
        }

        private static ItemObject Item(string id)
        {
            return new ItemObject(id);
        }

        private static ItemModifier Modifier(string id)
        {
            return new ItemModifier
            {
                StringId = id
            };
        }
    }
}
