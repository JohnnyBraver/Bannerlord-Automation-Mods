using System;
using System.IO;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class SmithingOptimizerIntegrationTests
    {
        [Fact]
        public void SmithingOptimizer_RegistersCoreRequestAndReportProviders()
        {
            string subModule = ReadSource("SmithingOptimizer", "SubModule.cs");
            string provider = ReadSource("SmithingOptimizer", "SmithingOptimizerProvider.cs");

            Assert.Contains("AutomationRegistry.RegisterRequestProvider(_provider)", subModule);
            Assert.Contains("AutomationRegistry.RegisterReportProvider(_provider)", subModule);
            Assert.Contains("AutomationRegistry.UnregisterRequestProvider(_provider)", subModule);
            Assert.Contains("AutomationRegistry.UnregisterReportProvider(_provider)", subModule);
            Assert.Contains("IAutomationRequestProvider", provider);
            Assert.Contains("IAutomationReportProvider", provider);
        }

        [Fact]
        public void SmithingOptimizer_SubmitsOnlyHardwoodRequestsThroughCore()
        {
            string provider = ReadSource("SmithingOptimizer", "SmithingOptimizerProvider.cs");
            string settings = ReadSource("SmithingOptimizer", "Settings.cs");

            Assert.Contains("\"Hardwood\"", provider);
            Assert.DoesNotContain("\"Charcoal\"", provider);
            Assert.Contains("AutomationRequest.ForInventoryTarget", provider);
            Assert.Contains("RequestType.SpecificItem", provider);
            Assert.Contains("Hero.MainHero?.Gold ?? 0) <= settings.SupplyGoldReserve", provider);
            Assert.Contains("BudgetPolicyKind.ExplicitReserve", provider);
            Assert.DoesNotContain("TransferCommand.Transfer", provider);
            Assert.DoesNotContain("DoneLogic()", provider);
            Assert.Contains("SmithingOptimizer_v0_6_0", settings);
            Assert.Contains("AutoBuySmithingSupplies", settings);
            Assert.DoesNotContain("DesiredCharcoal", settings);
            Assert.DoesNotContain("SupplyRequestPriority", settings);
        }

        private static string ReadSource(params string[] parts)
        {
            string root = FindRepoRoot();
            return File.ReadAllText(Path.Combine(root, Path.Combine(parts)));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "SettlementAutomationCore")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "SmithingOptimizer")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }
    }
}
