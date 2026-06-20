using System;
using System.IO;
using Xunit;

namespace PartyManager.Tests
{
    public class PartyManagerRoutingTests
    {
        [Fact]
        public void PartyManager_RoutesHerdingCleanupThroughSettlementCleanupProvider()
        {
            string providerSource = ReadSource("PartyManager", "PartyManagerProvider.cs");
            string subModuleSource = ReadSource("PartyManager", "SubModule.cs");

            Assert.Contains("ISettlementCleanupProvider", providerSource);
            Assert.Contains("TradeHelper.AnalyzeHerdingCleanup", providerSource);
            Assert.Contains("AutomationRegistry.RegisterSettlementCleanupProvider(_provider)", subModuleSource);
            Assert.Contains("AutomationRegistry.UnregisterSettlementCleanupProvider(_provider)", subModuleSource);
            Assert.DoesNotContain("IFreeTradeAnalyzer", providerSource);
            Assert.DoesNotContain("RegisterFreeTradeAnalyzer(_provider)", subModuleSource);
            Assert.DoesNotContain("RegisterSettlementLogisticsProvider", subModuleSource);
            Assert.DoesNotContain("ISettlementLogisticsProvider", providerSource);
        }

        [Fact]
        public void Settings_UseMountsAndHerdingSectionName()
        {
            string settingsSource = ReadSource("PartyManager", "Settings.cs");

            Assert.Contains("Mounts & Herding", settingsSource);
            Assert.DoesNotContain("Mounts & Logistics", settingsSource);
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
                    Directory.Exists(Path.Combine(dir.FullName, "PartyManager")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }
    }
}
