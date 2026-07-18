using System;
using System.IO;
using Xunit;

namespace TradeOptimizer.Tests
{
    public class TradeOptimizerSafetyTests
    {
        [Fact]
        public void AnalyzeMarket_DoesNotRunSecondSellSimulationForSplitTransactions()
        {
            string source = ReadSource("TradeOptimizer", "TradeOptimizerProvider.cs");
            string analyzeMarket = SliceMethod(source, "public TradeProposal AnalyzeMarket");

            Assert.DoesNotContain("SimulateAndCollectOrders", analyzeMarket);
        }

        [Fact]
        public void TradingEngine_BoundsBuyLoopAndDiagnosticSpam()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");

            Assert.Contains("MaxBuyLoopIterations", source);
            Assert.Contains("MaxBuySkipDiagnostics", source);
            Assert.Contains("[Buy Loop Guard]", source);
            Assert.Contains("Additional buy-skip diagnostics suppressed", source);
        }


        [Fact]
        public void TradingEngine_KeepsMarginSwappingBehindExplicitSetting()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");
            string evaluator = ReadSource("TradeOptimizer", "TradeBuyCandidateEvaluator.cs");

            Assert.Contains("bool marginSwappingEnabled = settings.EnableMarginSwapping;", source);
            Assert.Contains("marginSwappingEnabled", source);
            Assert.Contains("SwapCandidatePair.Empty", source);
            Assert.Contains("FindWorstOwnedSwapCandidates", source);
            Assert.Contains("TradeBlockReason.Overburdened", evaluator);
        }


        [Fact]
        public void TradingEngine_GatesBuyCapsByPolicyAndMode()
        {
            string source = ReadSource("TradeOptimizer", "TradeBuyCandidateEvaluator.cs");

            Assert.Contains("input.BuyCapPolicy == BuyCapPolicy.Count || input.BuyCapPolicy == BuyCapPolicy.Both", source);
            Assert.Contains("input.BuyCapPolicy == BuyCapPolicy.Value || input.BuyCapPolicy == BuyCapPolicy.Both", source);
            Assert.Contains("input.BuyCountCapMode == BuyCapMode.PerVisit ? input.BoughtSoFar : input.CurrentlyOwned", source);
            Assert.Contains("input.VisitSpentSoFar + input.CurrentPrice", source);
            Assert.Contains("(input.CurrentlyOwned + 1) * input.CurrentPrice", source);
        }

        [Fact]
        public void TradingEngine_DoesNotUseDisplayNameForSameStopExclusion()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");

            Assert.DoesNotContain("localExcludedItems.Add(itemObj.Name.ToString())", source);
            Assert.DoesNotContain("localExcludedItems.Contains(itemObj.Name.ToString())", source);
            Assert.Contains("localExcludedItems.Add(stack.IdentityKey)", source);
            Assert.Contains("SoldInSameStop = localExcludedItems.Contains(stack.IdentityKey)", source);
        }

        [Fact]
        public void Provider_UsesCoreClonedPricingSessionInsteadOfLiveRosterRestore()
        {
            string source = ReadSource("TradeOptimizer", "TradeOptimizerProvider.cs");

            Assert.Contains("TradePricingSession.CreateSimulated", source);
            Assert.DoesNotContain("party.ItemRoster.Clear()", source);
            Assert.DoesNotContain("settlement.ItemRoster.Clear()", source);
            Assert.DoesNotContain("CreateAndInitInventoryLogic(party, settlement, false)", source);
        }

        [Fact]
        public void Provider_UsesPlannerRecordedActionsInsteadOfInventoryDiff()
        {
            string source = ReadSource("TradeOptimizer", "TradeOptimizerProvider.cs");
            string collectOrders = SliceMethod(source, "private List<TradeOrder> SimulateAndCollectOrders");
            string analyzeMarket = SliceMethod(source, "public TradeProposal AnalyzeMarket");

            Assert.Contains("TradingEngine.PlanOptimization", collectOrders);
            Assert.Contains("plan.ToTradeOrders()", collectOrders);
            Assert.Contains("TradingEngine.PlanOptimization", analyzeMarket);
            Assert.Contains("plan.ToTradeProposal()", analyzeMarket);
            Assert.DoesNotContain("finalPlayerCounts", collectOrders);
            Assert.DoesNotContain("finalPlayerCounts", analyzeMarket);
            Assert.DoesNotContain("eqElementMap", collectOrders);
            Assert.DoesNotContain("eqElementMap", analyzeMarket);
        }

        [Fact]
        public void Provider_HonorsSimulationModeBeforeReturningExecutableTrades()
        {
            string source = ReadSource("TradeOptimizer", "TradeOptimizerProvider.cs");
            string collectOrders = SliceMethod(source, "private List<TradeOrder> SimulateAndCollectOrders");
            string analyzeMarket = SliceMethod(source, "public TradeProposal AnalyzeMarket");

            Assert.Contains("if (settings.SimulationMode)", collectOrders);
            Assert.Contains("return orders;", collectOrders);
            Assert.Contains("if (settings.SimulationMode)", analyzeMarket);
            Assert.Contains("return new TradeProposal(actions);", analyzeMarket);
        }

        [Fact]
        public void TradePlanner_AppliesLootHandlingModes()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");

            Assert.Contains("settings.LootHandling == LootHandlingMode.Liquidate || settings.LootHandling == LootHandlingMode.XPFarm", source);
            Assert.Contains("settings.LootHandling != LootHandlingMode.Profit", source);
            Assert.Contains("LootHandlingMode.XPFarm) && sold > 0", source);
        }

        [Fact]
        public void TradePlanner_AppliesCategoryPolicyToXpFarmActivation()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");
            string activation = SliceMethod(source, "private static void PlanXpFarmActivation");

            Assert.Contains("TradeCandidatePolicy.CanTradeByMode", activation);
            Assert.Contains("isBuy: true", activation);
        }

        [Fact]
        public void TradePlanner_UsesTradeContextForAnimalHeadroom()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");

            Assert.Contains("RemainingAnimalSlots = request.TradeContext.FreeAnimalSlots", source);
            Assert.Contains("request.TradeContext.FreeAnimalSlots - state.TotalAnimalsBoughtInSim", source);
            Assert.DoesNotContain("HerdingCalculator.GetRemainingAnimalSlots", source);
        }

        private static string ReadSource(params string[] parts)
        {
            string root = FindRepoRoot();
            return File.ReadAllText(Path.Combine(root, Path.Combine(parts)));
        }

        private static string SliceMethod(string source, string marker)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Could not find marker: {marker}");

            int depth = 0;
            bool started = false;
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                    started = true;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (started && depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            throw new InvalidOperationException($"Could not slice method for marker: {marker}");
        }

        [Fact]
        public void TradingEngine_ImplementsCargoLimitThresholdAndGoodSellThreshold()
        {
            string source = ReadSource("TradeOptimizer", "TradePlanner.cs");

            Assert.Contains("CargoLimitThreshold", source);
            Assert.Contains("GoodSellThreshold", source);
            Assert.Contains("TradingStance.MaxProfit", source);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "SettlementAutomationCore")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "TradeOptimizer")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }
    }
}
