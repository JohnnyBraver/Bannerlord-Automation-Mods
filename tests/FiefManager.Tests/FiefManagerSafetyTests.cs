using System;
using System.IO;
using Xunit;

namespace FiefManager.Tests
{
    public class FiefManagerSafetyTests
    {
        [Fact]
        public void Provider_ReturnsCoreFiefOrdersInsteadOfMutatingFiefState()
        {
            string source = ReadSource("FiefManager", "FiefManagerProvider.cs");

            Assert.Contains("IReadOnlyList<FiefAutomationOrder> GetFiefAutomationOrders", source);
            Assert.Contains("FiefAutomationOrder.QueueBuildings", source);
            Assert.Contains("FiefAutomationOrder.DepositBoostGold", source);
            Assert.DoesNotContain("town.BuildingsInProgress.Enqueue", source);
            Assert.DoesNotContain("Hero.MainHero.Gold -=", source);
            Assert.DoesNotContain("town.BoostBuildingProcess +=", source);
        }

        [Fact]
        public void Core_AppliesFiefOrdersCentrally()
        {
            string source = ReadSource("SettlementAutomationCore", "SubModule.cs");

            Assert.Contains("ExecuteFiefAutomationProvider", source);
            Assert.Contains("GetFiefAutomationOrders", source);
            Assert.Contains("foreach (var building in order.Buildings)", source);
            Assert.Contains("town.BuildingsInProgress.Enqueue(building)", source);
            Assert.Contains("GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amountToDeposit, false)", source);
            Assert.Contains("town.BoostBuildingProcess += amountToDeposit", source);
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
                    Directory.Exists(Path.Combine(dir.FullName, "FiefManager")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }
    }
}
