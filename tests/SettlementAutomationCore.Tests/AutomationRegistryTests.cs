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
    }
}
