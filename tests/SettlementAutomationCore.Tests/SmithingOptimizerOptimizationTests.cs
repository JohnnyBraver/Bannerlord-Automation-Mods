using System;
using System.IO;
using SmithingOptimizer;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class SmithingOptimizerOptimizationTests
    {
        [Fact]
        public void OptimizationGoal_PreservesLegacyValuesAndAddsSmithingXp()
        {
            Assert.Equal(0, (int)OptimizationGoal.SellValue);
            Assert.Equal(1, (int)OptimizationGoal.Damage);
            Assert.Equal(2, (int)OptimizationGoal.SmithingXp);
            Assert.Equal(0, (int)OptimizationEfficiency.Raw);
            Assert.Equal(1, (int)OptimizationEfficiency.PerStamina);
            Assert.Equal(2, (int)OptimizationEfficiency.PerMaterialValue);
        }

        [Fact]
        public void ManualAndAutoTriggers_UseTheSameRequestPipeline()
        {
            string source = ReadSource("SmithingOptimizer", "CraftingPatches.cs");

            Assert.Contains("RequestOptimization(OptimizationTrigger.AutoUnlock)", source);
            Assert.Contains("RequestOptimization(OptimizationTrigger.Manual)", source);
            Assert.Contains("RunOptimization(context)", source);
            Assert.Contains("Interlocked.Exchange(ref _isOptimizing, 1)", source);
            Assert.Contains("ShowMessages => Trigger == OptimizationTrigger.Manual", source);
        }

        [Fact]
        public void Optimizer_UsesActiveSmithDifficultyAndExactFreeBuildXpModel()
        {
            string patches = ReadSource("SmithingOptimizer", "CraftingPatches.cs");
            string engine = ReadSource("SmithingOptimizer", "OptimizerEngine.cs");

            Assert.Contains("GetActiveCraftingHero()", patches);
            Assert.DoesNotContain("Hero.MainHero", patches);
            Assert.Contains("CalculateWeaponDesignDifficulty(design)", engine);
            Assert.Contains("GetSkillXpForSmithingInFreeBuildMode(item)", engine);
            Assert.Contains("GetModifierQualityProbabilities", engine);
            Assert.Contains("SupportedCampaignSystemMvid", engine);
            Assert.Contains("GetEnergyCostForSmithing(item, crafter)", engine);
            Assert.Contains("GetMaterialReferenceValue", engine);
            Assert.Contains("ApplyEfficiency", engine);
            Assert.Contains("damageMinimumQualityChance > 0", engine);
        }

        [Fact]
        public void Optimizer_UsesBoundedCoordinateSearchInsteadOfCartesianGrid()
        {
            string source = ReadSource("SmithingOptimizer", "OptimizerEngine.cs");

            Assert.Contains("MaximumPartPasses = 3", source);
            Assert.Contains("for (int slot = 0; slot < slots.Length; slot++)", source);
            Assert.DoesNotContain("foreach (var blade in blades)", source);
            Assert.DoesNotContain("OrderByDescending", source);
            Assert.Contains("finally", source);
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
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Automation-Mods repository root.");
        }
    }
}
