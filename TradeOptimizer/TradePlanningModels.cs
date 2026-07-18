using System;
using System.Collections.Generic;
using System.Linq;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TradeOptimizer
{
    internal enum TradePlanPhase
    {
        Sell,
        XpFarmActivation,
        DirectBuy,
        MarginSwap
    }

    internal enum PlannedTradeActionKind
    {
        Buy,
        Sell,
        Slaughter
    }

    internal enum TradeBlockReason
    {
        None,
        CategoryPolicy,
        MerchantStockDepleted,
        NotSellable,
        UnknownReferencePrice,
        LootLockedForXpFarm,
        SellBuyConflict,
        HerdingLimitExceeded,
        Overburdened,
        CargoNearLimit,
        StackCountCap,
        StackValueCap,
        BudgetProtection,
        SameStopExclusion,
        AveragePriceUndetermined,
        PriceThreshold,
        NoProfitExpected,
        MarginSwapNotBetter
    }

    internal sealed class TradePlanningRequest
    {
        public TradePlanningRequest(
            SPInventoryVM inventoryVm,
            bool runSellPhase,
            bool runBuyPhase,
            TradeContext tradeContext,
            Settings settings,
            IEnumerable<string>? excludedItemKeys,
            bool applyTransfers)
        {
            InventoryVm = inventoryVm ?? throw new ArgumentNullException(nameof(inventoryVm));
            RunSellPhase = runSellPhase;
            RunBuyPhase = runBuyPhase;
            TradeContext = tradeContext ?? throw new ArgumentNullException(nameof(tradeContext));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ExcludedItemKeys = new HashSet<string>(excludedItemKeys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            ApplyTransfers = applyTransfers;
        }

        public SPInventoryVM InventoryVm { get; }
        public bool RunSellPhase { get; }
        public bool RunBuyPhase { get; }
        public TradeContext TradeContext { get; }
        public Settings Settings { get; }
        public ISet<string> ExcludedItemKeys { get; }
        public bool ApplyTransfers { get; }
    }

    internal sealed class TradeMarketStack
    {
        public TradeMarketStack(
            SPItemVM itemVm,
            ItemRosterElement rosterElement,
            EquipmentElement equipmentElement,
            ItemObject item,
            string identityKey,
            int initialCount,
            float buyReferencePrice,
            float sellReferencePrice)
        {
            ItemVm = itemVm;
            RosterElement = rosterElement;
            EquipmentElement = equipmentElement;
            Item = item;
            IdentityKey = identityKey;
            InitialCount = initialCount;
            BuyReferencePrice = buyReferencePrice;
            SellReferencePrice = sellReferencePrice;
        }

        public SPItemVM ItemVm { get; }
        public ItemRosterElement RosterElement { get; }
        public EquipmentElement EquipmentElement { get; }
        public ItemObject Item { get; }
        public string IdentityKey { get; }
        public string Name => Item.Name?.ToString() ?? string.Empty;
        public int InitialCount { get; }
        public float Weight => Item.Weight;
        public float BuyReferencePrice { get; }
        public float SellReferencePrice { get; }
    }

    internal sealed class TradeMarketSnapshot
    {
        private TradeMarketSnapshot(
            IReadOnlyList<TradeMarketStack> playerStacks,
            IReadOnlyList<TradeMarketStack> merchantStacks,
            IReadOnlyDictionary<string, float> buyReferencePrices,
            IReadOnlyDictionary<string, float> sellReferencePrices)
        {
            PlayerStacks = playerStacks;
            MerchantStacks = merchantStacks;
            BuyReferencePrices = buyReferencePrices;
            SellReferencePrices = sellReferencePrices;
        }

        public IReadOnlyList<TradeMarketStack> PlayerStacks { get; }
        public IReadOnlyList<TradeMarketStack> MerchantStacks { get; }
        public IReadOnlyDictionary<string, float> BuyReferencePrices { get; }
        public IReadOnlyDictionary<string, float> SellReferencePrices { get; }

        public static TradeMarketSnapshot Capture(SPInventoryVM vm, InventoryLogic? logic)
        {
            var playerStacks = new List<TradeMarketStack>();
            var merchantStacks = new List<TradeMarketStack>();
            var buyReferencePrices = new Dictionary<string, float>(StringComparer.Ordinal);
            var sellReferencePrices = new Dictionary<string, float>(StringComparer.Ordinal);

            if (vm.RightItemListVM != null)
            {
                foreach (var item in vm.RightItemListVM)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null)
                    {
                        continue;
                    }

                    var equipmentElement = item.ItemRosterElement.EquipmentElement;
                    var itemObj = equipmentElement.Item;
                    string key = TradingEngine.GetTradeIdentityKey(equipmentElement);
                    float sellReference = PricingService.GetReferencePrice(
                        logic,
                        equipmentElement,
                        item.ItemRosterElement,
                        0f,
                        isSelling: true);
                    float buyReference = PricingService.GetReferencePrice(
                        logic,
                        equipmentElement,
                        null,
                        0f,
                        isSelling: false);

                    sellReferencePrices[key] = sellReference;
                    buyReferencePrices[key] = buyReference;
                    playerStacks.Add(new TradeMarketStack(
                        item,
                        item.ItemRosterElement,
                        equipmentElement,
                        itemObj,
                        key,
                        item.ItemCount,
                        buyReference,
                        sellReference));
                }
            }

            if (vm.LeftItemListVM != null)
            {
                foreach (var item in vm.LeftItemListVM)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null)
                    {
                        continue;
                    }

                    var equipmentElement = item.ItemRosterElement.EquipmentElement;
                    var itemObj = equipmentElement.Item;
                    string key = TradingEngine.GetTradeIdentityKey(equipmentElement);
                    if (!buyReferencePrices.TryGetValue(key, out float buyReference))
                    {
                        buyReference = PricingService.GetReferencePrice(
                            logic,
                            equipmentElement,
                            null,
                            0f,
                            isSelling: false);
                        buyReferencePrices[key] = buyReference;
                    }

                    sellReferencePrices.TryGetValue(key, out float sellReference);
                    merchantStacks.Add(new TradeMarketStack(
                        item,
                        item.ItemRosterElement,
                        equipmentElement,
                        itemObj,
                        key,
                        item.ItemCount,
                        buyReference,
                        sellReference));
                }
            }

            return new TradeMarketSnapshot(playerStacks, merchantStacks, buyReferencePrices, sellReferencePrices);
        }

        public float GetBuyReferencePrice(string identityKey)
        {
            return BuyReferencePrices.TryGetValue(identityKey, out float price) ? price : 0f;
        }

        public float GetSellReferencePrice(string identityKey)
        {
            return SellReferencePrices.TryGetValue(identityKey, out float price) ? price : 0f;
        }
    }

    internal sealed class TradeSimulationState
    {
        private readonly Dictionary<string, int> _boughtQuantities = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _buyGoldByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _soldQuantities = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _extraSoldQuantities = new Dictionary<string, int>(StringComparer.Ordinal);

        public TradeSimulationState(int currentBalance, float startWeight, float usableCapacity)
        {
            CurrentBalance = currentBalance;
            StartWeight = startWeight;
            UsableCapacity = usableCapacity;
        }

        public int CurrentBalance { get; set; }
        public float StartWeight { get; }
        public float UsableCapacity { get; }
        public float NetWeightAdded { get; set; }
        public int TotalAnimalsBoughtInSim { get; set; }

        public int GetBoughtCount(string identityKey)
        {
            return _boughtQuantities.TryGetValue(identityKey, out int count) ? count : 0;
        }

        public int GetBuyGold(string identityKey)
        {
            return _buyGoldByKey.TryGetValue(identityKey, out int gold) ? gold : 0;
        }

        public int GetSoldCount(string identityKey)
        {
            return _soldQuantities.TryGetValue(identityKey, out int count) ? count : 0;
        }

        public int GetExtraSoldCount(string identityKey)
        {
            return _extraSoldQuantities.TryGetValue(identityKey, out int count) ? count : 0;
        }

        public int GetOwnedCount(TradeMarketSnapshot snapshot, string identityKey)
        {
            int baseOwned = snapshot.PlayerStacks
                .Where(stack => stack.IdentityKey == identityKey)
                .Sum(stack => stack.InitialCount);
            return baseOwned + GetBoughtCount(identityKey) - GetSoldCount(identityKey) - GetExtraSoldCount(identityKey);
        }

        public void RecordBought(string identityKey, int quantity, int gold)
        {
            _boughtQuantities[identityKey] = GetBoughtCount(identityKey) + quantity;
            _buyGoldByKey[identityKey] = GetBuyGold(identityKey) + gold;
        }

        public void RecordSold(string identityKey, int quantity)
        {
            _soldQuantities[identityKey] = GetSoldCount(identityKey) + quantity;
        }

        public void RecordExtraSold(string identityKey, int quantity)
        {
            _extraSoldQuantities[identityKey] = GetExtraSoldCount(identityKey) + quantity;
        }
    }

    internal sealed class PlannedTradeAction
    {
        public PlannedTradeAction(
            PlannedTradeActionKind kind,
            TradePlanPhase phase,
            EquipmentElement equipmentElement,
            string identityKey,
            string itemName,
            int quantity,
            int gold,
            int startPrice,
            int endPrice,
            float referencePrice,
            float profitDensity,
            bool isLoot = false,
            bool isMarginSwap = false)
        {
            Kind = kind;
            Phase = phase;
            EquipmentElement = equipmentElement;
            IdentityKey = identityKey;
            ItemName = itemName;
            Quantity = quantity;
            Gold = gold;
            StartPrice = startPrice;
            EndPrice = endPrice;
            ReferencePrice = referencePrice;
            ProfitDensity = profitDensity;
            IsLoot = isLoot;
            IsMarginSwap = isMarginSwap;
        }

        public PlannedTradeActionKind Kind { get; }
        public TradePlanPhase Phase { get; }
        public EquipmentElement EquipmentElement { get; }
        public string IdentityKey { get; }
        public string ItemName { get; }
        public int Quantity { get; set; }
        public int Gold { get; set; }
        public int StartPrice { get; }
        public int EndPrice { get; set; }
        public float ReferencePrice { get; }
        public float ProfitDensity { get; }
        public bool IsLoot { get; }
        public bool IsMarginSwap { get; }
    }

    internal sealed class TradeDecisionTrace
    {
        public TradeDecisionTrace(
            TradePlanPhase phase,
            string identityKey,
            string itemName,
            TradeBlockReason reason,
            string details,
            int currentPrice,
            float referencePrice)
        {
            Phase = phase;
            IdentityKey = identityKey;
            ItemName = itemName;
            Reason = reason;
            Details = details;
            CurrentPrice = currentPrice;
            ReferencePrice = referencePrice;
        }

        public TradePlanPhase Phase { get; }
        public string IdentityKey { get; }
        public string ItemName { get; }
        public TradeBlockReason Reason { get; }
        public string Details { get; }
        public int CurrentPrice { get; }
        public float ReferencePrice { get; }
    }

    internal sealed class TradePlan
    {
        private readonly List<PlannedTradeAction> _actions = new List<PlannedTradeAction>();
        private readonly List<TradeDecisionTrace> _blockedCandidates = new List<TradeDecisionTrace>();

        public IReadOnlyList<PlannedTradeAction> Actions => _actions;
        public IReadOnlyList<TradeDecisionTrace> BlockedCandidates => _blockedCandidates;

        public void RecordAction(PlannedTradeAction action)
        {
            var existing = _actions.FirstOrDefault(candidate =>
                candidate.Kind == action.Kind &&
                candidate.Phase == action.Phase &&
                candidate.IdentityKey == action.IdentityKey &&
                candidate.IsMarginSwap == action.IsMarginSwap);

            if (existing == null)
            {
                _actions.Add(action);
                return;
            }

            existing.Quantity += action.Quantity;
            existing.Gold += action.Gold;
            existing.EndPrice = action.EndPrice;
        }

        public void RecordBlock(TradeDecisionTrace trace)
        {
            if (trace.Reason != TradeBlockReason.None)
            {
                _blockedCandidates.Add(trace);
            }
        }

        public TradeTransactionReport ToTransactionReport()
        {
            var report = new TradeTransactionReport();
            foreach (var group in _actions.GroupBy(action => new { action.Kind, action.IdentityKey }))
            {
                var first = group.First();
                int quantity = group.Sum(action => action.Quantity);
                int gold = group.Sum(action => action.Gold);
                float referencePrice = first.ReferencePrice;

                if (first.Kind == PlannedTradeActionKind.Buy)
                {
                    report.BoughtItems.Add(new TradedItemInfo
                    {
                        Name = first.ItemName,
                        Count = quantity,
                        Gold = gold,
                        MarketPrice = referencePrice
                    });
                }
                else if (first.Kind == PlannedTradeActionKind.Sell)
                {
                    report.SoldItems.Add(new TradedItemInfo
                    {
                        Name = first.ItemName,
                        Count = quantity,
                        Gold = gold,
                        MarketPrice = referencePrice
                    });

                    if (group.Any(action => !action.IsLoot || action.IsMarginSwap))
                    {
                        report.SoldNormalItems.Add(first.ItemName);
                    }
                }
                else if (first.Kind == PlannedTradeActionKind.Slaughter)
                {
                    report.ArbitrageSlaughters.Add((first.EquipmentElement, quantity));
                }
            }

            return report;
        }

        public List<TradeOrder> ToTradeOrders()
        {
            return ToAggregatedActions()
                .Select(action => new TradeOrder(
                    action.EquipmentElement,
                    action.Quantity,
                    action.Kind == PlannedTradeActionKind.Buy,
                    action.Kind == PlannedTradeActionKind.Slaughter))
                .ToList();
        }

        public TradeProposal ToTradeProposal()
        {
            var actions = ToAggregatedActions()
                .Select(action => new TradeAction(
                    action.EquipmentElement,
                    action.Quantity,
                    ToCoreActionType(action.Kind)))
                .ToList();
            return new TradeProposal(actions);
        }

        private List<PlannedTradeAction> ToAggregatedActions()
        {
            return _actions
                .GroupBy(action => new { action.Kind, action.IdentityKey })
                .Select(group =>
                {
                    var first = group.First();
                    return new PlannedTradeAction(
                        first.Kind,
                        first.Phase,
                        first.EquipmentElement,
                        first.IdentityKey,
                        first.ItemName,
                        group.Sum(action => action.Quantity),
                        group.Sum(action => action.Gold),
                        first.StartPrice,
                        group.Last().EndPrice,
                        first.ReferencePrice,
                        first.ProfitDensity,
                        first.IsLoot,
                        first.IsMarginSwap);
                })
                .ToList();
        }

        private static TradeActionType ToCoreActionType(PlannedTradeActionKind kind)
        {
            switch (kind)
            {
                case PlannedTradeActionKind.Buy:
                    return TradeActionType.Buy;
                case PlannedTradeActionKind.Sell:
                    return TradeActionType.Sell;
                case PlannedTradeActionKind.Slaughter:
                    return TradeActionType.Slaughter;
                default:
                    return TradeActionType.Buy;
            }
        }
    }

    internal static class TradeBlockReasonFormatter
    {
        public static string ToSummaryKey(TradeBlockReason reason)
        {
            switch (reason)
            {
                case TradeBlockReason.CategoryPolicy:
                    return "CategoryPolicy";
                case TradeBlockReason.MerchantStockDepleted:
                    return "MerchantStockDepleted";
                case TradeBlockReason.NotSellable:
                    return "NotSellable";
                case TradeBlockReason.UnknownReferencePrice:
                    return "UnknownReferencePrice";
                case TradeBlockReason.LootLockedForXpFarm:
                    return "LootLockedForXpFarm";
                case TradeBlockReason.SellBuyConflict:
                    return "SellBuyConflict";
                case TradeBlockReason.HerdingLimitExceeded:
                    return "HerdingLimitExceeded";
                case TradeBlockReason.Overburdened:
                    return "Overburdened";
                case TradeBlockReason.CargoNearLimit:
                    return "CargoNearLimit";
                case TradeBlockReason.StackCountCap:
                    return "StackLimitExceeded";
                case TradeBlockReason.StackValueCap:
                    return "StackValueLimitExceeded";
                case TradeBlockReason.BudgetProtection:
                    return "BudgetProtectionActive";
                case TradeBlockReason.SameStopExclusion:
                    return "SoldInSameStop";
                case TradeBlockReason.AveragePriceUndetermined:
                    return "AveragePriceUndetermined";
                case TradeBlockReason.PriceThreshold:
                    return "PriceCheckFailed";
                case TradeBlockReason.NoProfitExpected:
                    return "NoProfitExpected";
                case TradeBlockReason.MarginSwapNotBetter:
                    return "MarginSwapNotBetter";
                default:
                    return "None";
            }
        }

        public static string Format(TradeBlockReason reason, string details)
        {
            string key = ToSummaryKey(reason);
            return string.IsNullOrWhiteSpace(details) ? key : $"{key} ({details})";
        }
    }
}
