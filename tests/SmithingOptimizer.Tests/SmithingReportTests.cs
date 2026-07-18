using System.Collections.Generic;
using SettlementAutomationCore;
using Xunit;

namespace SmithingOptimizer.Tests
{
    public class SmithingReportTests
    {
        [Fact]
        public void Provider_ExposesStableNameAndReportColor()
        {
            var provider = new SmithingOptimizerProvider();

            Assert.Equal("SmithingOptimizer", provider.ProviderName);
            Assert.Equal(0xB0A89CFFu, provider.ReportHeaderColor);
        }

        [Fact]
        public void BuildAutomationReportLines_FormatsPrioritySupplyPurchases()
        {
            var provider = new SmithingOptimizerProvider();
            var report = new AutomationProviderReport(
                "SmithingOptimizer",
                AutomationTransactionStage.PriorityRequest,
                new List<AutomationReportItem>
                {
                    new AutomationReportItem("Hardwood", InventoryItemCategory.TradeGood, 40, 320),
                    new AutomationReportItem("Charcoal", InventoryItemCategory.TradeGood, 20, 180)
                },
                new List<AutomationReportItem>(),
                new List<AutomationReportItem>());
            var context = new AutomationReportContext(null!, "SmithingOptimizer", report, null);

            var lines = provider.BuildAutomationReportLines(context);

            Assert.Equal("[Smithing] Bought supplies @ Settlement: 40x Hardwood (-320d), 20x Charcoal (-180d)", Assert.Single(lines));
        }

        [Fact]
        public void BuildAutomationReportLines_IgnoresWrongStageOrEmptyPurchases()
        {
            var provider = new SmithingOptimizerProvider();
            var freeTrade = new AutomationReportContext(
                null!,
                "SmithingOptimizer",
                new AutomationProviderReport(
                    "SmithingOptimizer",
                    AutomationTransactionStage.FreeTrade,
                    new List<AutomationReportItem> { new AutomationReportItem("Hardwood", 1, 10) },
                    new List<AutomationReportItem>(),
                    new List<AutomationReportItem>()),
                null);
            var emptyPriority = new AutomationReportContext(
                null!,
                "SmithingOptimizer",
                new AutomationProviderReport(
                    "SmithingOptimizer",
                    AutomationTransactionStage.PriorityRequest,
                    new List<AutomationReportItem>(),
                    new List<AutomationReportItem>(),
                    new List<AutomationReportItem>()),
                null);

            Assert.Empty(provider.BuildAutomationReportLines(freeTrade));
            Assert.Empty(provider.BuildAutomationReportLines(emptyPriority));
            Assert.Empty(provider.BuildAutomationReportLines(null!));
        }
    }
}
