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

        private static string ToRelativePath(string root, string path)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(prefix.Length)
                : path;
        }
    }
}
