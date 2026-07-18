using System;

namespace TradeOptimizer
{
    internal sealed class TradeBuyCandidateInput
    {
        public bool CategoryAllowed { get; set; } = true;
        public int InitialMerchantCount { get; set; }
        public int BoughtSoFar { get; set; }
        public bool IsAnimalOrMount { get; set; }
        public int AnimalsBoughtSoFar { get; set; }
        public int RemainingAnimalSlots { get; set; }
        public bool EnforceCargoLimit { get; set; }
        public float FreeCargo { get; set; }
        public float ItemWeight { get; set; }
        public bool MarginSwappingEnabled { get; set; }
        public bool SwapCandidateAvailable { get; set; }
        public float SwapProfitDensity { get; set; }
        public float ProfitDensity { get; set; }
        public TradingStance Stance { get; set; }
        public float CargoFullness { get; set; }
        public float CargoLimitThreshold { get; set; }
        public float GoodBuyLimit { get; set; }
        public BuyCapPolicy BuyCapPolicy { get; set; }
        public BuyCapMode BuyCountCapMode { get; set; }
        public BuyCapMode BuyValueCapMode { get; set; }
        public int CurrentlyOwned { get; set; }
        public int VisitSpentSoFar { get; set; }
        public int CurrentBalance { get; set; }
        public int CurrentPrice { get; set; }
        public float AveragePrice { get; set; }
        public float BuyPriceThresholdFactor { get; set; }
        public int MaxStackSizeToBuy { get; set; }
        public int MaxStackValueToBuy { get; set; }
        public bool SoldInSameStop { get; set; }
    }

    internal sealed class TradeBuyCandidateDecision
    {
        private TradeBuyCandidateDecision(TradeBlockReason reason, string details, bool requiresSwap)
        {
            Reason = reason;
            Details = details;
            RequiresSwap = requiresSwap;
        }

        public TradeBlockReason Reason { get; }
        public string Details { get; }
        public bool RequiresSwap { get; }
        public bool Accepted => Reason == TradeBlockReason.None;

        public static TradeBuyCandidateDecision Accept(bool requiresSwap = false)
        {
            return new TradeBuyCandidateDecision(TradeBlockReason.None, string.Empty, requiresSwap);
        }

        public static TradeBuyCandidateDecision Block(TradeBlockReason reason, string details = "")
        {
            return new TradeBuyCandidateDecision(reason, details, requiresSwap: false);
        }
    }

    internal static class TradeBuyCandidateEvaluator
    {
        public static TradeBuyCandidateDecision Evaluate(TradeBuyCandidateInput input)
        {
            if (!input.CategoryAllowed)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.CategoryPolicy);
            }

            if (input.BoughtSoFar >= input.InitialMerchantCount)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.MerchantStockDepleted);
            }

            if (input.IsAnimalOrMount && input.AnimalsBoughtSoFar >= input.RemainingAnimalSlots)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.HerdingLimitExceeded);
            }

            if (input.EnforceCargoLimit && input.FreeCargo < input.ItemWeight)
            {
                if (input.MarginSwappingEnabled)
                {
                    if (input.SwapCandidateAvailable && input.SwapProfitDensity < input.ProfitDensity)
                    {
                        return TradeBuyCandidateDecision.Accept(requiresSwap: true);
                    }

                    return TradeBuyCandidateDecision.Block(TradeBlockReason.MarginSwapNotBetter);
                }

                return TradeBuyCandidateDecision.Block(TradeBlockReason.Overburdened);
            }

            bool countCapEnabled = input.BuyCapPolicy == BuyCapPolicy.Count || input.BuyCapPolicy == BuyCapPolicy.Both;
            if (countCapEnabled)
            {
                int countBasis = input.BuyCountCapMode == BuyCapMode.PerVisit ? input.BoughtSoFar : input.CurrentlyOwned;
                if (countBasis >= input.MaxStackSizeToBuy)
                {
                    string scope = input.BuyCountCapMode == BuyCapMode.PerVisit ? "visit" : "inventory";
                    return TradeBuyCandidateDecision.Block(TradeBlockReason.StackCountCap, $"{scope}={countBasis}, max={input.MaxStackSizeToBuy}");
                }
            }

            bool valueCapEnabled = input.BuyCapPolicy == BuyCapPolicy.Value || input.BuyCapPolicy == BuyCapPolicy.Both;
            if (valueCapEnabled)
            {
                bool valueCapIsPerVisit = input.BuyValueCapMode == BuyCapMode.PerVisit;
                int projectedValue = valueCapIsPerVisit
                    ? input.VisitSpentSoFar + input.CurrentPrice
                    : (input.CurrentlyOwned + 1) * input.CurrentPrice;
                if (projectedValue > input.MaxStackValueToBuy)
                {
                    string scope = valueCapIsPerVisit ? "visit" : "inventory";
                    return TradeBuyCandidateDecision.Block(TradeBlockReason.StackValueCap, $"{scope}={projectedValue}, max={input.MaxStackValueToBuy}");
                }
            }

            if (input.CurrentBalance < input.CurrentPrice)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.BudgetProtection, $"available={input.CurrentBalance}, price={input.CurrentPrice}");
            }

            if (input.SoldInSameStop)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.SameStopExclusion);
            }

            if (input.AveragePrice <= 0f)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.AveragePriceUndetermined);
            }

            if (input.Stance == TradingStance.Balanced && input.CargoFullness >= input.CargoLimitThreshold && input.CurrentPrice > input.GoodBuyLimit)
            {
                return TradeBuyCandidateDecision.Block(
                    TradeBlockReason.CargoNearLimit,
                    $"fullness={input.CargoFullness:P0}, price={input.CurrentPrice} > limit={input.GoodBuyLimit:F1}");
            }

            if (!TradeCandidatePolicy.PassesBuyPriceThreshold(input.CurrentPrice, input.AveragePrice, input.BuyPriceThresholdFactor))
            {
                float ratio = input.AveragePrice > 0f ? (float)input.CurrentPrice / input.AveragePrice : 0f;
                return TradeBuyCandidateDecision.Block(
                    TradeBlockReason.PriceThreshold,
                    $"price={input.CurrentPrice}, limit={input.AveragePrice * input.BuyPriceThresholdFactor:F1}, ratio={ratio:P1}, threshold={input.BuyPriceThresholdFactor:P1}");
            }

            if (input.AveragePrice - input.CurrentPrice <= 0f)
            {
                return TradeBuyCandidateDecision.Block(TradeBlockReason.NoProfitExpected);
            }

            return TradeBuyCandidateDecision.Accept();
        }
    }
}
