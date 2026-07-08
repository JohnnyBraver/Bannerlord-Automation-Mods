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
            Assert.Contains("TriggerOptimization(silentOnNoImprovement: false, onlyIfCurrentDesignMatchesOptimizer: false)", craftingPatches);
        }

        [Fact]
        public void SmithingOptimizerButtonCommandIsRegisteredOnCraftingScreenViewModel()
        {
            string root = FindRepoRoot();
            string prefab = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "GUI", "PrefabExtensions", "SmithingOptimizerOptimize.xml"));
            string mixin = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "UIExtensions", "CraftingVMMixin.cs"));

            Assert.Contains("Command.Click=\"ExecuteSmithingOptimize\"", prefab);
            Assert.Contains("BaseViewModelMixin<CraftingVM>", mixin);
            Assert.Contains("void ExecuteSmithingOptimize()", mixin);
            Assert.Contains("CraftingPatches.ManualTrigger();", mixin);
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
            Assert.Contains("settings.AutoSwitchEnabled", smithingPatches);
            Assert.Contains("!settings.AutoBuySmithingSupplies", smithingProvider);
        }

        [Fact]
        public void SmithingOptimizerAutoOptimizeCoversForgeLifecycleAndCrafting()
        {
            string root = FindRepoRoot();
            string patches = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "CraftingPatches.cs"));
            string settings = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "Settings.cs"));

            Assert.Contains("Auto-optimize in Forge", settings);
            Assert.Contains("public bool AutoSwitchEnabled { get; set; } = true;", settings);
            Assert.Contains("TriggerAutoOptimization();", ExtractMethod(patches, "OnWeaponDesignVMConstructed"));
            Assert.Contains("TriggerAutoOptimization();", ExtractMethod(patches, "OnCraftingLogicRefreshedPostfix"));
            Assert.Contains("TriggerAutoOptimization();", ExtractMethod(patches, "UpdateCraftingHeroPostfix"));
            Assert.DoesNotContain("ExecuteFinalizeCraftingPrefix", patches);
            Assert.Contains("[HarmonyPostfix]", patches);
            Assert.Contains("TriggerAutoOptimization(onlyIfCurrentDesignMatchesOptimizer: true)", ExtractMethod(patches, "ExecuteFinalizeCraftingPostfix"));
            Assert.Contains("settings.AutoSwitchEnabled", patches);
            Assert.Contains("_isOptimizing", patches);
            Assert.Contains("_lastOptimizerFingerprint", patches);
            Assert.Contains("CurrentDesignMatchesLastOptimizerDesign", patches);
            Assert.Contains("GetCurrentDesignFingerprint", patches);
            Assert.Contains("ScalePercentage", patches);
        }

        [Fact]
        public void SmithingOptimizerScoresRawXpForSelectedCrafterWithoutLearningRate()
        {
            string root = FindRepoRoot();
            string patches = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "CraftingPatches.cs"));
            string engine = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "OptimizerEngine.cs"));
            string estimator = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "SmithingScoreEstimator.cs"));
            string settings = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "Settings.cs"));

            Assert.Contains("OptimizationGoal.XpEfficiency", settings);
            Assert.Contains("XP Efficiency (Raw XP/Stamina)", settings);
            Assert.DoesNotContain("Profit (XP + Sell Value)", settings);

            Assert.Contains("var crafter = ResolveActiveCrafter(craftingLogic);", patches);
            Assert.DoesNotContain("Hero.MainHero,", patches);
            Assert.Contains("ActiveCraftingVM?.CurrentCraftingHero?.Hero", patches);
            Assert.Contains("GetActiveCraftingHero()", patches);
            Assert.Contains("return Hero.MainHero;", patches);

            Assert.Contains("SmithingScoreEstimator.ScoreCraftingItem(item, design, crafter)", engine);
            Assert.Contains("GetSkillXpForSmithingInFreeBuildMode", estimator);
            Assert.Contains("GetEnergyCostForSmithing(item, crafter)", estimator);
            Assert.Contains("crafter.GetSkillValue(DefaultSkills.Crafting)", estimator);
            Assert.Contains("crafter.GetPerkValue(perk)", estimator);
            Assert.Contains("DefaultPerks.Crafting.PracticalRefiner", estimator);

            Assert.DoesNotContain("Learning", patches);
            Assert.DoesNotContain("Learning", engine);
            Assert.DoesNotContain("Learning", estimator);
        }

        [Fact]
        public void SmithingOptimizerSellValueUsesCrafterAdjustedCraftingScore()
        {
            string root = FindRepoRoot();
            string patches = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "CraftingPatches.cs"));
            string engine = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "OptimizerEngine.cs"));
            string estimator = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "SmithingScoreEstimator.cs"));

            Assert.Contains("else if (goal == OptimizationGoal.SellValue)", engine);
            Assert.Contains("candidate.Value = craftingScore?.Value ?? value;", engine);
            Assert.Contains("candidate.Score = candidate.Value;", engine);
            Assert.Contains("GetCraftedWeaponModifier(design, crafter)", estimator);
            Assert.Contains("new EquipmentElement(item, modifier, null, false)", estimator);
            Assert.Contains("return equipmentElement.ItemValue;", estimator);
            Assert.Contains("var currentScore = SmithingScoreEstimator.ScoreCraftingItem(currentItem, currentDesign, crafter);", patches);
            Assert.Contains("int currentValue = currentScore?.Value ?? currentItem.Value;", patches);
            Assert.Contains("Expected Value", patches);
        }

        [Fact]
        public void SmithingOptimizerComparesSmeltingAndRefiningWithoutExecutingThem()
        {
            string root = FindRepoRoot();
            string patches = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "CraftingPatches.cs"));
            string estimator = File.ReadAllText(Path.Combine(root, "SmithingOptimizer", "SmithingScoreEstimator.cs"));

            Assert.Contains("MaybeRecommendTrainingAlternative", patches);
            Assert.Contains("FindBestAlternative(crafter)", patches);
            Assert.Contains("GetSkillXpForSmelting", estimator);
            Assert.Contains("GetSkillXpForRefining", estimator);
            Assert.Contains("GetEnergyCostForSmelting(item, crafter)", estimator);
            Assert.Contains("GetEnergyCostForRefining(ref formulaForStamina, crafter)", estimator);
            Assert.Contains("GetRefiningFormulas(crafter)", estimator);
            Assert.Contains("roster.GetItemNumber(materialItem)", estimator);

            Assert.DoesNotContain("DoSmelting(", patches);
            Assert.DoesNotContain("DoSmelting(", estimator);
            Assert.DoesNotContain("DoRefinement(", patches);
            Assert.DoesNotContain("DoRefinement(", estimator);
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
