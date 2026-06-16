namespace SettlementAutomationCore
{
    public enum RejectedOrderReason
    {
        InvalidQuantity,
        GoldReserveReached,
        UnsupportedQuantityMode,
        NoMarketCandidates,
        CandidateNotFromMerchantInventory,
        MerchantStockMissing,
        NoMatchingMerchantStock,
        CandidateItemMissing,
        PricePolicyExceeded,
        GoldReserveBreach,
        CargoCapacityExceeded,
        HerdingLimitExceeded,
        NoAffordableMatchingStock
    }

    public sealed class RejectedOrderDetail
    {
        public RejectedOrderDetail(
            string settlementName,
            AutomationRequest request,
            RejectedOrderReason reason,
            string detail)
        {
            SettlementName = settlementName;
            Request = request;
            Reason = reason;
            Detail = detail ?? "";
        }

        public string SettlementName { get; }
        public AutomationRequest Request { get; }
        public RejectedOrderReason Reason { get; }
        public string Detail { get; }
    }
}
