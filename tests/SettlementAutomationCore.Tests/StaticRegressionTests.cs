using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class StaticRegressionTests
    {
        [Theory]
        [InlineData("IEquipmentUpgradeProvider")]
        [InlineData("TargetMarketItem")]
        [InlineData("MaxPriceMultiplier")]
        [InlineData("ForTroopTarget")]
        [InlineData("IsLocked = true")]
        [InlineData("SettlementAutomationCore.Settings")]
        [InlineData("TradeOptimizer.Settings")]
        [InlineData("PartyManager.Settings")]
        [InlineData("EquipmentManager.Settings")]
        [InlineData("FiefManager.Settings")]
        [InlineData("GetType(\"PartyManager.Settings")]
        [InlineData("GetProperty(\"Instance\"")]
        public void SourceDoesNotContainLegacyAutomationPaths(string forbiddenText)
        {
            string root = FindRepoRoot();
            var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsIgnored(path))
                .Where(path => File.ReadAllText(path).Contains(forbiddenText))
                .Select(path => ToRelativePath(root, path))
                .ToList();

            Assert.Empty(offenders);
        }

        [Theory]
        [InlineData("MinimumGoldReserve")]
        [InlineData("MinDaysExpensesToKeep")]
        [InlineData("LimitToInventoryCapacity")]
        public void TradeOptimizerDoesNotExposeCoreBudgetOrCargoSettings(string forbiddenText)
        {
            string root = FindRepoRoot();
            string tradeOptimizerRoot = Path.Combine(root, "TradeOptimizer");
            var offenders = Directory.EnumerateFiles(tradeOptimizerRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsIgnored(path))
                .Where(path => File.ReadAllText(path).Contains(forbiddenText))
                .Select(path => ToRelativePath(root, path))
                .ToList();

            Assert.Empty(offenders);
        }

        [Fact]
        public void ManualButtonsDoNotRequireFeatureMasterToggles()
        {
            string root = FindRepoRoot();
            string equipmentEngine = File.ReadAllText(Path.Combine(root, "EquipmentManager", "EquipmentEngine.cs"));
            string tradePatches = File.ReadAllText(Path.Combine(root, "TradeOptimizer", "TradePatches.cs"));
            string tradingEngine = File.ReadAllText(Path.Combine(root, "TradeOptimizer", "TradingEngine.cs"));
            string craftingPatches = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "CraftingPatches.cs"));

            Assert.DoesNotContain("ModEnabled", ExtractMethod(equipmentEngine, "OptimizeEquipment"));
            Assert.DoesNotContain("ModEnabled", ExtractMethod(tradePatches, "ManualTrigger"));
            Assert.DoesNotContain("ModEnabled", ExtractMethod(tradingEngine, "RunOptimization"));
            Assert.Contains("TriggerOptimization(silentOnNoImprovement: false, requireAutomationEnabled: false)", craftingPatches);
        }

        [Fact]
        public void PassiveAutomationStillRequiresFeatureMasterToggles()
        {
            string root = FindRepoRoot();
            string equipmentEngine = File.ReadAllText(Path.Combine(root, "EquipmentManager", "EquipmentEngine.cs"));
            string tradeProvider = File.ReadAllText(Path.Combine(root, "TradeOptimizer", "TradeOptimizerProvider.cs"));
            string smithingPatches = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "CraftingPatches.cs"));
            string smithingProvider = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "SmithingOptimizerProvider.cs"));

            Assert.Contains("!settings.ModEnabled", ExtractMethod(equipmentEngine, "AutoEquipHeadless"));
            Assert.Contains("!settings.ModEnabled", ExtractMethod(tradeProvider, "AnalyzeMarket"));
            Assert.Contains("settings.ModEnabled && settings.AutoSwitchEnabled", smithingPatches);
            Assert.Contains("!settings.ModEnabled || !settings.AutoBuySmithingSupplies", smithingProvider);
        }

        private static bool IsIgnored(string path)
        {
            var parts = new HashSet<string>(
                path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparer.OrdinalIgnoreCase);
            return parts.Contains("tests") || parts.Contains("bin") || parts.Contains("obj");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "SettlementAutomationCore")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "EquipmentManager")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }

        private static string ExtractMethod(string source, string methodName)
        {
            int nameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
            Assert.True(nameIndex >= 0, $"Could not find method {methodName}.");

            int bodyStart = source.IndexOf('{', nameIndex);
            Assert.True(bodyStart >= 0, $"Could not find body for method {methodName}.");

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(bodyStart, i - bodyStart + 1);
                    }
                }
            }

            throw new InvalidOperationException($"Could not parse method {methodName}.");
        }

        private static string ToRelativePath(string root, string path)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(prefix.Length)
                : path;
        }
    }
}
