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
            string source = ReadSource("TradeOptimizer", "TradingEngine.cs");

            Assert.Contains("MaxBuyLoopIterations", source);
            Assert.Contains("MaxBuySkipDiagnostics", source);
            Assert.Contains("[Buy Loop Guard]", source);
            Assert.Contains("Additional buy-skip diagnostics suppressed", source);
        }

        [Fact]
        public void TradingEngine_DoesNotUseDisplayNameForSameStopExclusion()
        {
            string source = ReadSource("TradeOptimizer", "TradingEngine.cs");

            Assert.DoesNotContain("localExcludedItems.Add(itemObj.Name.ToString())", source);
            Assert.DoesNotContain("localExcludedItems.Contains(itemObj.Name.ToString())", source);
            Assert.Contains("GetTradeIdentityKey", source);
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
        public void Provider_MapsNewPlayerSideStacksWhenReconstructingSimulatedBuys()
        {
            string source = ReadSource("TradeOptimizer", "TradeOptimizerProvider.cs");
            string collectOrders = SliceMethod(source, "private List<TradeOrder> SimulateAndCollectOrders");

            Assert.Contains("if (!eqElementMap.ContainsKey(key))", collectOrders);
            Assert.Contains("eqElementMap[key] = el.EquipmentElement;", collectOrders);
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
