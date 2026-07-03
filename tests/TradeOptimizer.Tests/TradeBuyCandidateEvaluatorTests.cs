using Xunit;

namespace TradeOptimizer.Tests
{
    public class TradeBuyCandidateEvaluatorTests
    {
        [Theory]
        [InlineData("category", "CategoryPolicy")]
        [InlineData("stock", "MerchantStockDepleted")]
        [InlineData("herding", "HerdingLimitExceeded")]
        [InlineData("cargo", "Overburdened")]
        [InlineData("budget", "BudgetProtection")]
        [InlineData("same-stop", "SameStopExclusion")]
        [InlineData("average", "AveragePriceUndetermined")]
        [InlineData("price", "PriceThreshold")]
        [InlineData("profit", "NoProfitExpected")]
        public void Evaluate_ReturnsTypedBlockReasonForEachGate(string scenario, string expected)
        {
            var input = ViableInput();
            switch (scenario)
            {
                case "category":
                    input.CategoryAllowed = false;
                    break;
                case "stock":
                    input.BoughtSoFar = input.InitialMerchantCount;
                    break;
                case "herding":
                    input.IsAnimalOrMount = true;
                    input.AnimalsBoughtSoFar = input.RemainingAnimalSlots;
                    break;
                case "cargo":
                    input.FreeCargo = 0.1f;
                    input.ItemWeight = 2f;
                    break;
                case "budget":
                    input.CurrentBalance = input.CurrentPrice - 1;
                    break;
                case "same-stop":
                    input.SoldInSameStop = true;
                    break;
                case "average":
                    input.AveragePrice = 0f;
                    break;
                case "price":
                    input.CurrentPrice = 90;
                    break;
                case "profit":
                    input.CurrentPrice = 100;
                    input.BuyPriceThresholdFactor = 1.10f;
                    break;
            }

            var decision = TradeBuyCandidateEvaluator.Evaluate(input);

            Assert.False(decision.Accepted);
            Assert.Equal(expected, decision.Reason.ToString());
        }

        [Fact]
        public void Evaluate_BuyCapPolicyNoneDisablesCountAndValueCaps()
        {
            var input = ViableInput();
            input.BuyCapPolicy = BuyCapPolicy.None;
            input.InitialMerchantCount = 2_000;
            input.BoughtSoFar = 999;
            input.CurrentlyOwned = 999;
            input.VisitSpentSoFar = 999_000;
            input.MaxStackSizeToBuy = 1;
            input.MaxStackValueToBuy = 1;

            var decision = TradeBuyCandidateEvaluator.Evaluate(input);

            Assert.True(decision.Accepted);
        }

        [Theory]
        [InlineData(BuyCapPolicy.Count, "StackCountCap")]
        [InlineData(BuyCapPolicy.Value, "StackValueCap")]
        [InlineData(BuyCapPolicy.Both, "StackCountCap")]
        public void Evaluate_BuyCapsOnlyBlockPurchases(BuyCapPolicy policy, string expected)
        {
            var input = ViableInput();
            input.BuyCapPolicy = policy;
            input.BoughtSoFar = 10;
            input.CurrentlyOwned = 10;
            input.VisitSpentSoFar = 500;
            input.MaxStackSizeToBuy = 10;
            input.MaxStackValueToBuy = 510;

            var decision = TradeBuyCandidateEvaluator.Evaluate(input);

            Assert.False(decision.Accepted);
            Assert.Equal(expected, decision.Reason.ToString());
            Assert.False(decision.RequiresSwap);
        }

        [Fact]
        public void Evaluate_ValueCapUsesProjectedNextPurchase()
        {
            var input = ViableInput();
            input.BuyCapPolicy = BuyCapPolicy.Value;
            input.BuyValueCapMode = BuyCapMode.PerVisit;
            input.VisitSpentSoFar = 480;
            input.CurrentPrice = 40;
            input.MaxStackValueToBuy = 500;

            var decision = TradeBuyCandidateEvaluator.Evaluate(input);

            Assert.False(decision.Accepted);
            Assert.Equal(TradeBlockReason.StackValueCap, decision.Reason);
            Assert.Contains("visit=520", decision.Details);
        }

        [Fact]
        public void Evaluate_MarginSwapRequiresExplicitBetterSwapCandidate()
        {
            var input = ViableInput();
            input.MarginSwappingEnabled = true;
            input.FreeCargo = 0.1f;
            input.ItemWeight = 2f;
            input.ProfitDensity = 20f;
            input.SwapCandidateAvailable = true;
            input.SwapProfitDensity = 5f;

            var decision = TradeBuyCandidateEvaluator.Evaluate(input);

            Assert.True(decision.Accepted);
            Assert.True(decision.RequiresSwap);
        }

        private static TradeBuyCandidateInput ViableInput()
        {
            return new TradeBuyCandidateInput
            {
                CategoryAllowed = true,
                InitialMerchantCount = 20,
                BoughtSoFar = 0,
                IsAnimalOrMount = false,
                AnimalsBoughtSoFar = 0,
                RemainingAnimalSlots = 50,
                EnforceCargoLimit = true,
                FreeCargo = 100f,
                ItemWeight = 1f,
                MarginSwappingEnabled = false,
                SwapCandidateAvailable = false,
                SwapProfitDensity = 0f,
                ProfitDensity = 60f,
                Stance = TradingStance.MaxProfit,
                CargoFullness = 0.25f,
                CargoLimitThreshold = 0.90f,
                GoodBuyLimit = 70f,
                BuyCapPolicy = BuyCapPolicy.None,
                BuyCountCapMode = BuyCapMode.PerVisit,
                BuyValueCapMode = BuyCapMode.PerVisit,
                CurrentlyOwned = 0,
                VisitSpentSoFar = 0,
                CurrentBalance = 1000,
                CurrentPrice = 40,
                AveragePrice = 100f,
                BuyPriceThresholdFactor = 0.80f,
                MaxStackSizeToBuy = 100,
                MaxStackValueToBuy = 2000,
                SoldInSameStop = false
            };
        }
    }
}
