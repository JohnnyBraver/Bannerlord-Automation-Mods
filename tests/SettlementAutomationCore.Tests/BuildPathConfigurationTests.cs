using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class BuildPathConfigurationTests
    {
        public static IEnumerable<object[]> SharedPropsFiles()
        {
            yield return new object[] { "Directory.Build.props" };
            yield return new object[] { Path.Combine("tests", "Directory.Build.props") };
        }

        [Theory]
        [MemberData(nameof(SharedPropsFiles))]
        public void SharedBuildProps_ExposeConditionalBannerlordPathOverrides(string relativePath)
        {
            var document = XDocument.Load(Path.Combine(FindRepoRoot(), relativePath));
            var properties = document.Descendants()
                .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
                .ToDictionary(element => element.Name.LocalName, element => element, StringComparer.Ordinal);

            AssertConditionalProperty(properties, "BannerlordGameRoot");
            AssertConditionalProperty(properties, "BannerlordGameBin", "$(BannerlordGameRoot)\\bin\\Win64_Shipping_Client");
            AssertConditionalProperty(properties, "BannerlordModulesRoot", "$(BannerlordGameRoot)\\Modules");
            AssertConditionalProperty(properties, "BannerlordWorkshopRoot");
            AssertConditionalProperty(properties, "UIExtenderExPath");
            AssertConditionalProperty(properties, "MCMv5Path");
        }

        [Theory]
        [InlineData("SettlementAutomationCore")]
        [InlineData("TradeOptimizer")]
        [InlineData("PartyManager")]
        [InlineData("EquipmentManager")]
        [InlineData("FiefManager")]
        [InlineData("SmithingOptimizer")]
        public void ModulePostBuildDeploysThroughSharedModulesRoot(string moduleName)
        {
            string projectPath = Path.Combine(FindRepoRoot(), moduleName, $"{moduleName}.csproj");
            var document = XDocument.Load(projectPath);
            var postBuildTarget = document.Descendants("Target")
                .FirstOrDefault(element => string.Equals((string?)element.Attribute("Name"), "PostBuild", StringComparison.Ordinal));

            Assert.NotNull(postBuildTarget);
            var modFolder = postBuildTarget!
                .Descendants("ModFolder")
                .Single()
                .Value;

            Assert.Equal($"$(BannerlordModulesRoot)\\{moduleName}", modFolder);
        }

        [Fact]
        public void DependencyCleanupTarget_UsesSharedModulesRoot()
        {
            string targetsPath = Path.Combine(FindRepoRoot(), "Directory.Build.targets");
            var document = XDocument.Load(targetsPath);

            var cleanupTarget = document.Descendants("Target")
                .First(element => string.Equals((string?)element.Attribute("Name"), "RemoveInstalledModuleDependencyDlls", StringComparison.Ordinal));
            var modulesRoot = cleanupTarget.Descendants("BannerlordModulesRoot").Single();

            Assert.Contains("$(BannerlordGameRoot)\\Modules", modulesRoot.Value);
            Assert.Contains("'$(BannerlordModulesRoot)' == ''", (string?)modulesRoot.Attribute("Condition") ?? "");
        }

        [Fact]
        public void PublishScript_AcceptsAndForwardsBannerlordPathOverrides()
        {
            string script = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "publish.ps1"));

            Assert.Contains("[string]$BannerlordGameRoot", script);
            Assert.Contains("[string]$BannerlordModulesRoot", script);
            Assert.Contains("[string]$UIExtenderExPath", script);
            Assert.Contains("[string]$MCMv5Path", script);
            Assert.Contains("-p:BannerlordGameRoot=\"$BannerlordGameRoot\"", script);
            Assert.Contains("-p:BannerlordModulesRoot=\"$BannerlordModulesRoot\"", script);
            Assert.Contains("-p:UIExtenderExPath=\"$UIExtenderExPath\"", script);
            Assert.Contains("-p:MCMv5Path=\"$MCMv5Path\"", script);
            Assert.DoesNotContain("-p:UIExtenderExPath=\"E:\\", script);
            Assert.DoesNotContain("-p:MCMv5Path=\"E:\\", script);
        }

        private static void AssertConditionalProperty(
            IReadOnlyDictionary<string, XElement> properties,
            string propertyName,
            string? expectedValue = null)
        {
            Assert.True(properties.TryGetValue(propertyName, out var property), $"Missing {propertyName}.");
            Assert.Contains($"'$({propertyName})' == ''", (string?)property.Attribute("Condition") ?? "");
            if (expectedValue != null)
            {
                Assert.Equal(expectedValue, property.Value);
            }
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
