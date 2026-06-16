using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class ModuleDependencyTests
    {
        public static IEnumerable<object[]> ModuleDependencies()
        {
            yield return new object[]
            {
                "SettlementAutomationCore",
                new[] { "Native", "SandBoxCore", "Bannerlord.MBOptionScreen" }
            };
            yield return new object[]
            {
                "TradeOptimizer",
                new[] { "Native", "SandBoxCore", "SettlementAutomationCore", "Bannerlord.Harmony", "Bannerlord.UIExtenderEx", "Bannerlord.MBOptionScreen" }
            };
            yield return new object[]
            {
                "PartyManager",
                new[] { "Native", "SandBoxCore", "SettlementAutomationCore", "Bannerlord.MBOptionScreen" }
            };
            yield return new object[]
            {
                "EquipmentManager",
                new[] { "Native", "SandBoxCore", "SettlementAutomationCore", "Bannerlord.Harmony", "Bannerlord.UIExtenderEx", "Bannerlord.MBOptionScreen" }
            };
            yield return new object[]
            {
                "FiefManager",
                new[] { "Native", "SandBoxCore", "SettlementAutomationCore", "Bannerlord.MBOptionScreen" }
            };
        }

        [Theory]
        [MemberData(nameof(ModuleDependencies))]
        public void SubModuleXml_DeclaresExactDirectDependencies(string moduleName, string[] expectedDependencies)
        {
            string root = FindRepoRoot();
            string manifestPath = Path.Combine(root, moduleName, "SubModule.xml");
            var manifest = XDocument.Load(manifestPath);

            var actual = manifest
                .Descendants("DependedModule")
                .Select(e => e.Attribute("Id")?.Value)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray();

            Assert.Equal(expectedDependencies, actual);
        }

        [Theory]
        [InlineData("SettlementAutomationCore")]
        [InlineData("TradeOptimizer")]
        [InlineData("PartyManager")]
        [InlineData("EquipmentManager")]
        [InlineData("FiefManager")]
        public void ModuleBuildOutput_DoesNotContainCopiedDependencyDlls(string moduleName)
        {
            string root = FindRepoRoot();
            string outputPath = Path.Combine(root, moduleName, "bin", "Release");
            if (!Directory.Exists(outputPath))
            {
                return;
            }

            var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "0Harmony.dll",
                "Bannerlord.UIExtenderEx.dll",
                "MCMv5.dll",
                "Newtonsoft.Json.dll"
            };
            if (!string.Equals(moduleName, "SettlementAutomationCore", StringComparison.OrdinalIgnoreCase))
            {
                forbidden.Add("SettlementAutomationCore.dll");
            }
            if (!string.Equals(moduleName, "TradeOptimizer", StringComparison.OrdinalIgnoreCase))
            {
                forbidden.Add("TradeOptimizer.dll");
            }

            var offenders = Directory.EnumerateFiles(outputPath, "*.dll")
                .Select(Path.GetFileName)
                .Where(name => name != null && forbidden.Contains(name))
                .ToArray();

            Assert.Empty(offenders);
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
    }
}
