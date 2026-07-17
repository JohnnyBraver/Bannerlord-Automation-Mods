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
            Assert.Contains("RequestOptimization(OptimizationTrigger.AutoHeroChanged)", source);
            Assert.Contains("RequestOptimization(OptimizationTrigger.AutoWeaponCategoryChanged)", source);
            Assert.Contains("RequestOptimization(OptimizationTrigger.AutoOrderModeEntered)", source);
            Assert.Contains("RequestOptimization(OptimizationTrigger.Manual)", source);
            Assert.Contains("RunOptimization(context)", source);
            Assert.Contains("Interlocked.Exchange(ref _isOptimizing, 1)", source);
            Assert.Contains("ShowMessages => Trigger == OptimizationTrigger.Manual || Trigger == OptimizationTrigger.AutoOrderSelected", source);
        }

        [Fact]
        public void SelectedOrders_UseTheSharedPipelineAndGameResultCheck()
        {
            string patches = ReadSource("SmithingOptimizer", "CraftingPatches.cs");
            string engine = ReadSource("SmithingOptimizer", "OptimizerEngine.cs");
            string settings = ReadSource("SmithingOptimizer", "Settings.cs");

            Assert.Contains("OnCraftingOrderSelected", patches);
            Assert.Contains("RequestOptimization(OptimizationTrigger.AutoOrderSelected)", patches);
            Assert.Contains("OptimizeOrder(", patches);
            Assert.Contains("HasAvailableHeroes", patches);
            Assert.Contains("OrderMinimumCompletionChance", settings);
            Assert.Contains("AutoOptimizeCraftingOrders", settings);
            Assert.Contains("GetOrderResult", engine);
            Assert.Contains("_currentItemModifier", engine);
            Assert.Contains("IsOrderCandidateBetter", engine);
        }

        [Fact]
        public void SmithingXpPerStamina_RetainsTrainingRecommendations()
        {
            string patches = ReadSource("SmithingOptimizer", "CraftingPatches.cs");
            string advisor = ReadSource("SmithingOptimizer", "SmithingTrainingAdvisor.cs");

            Assert.Contains("MaybeRecommendTrainingAlternative", patches);
            Assert.Contains("OptimizationGoal.SmithingXp", patches);
            Assert.Contains("OptimizationEfficiency.PerStamina", patches);
            Assert.Contains("FindBetterAlternative", patches);
            Assert.Contains("GetSkillXpForSmelting", advisor);
            Assert.Contains("GetRefiningFormulas", advisor);
            Assert.Contains("GetEnergyCostForRefining", advisor);
            Assert.Contains("XpPerStamina => RawXp / (float)Math.Max(1, StaminaCost)", advisor);
            Assert.Contains("SmithingTrainingRecommendation? best", advisor);
            Assert.Contains("return best", advisor);
            Assert.Contains("OptimizationEfficiency.PerMaterialValue", advisor);
            Assert.Contains("GetCraftingXpScore", patches);
            Assert.Contains("GetXpBasisText", patches);
        }

        [Fact]
        public void SellValue_ConsidersOtherTemplatesAndCachesUnchangedPartSets()
        {
            string patches = ReadSource("SmithingOptimizer", "CraftingPatches.cs");
            string engine = ReadSource("SmithingOptimizer", "OptimizerEngine.cs");
            Assert.Contains("_primaryUsages", patches);
            Assert.Contains("SellValueCache", patches);
            Assert.Contains("GetUnlockedPartFingerprint", patches);
            Assert.Contains("GetMaterialFingerprint", patches);
            Assert.Contains("GetSmithingCapabilityFingerprint", patches);
            Assert.DoesNotContain("context.Crafter.StringId", patches);
            Assert.Contains("PresentBetterTemplateAlternative", patches);
            Assert.Contains("Kept the current weapon type", patches);
            Assert.Contains("OptimizationGoal.SmithingXp", patches);
            Assert.Contains("UpdateCraftingHero", patches);
            Assert.Contains("GenerateEvaluationItem", engine);
            Assert.Contains("InitializePreCraftedWeaponOnLoad", engine);
            Assert.DoesNotContain("GetCurrentCraftedItemObject", engine);
            Assert.DoesNotContain("CreateCraftedWeaponInFreeBuildMode", engine);
            Assert.Contains("hasEligibleCacheEntry", patches);
            Assert.Contains("OptimizerEngine.IsCandidateEligible(cached.Best, template, behavior)", patches);
            Assert.Contains("Weapon-type recommendation", patches);
            Assert.Contains("FormatCandidateMetric", engine);
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
            Assert.Contains("BindingFlags.Instance | BindingFlags.NonPublic", engine);
            Assert.Contains("Invoke(Campaign.Current.Models.SmithingModel", engine);
            Assert.Contains("SupportedCampaignSystemMvid", engine);
            Assert.Contains("GetEnergyCostForSmithing(item, crafter)", engine);
            Assert.Contains("GetMaterialReferenceValue", engine);
            Assert.Contains("ApplyEfficiency", engine);
            Assert.Contains("damageMinimumQualityChance > 0", engine);
            Assert.Contains("goal == OptimizationGoal.Damage", engine);
            Assert.Contains("? rawScore", engine);
            Assert.Contains("{damageMinimumQuality}+Chance={candidate.QualityChance:P0}", engine);
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

        [Fact]
        public void Optimizer_RejectsLockedOrCrossTemplateCurrentDesigns()
        {
            string engine = ReadSource("SmithingOptimizer", "OptimizerEngine.cs");

            Assert.Contains("ReferenceEquals(originalDesign?.Template, template)", engine);
            Assert.Contains("IsCandidateEligible(current, template, behavior)", engine);
            Assert.Contains("behavior.IsOpened(piece, template)", engine);
            Assert.Contains("ReferenceEquals(piece, GetInvalidPiece(expectedType))", engine);
            Assert.Contains("foreach (var templatePiece in template.Pieces)", engine);
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
