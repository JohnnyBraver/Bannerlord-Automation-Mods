using System.Linq;
using SettlementAutomationCore;
using TaleWorlds.Core;
using Xunit;

namespace TradeOptimizer.Tests
{
    public class TradePlanTests
    {
        [Fact]
        public void ToTradeProposal_AggregatesByIdentityAndPreservesEquipmentElement()
        {
            var item = new ItemObject("grain");
            var fine = new ItemModifier { StringId = "fine" };
            var fineElement = new EquipmentElement(item, fine, null!, false);
            var plan = new TradePlan();

            plan.RecordAction(BuyAction(fineElement, "grain::fine", quantity: 1, gold: 40));
            plan.RecordAction(BuyAction(fineElement, "grain::fine", quantity: 2, gold: 90));

            var proposal = plan.ToTradeProposal();
            var action = Assert.Single(proposal.Actions);

            Assert.Equal(3, action.Quantity);
            Assert.Equal(TradeActionType.Buy, action.ActionType);
            Assert.Equal(fine, action.EquipmentElement.ItemModifier);
        }

        [Fact]
        public void ToTradeProposal_DoesNotMergeDifferentIdentitiesWithSameDisplayName()
        {
            var item = new ItemObject("grain");
            var fine = new ItemModifier { StringId = "fine" };
            var cracked = new ItemModifier { StringId = "cracked" };
            var fineElement = new EquipmentElement(item, fine, null!, false);
            var crackedElement = new EquipmentElement(item, cracked, null!, false);
            var plan = new TradePlan();

            plan.RecordAction(BuyAction(fineElement, "grain::fine", quantity: 1, gold: 40));
            plan.RecordAction(BuyAction(crackedElement, "grain::cracked", quantity: 1, gold: 35));

            var proposal = plan.ToTradeProposal();

            Assert.Equal(2, proposal.Actions.Count);
            Assert.Contains(proposal.Actions, action => action.EquipmentElement.ItemModifier == fine);
            Assert.Contains(proposal.Actions, action => action.EquipmentElement.ItemModifier == cracked);
        }

        [Fact]
        public void ToTransactionReport_RendersPlannedBuyAndSellSummaries()
        {
            var item = new ItemObject("tools");
            var element = new EquipmentElement(item, null, null!, false);
            var plan = new TradePlan();

            plan.RecordAction(BuyAction(element, "tools::", quantity: 4, gold: 200));
            plan.RecordAction(new PlannedTradeAction(
                PlannedTradeActionKind.Sell,
                TradePlanPhase.Sell,
                element,
                "tools::",
                "Tools",
                2,
                180,
                90,
                90,
                80f,
                0f));

            var report = plan.ToTransactionReport();

            Assert.Equal(4, Assert.Single(report.BoughtItems).Count);
            Assert.Equal(200, report.BoughtItems.Single().Gold);
            Assert.Equal(2, Assert.Single(report.SoldItems).Count);
            Assert.Equal(180, report.SoldItems.Single().Gold);
        }

        private static PlannedTradeAction BuyAction(EquipmentElement element, string identityKey, int quantity, int gold)
        {
            return new PlannedTradeAction(
                PlannedTradeActionKind.Buy,
                TradePlanPhase.DirectBuy,
                element,
                identityKey,
                "Grain",
                quantity,
                gold,
                gold / quantity,
                gold / quantity,
                100f,
                1f);
        }
    }
}
