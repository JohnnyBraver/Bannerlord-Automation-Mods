using System.Linq;
using SettlementAutomationCore;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class AutomationRegistryTests
    {
        [Fact]
        public void RegisterRequestProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakeRequestProvider();

            try
            {
                AutomationRegistry.RegisterRequestProvider(provider);
                AutomationRegistry.RegisterRequestProvider(provider);

                Assert.Single(AutomationRegistry.ActiveRequestProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterRequestProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActiveRequestProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void RegisterRequest_StoresAndClearsCurrentRequests()
        {
            AutomationRegistry.ClearRequests();
            var request = AutomationRequest.ForInventoryTarget("PartyManager", RequestType.ItemCategory, "Food", 5, RequestProfile.Critical);

            AutomationRegistry.RegisterRequest(request);

            Assert.Same(request, AutomationRegistry.ActiveRequests.Single());

            AutomationRegistry.ClearRequests();
            Assert.Empty(AutomationRegistry.ActiveRequests);
        }

        [Fact]
        public void RegisterReportProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakeReportProvider();

            try
            {
                AutomationRegistry.RegisterReportProvider(provider);
                AutomationRegistry.RegisterReportProvider(provider);

                Assert.Single(AutomationRegistry.ActiveReportProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterReportProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActiveReportProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void RegisterPostBattleProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakePostBattleProvider();

            try
            {
                AutomationRegistry.RegisterPostBattleProvider(provider);
                AutomationRegistry.RegisterPostBattleProvider(provider);

                Assert.Single(AutomationRegistry.ActivePostBattleProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterPostBattleProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActivePostBattleProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void RegisterReservationProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakeReservationProvider();

            try
            {
                AutomationRegistry.RegisterReservationProvider(provider);
                AutomationRegistry.RegisterReservationProvider(provider);

                Assert.Single(AutomationRegistry.ActiveReservationProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterReservationProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActiveReservationProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void RegisterSettlementRecruitmentProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakeSettlementRecruitmentProvider();

            try
            {
                AutomationRegistry.RegisterSettlementRecruitmentProvider(provider);
                AutomationRegistry.RegisterSettlementRecruitmentProvider(provider);

                Assert.Single(AutomationRegistry.ActiveSettlementRecruitmentProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterSettlementRecruitmentProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActiveSettlementRecruitmentProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void RegisterSettlementCleanupProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakeSettlementCleanupProvider();

            try
            {
                AutomationRegistry.RegisterSettlementCleanupProvider(provider);
                AutomationRegistry.RegisterSettlementCleanupProvider(provider);

                Assert.Single(AutomationRegistry.ActiveSettlementCleanupProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterSettlementCleanupProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActiveSettlementCleanupProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void RegisterPrisonerDispositionProvider_DedupesAndUnregistersProvider()
        {
            var provider = new FakePrisonerDispositionProvider();

            try
            {
                AutomationRegistry.RegisterPrisonerDispositionProvider(provider);
                AutomationRegistry.RegisterPrisonerDispositionProvider(provider);

                Assert.Single(AutomationRegistry.ActivePrisonerDispositionProviders, r => ReferenceEquals(r.Provider, provider));
            }
            finally
            {
                AutomationRegistry.UnregisterPrisonerDispositionProvider(provider);
            }

            Assert.DoesNotContain(AutomationRegistry.ActivePrisonerDispositionProviders, r => ReferenceEquals(r.Provider, provider));
        }

        [Fact]
        public void BuildProviderConflictWarnings_IncludesSharedPoolInterfaces()
        {
            var recruitA = new NamedSettlementRecruitmentProvider("RecruitA");
            var recruitB = new NamedSettlementRecruitmentProvider("RecruitB");
            var tradeA = new NamedFreeTradeAnalyzer("TradeA");
            var tradeB = new NamedFreeTradeAnalyzer("TradeB");

            try
            {
                AutomationRegistry.RegisterSettlementRecruitmentProvider(recruitA);
                AutomationRegistry.RegisterSettlementRecruitmentProvider(recruitB);
                AutomationRegistry.RegisterFreeTradeAnalyzer(tradeA);
                AutomationRegistry.RegisterFreeTradeAnalyzer(tradeB);

                var warnings = AutomationRegistry.BuildProviderConflictWarnings();

                Assert.Contains(warnings, warning => warning.Contains("settlement recruitment") && warning.Contains("RecruitA") && warning.Contains("RecruitB"));
                Assert.Contains(warnings, warning => warning.Contains("free trade") && warning.Contains("TradeA") && warning.Contains("TradeB"));
            }
            finally
            {
                AutomationRegistry.UnregisterSettlementRecruitmentProvider(recruitA);
                AutomationRegistry.UnregisterSettlementRecruitmentProvider(recruitB);
                AutomationRegistry.UnregisterFreeTradeAnalyzer(tradeA);
                AutomationRegistry.UnregisterFreeTradeAnalyzer(tradeB);
            }
        }

        [Fact]
        public void BuildProviderConflictWarnings_DoesNotWarnForMultipleRequestProviders()
        {
            var requestA = new NamedRequestProvider("RequestA");
            var requestB = new NamedRequestProvider("RequestB");

            try
            {
                AutomationRegistry.RegisterRequestProvider(requestA);
                AutomationRegistry.RegisterRequestProvider(requestB);

                var warnings = AutomationRegistry.BuildProviderConflictWarnings();

                Assert.DoesNotContain(warnings, warning => warning.Contains("request"));
                Assert.DoesNotContain(warnings, warning => warning.Contains("RequestA"));
                Assert.DoesNotContain(warnings, warning => warning.Contains("RequestB"));
            }
            finally
            {
                AutomationRegistry.UnregisterRequestProvider(requestA);
                AutomationRegistry.UnregisterRequestProvider(requestB);
            }
        }

        [Fact]
        public void PostBattleAutomationResult_IgnoresBlankActivityLines()
        {
            var result = new PostBattleAutomationResult();

            result.AddActivity("");
            result.AddActivity("discarded 2 prisoners");

            Assert.True(result.HasActivity);
            Assert.Single(result.Activities);
            Assert.Equal("discarded 2 prisoners", result.Activities.Single().Message);
        }

        private sealed class FakeRequestProvider : IAutomationRequestProvider
        {
            public string ProviderName => "Fake";

            public void SubmitAutomationRequests(AutomationRequestContext context)
            {
            }
        }

        private sealed class FakeReportProvider : IAutomationReportProvider
        {
            public string ProviderName => "Fake";

            public System.Collections.Generic.IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context)
            {
                return new[] { "report" };
            }
        }

        private sealed class FakePostBattleProvider : IPostBattleAutomationProvider
        {
            public string ProviderName => "Fake";

            public PostBattleAutomationResult ProcessPostBattle(PostBattleAutomationContext context)
            {
                return new PostBattleAutomationResult();
            }
        }

        private sealed class FakeReservationProvider : IAutomationReservationProvider
        {
            public string ProviderName => "Fake";

            public System.Collections.Generic.IReadOnlyList<ItemReservation> GetReservations(AutomationReservationContext context)
            {
                return new[] { new ItemReservation("Fake", "Horse", true, 1) };
            }
        }

        private sealed class FakeSettlementRecruitmentProvider : ISettlementRecruitmentProvider
        {
            public string ProviderName => "Fake";
            public RecruitmentNotificationMode NotificationMode => RecruitmentNotificationMode.OneByOne;

            public System.Collections.Generic.IReadOnlyList<SettlementRecruitmentOrder> GetRecruitmentOrders(SettlementRecruitmentContext context)
            {
                return new SettlementRecruitmentOrder[0];
            }
        }

        private sealed class FakeSettlementCleanupProvider : ISettlementCleanupProvider
        {
            public string ProviderName => "Fake";

            public TradeProposal AnalyzeSettlementCleanup(TradeContext context)
            {
                return new TradeProposal(new System.Collections.Generic.List<TradeAction>());
            }
        }

        private sealed class FakePrisonerDispositionProvider : IPrisonerDispositionProvider
        {
            public string ProviderName => "Fake";

            public System.Collections.Generic.IReadOnlyList<PrisonerDispositionOrder> GetPrisonerDispositionOrders(PrisonerDispositionContext context)
            {
                return new PrisonerDispositionOrder[0];
            }
        }

        private sealed class NamedSettlementRecruitmentProvider : ISettlementRecruitmentProvider
        {
            public NamedSettlementRecruitmentProvider(string providerName)
            {
                ProviderName = providerName;
            }

            public string ProviderName { get; }
            public RecruitmentNotificationMode NotificationMode => RecruitmentNotificationMode.OneByOne;

            public System.Collections.Generic.IReadOnlyList<SettlementRecruitmentOrder> GetRecruitmentOrders(SettlementRecruitmentContext context)
            {
                return new SettlementRecruitmentOrder[0];
            }
        }

        private sealed class NamedFreeTradeAnalyzer : IFreeTradeAnalyzer
        {
            public NamedFreeTradeAnalyzer(string providerName)
            {
                ProviderName = providerName;
            }

            public string ProviderName { get; }

            public TradeProposal AnalyzeMarket(TradeContext context)
            {
                return new TradeProposal(new System.Collections.Generic.List<TradeAction>());
            }
        }

        private sealed class NamedRequestProvider : IAutomationRequestProvider
        {
            public NamedRequestProvider(string providerName)
            {
                ProviderName = providerName;
            }

            public string ProviderName { get; }

            public void SubmitAutomationRequests(AutomationRequestContext context)
            {
            }
        }

    }
}
