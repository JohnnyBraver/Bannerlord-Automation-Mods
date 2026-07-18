using System;
using System.Collections.Generic;
using System.Linq;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TradeOptimizer
{
    internal sealed class TradePlanner
    {
        private const int MaxBuyLoopIterations = 1000;
        private const int MaxBuySkipDiagnostics = 25;

        public TradePlan CreatePlan(TradePlanningRequest request)
        {
            PricingService.ClearCache();

            var plan = new TradePlan();
            var vm = request.InventoryVm;
            var settings = request.Settings;
            var logic = vm.GetInventoryLogic();
            string otherPartyName = logic?.OtherParty?.Name?.ToString() ?? "Unknown";
            TradingEngine.WriteLog($"=== Optimization Run started for: {otherPartyName} (Version: {SubModule.Version}, Simulation Mode: {settings.SimulationMode}) ===");

            float startWeight = MobileParty.MainParty != null
                ? SettlementAutomationCore.Helpers.InventoryHelper.GetRosterWeight(MobileParty.MainParty.ItemRoster)
                : 0f;
            float usableCapacity = startWeight + request.TradeContext.CargoCapacityBalance;
            var state = new TradeSimulationState(request.TradeContext.AvailableGold, startWeight, usableCapacity);

            TradingEngine.WriteLog($"Initial Balance: {state.CurrentBalance} (Hero Gold: {Hero.MainHero?.Gold ?? 0}, Logic TotalAmount: {logic?.TotalAmount ?? 0})");
            TradingEngine.WriteLog($"Trade Perks Status: hasTier1={PricingService.HasTier1Perks()}, hasTier2={PricingService.HasTier2Perks()}");

            var localExcludedItems = new HashSet<string>(request.ExcludedItemKeys, StringComparer.Ordinal);
            var snapshot = TradeMarketSnapshot.Capture(vm, logic);

            if (request.RunSellPhase)
            {
                PlanSellPhase(request, snapshot, state, plan, localExcludedItems, logic);
            }

            if (request.RunBuyPhase)
            {
                PlanBuyPhase(request, snapshot, state, plan, localExcludedItems, logic);
            }

            WriteOptimizationSummary(plan);
            return plan;
        }

        private static void PlanSellPhase(
            TradePlanningRequest request,
            TradeMarketSnapshot snapshot,
            TradeSimulationState state,
            TradePlan plan,
            ISet<string> localExcludedItems,
            InventoryLogic? logic)
        {
            var settings = request.Settings;
            foreach (var stack in snapshot.PlayerStacks)
            {
                if (!TradeCandidatePolicy.CanTradeByMode(
                        stack.Item,
                        settings.FoodTradingMode,
                        settings.LivestockTradingMode,
                        settings.MountsTradingMode,
                        settings.CraftingMaterialsTradingMode,
                        isBuy: false))
                {
                    continue;
                }

                var sellableEntry = request.TradeContext.SellableItems.FirstOrDefault(s => s.Matches(stack.EquipmentElement));
                int maxSellable = (sellableEntry?.AvailableQuantity ?? 0) - state.GetSoldCount(stack.IdentityKey);
                if (maxSellable <= 0)
                {
                    continue;
                }

                float baseReferencePrice = stack.SellReferencePrice;
                float avgPriceForBuy = snapshot.GetBuyReferencePrice(stack.IdentityKey);
                int startPrice = GetSellPrice(logic, stack);
                int currentPrice = startPrice;
                int sold = 0;
                int totalGoldGained = 0;
                bool isLoot = PricingService.GetTrackedAveragePrice(stack.RosterElement) <= 0f;
                TradeBlockReason stopReason = TradeBlockReason.None;
                string stopDetails = string.Empty;

                while (sold < maxSellable)
                {
                    currentPrice = GetSellPrice(logic, stack);
                    bool shouldSell = false;

                    bool forceLootSale = isLoot && (settings.LootHandling == LootHandlingMode.Liquidate || settings.LootHandling == LootHandlingMode.XPFarm);
                    if (forceLootSale)
                    {
                        shouldSell = currentPrice > 0;
                        if (!shouldSell)
                        {
                            stopReason = TradeBlockReason.PriceThreshold;
                            stopDetails = "loot sell price was not positive";
                        }
                    }
                    else
                    {
                        if (baseReferencePrice <= 0f)
                        {
                            stopReason = TradeBlockReason.UnknownReferencePrice;
                            stopDetails = "valuation reference could not be determined";
                            plan.RecordBlock(new TradeDecisionTrace(
                                TradePlanPhase.Sell,
                                stack.IdentityKey,
                                stack.Name,
                                stopReason,
                                stopDetails,
                                currentPrice,
                                baseReferencePrice));
                            break;
                        }

                        shouldSell = currentPrice >= baseReferencePrice * settings.SellPriceThresholdFactor;
                    }

                    if (!shouldSell)
                    {
                        if (stopReason == TradeBlockReason.None)
                        {
                            stopReason = TradeBlockReason.PriceThreshold;
                            stopDetails = $"price={currentPrice}, reference={baseReferencePrice:F1}, threshold={settings.SellPriceThresholdFactor:P1}";
                        }

                        TradingEngine.WriteLog($"[Trace-Sell] {stack.Name}: SKIPPED | {TradeBlockReasonFormatter.Format(stopReason, stopDetails)}");
                        plan.RecordBlock(new TradeDecisionTrace(
                            TradePlanPhase.Sell,
                            stack.IdentityKey,
                            stack.Name,
                            stopReason,
                            stopDetails,
                            currentPrice,
                            baseReferencePrice));
                        break;
                    }

                    if (!isLoot || settings.LootHandling == LootHandlingMode.Profit)
                    {
                        int buyPrice = GetBuyPrice(logic, stack);
                        bool isBuyCandidate = avgPriceForBuy > 0f && buyPrice <= avgPriceForBuy * settings.BuyPriceThresholdFactor;
                        if (isBuyCandidate)
                        {
                            if (settings.Stance == TradingStance.MaxProfit)
                            {
                                stopReason = TradeBlockReason.SellBuyConflict;
                                stopDetails = "price below buy threshold (Stance: Max Profit)";
                                TradingEngine.WriteLog($"[Trace-Sell] {stack.Name}: SKIPPED (Conflict) | {stopDetails}");
                                plan.RecordBlock(new TradeDecisionTrace(
                                    TradePlanPhase.Sell,
                                    stack.IdentityKey,
                                    stack.Name,
                                    stopReason,
                                    stopDetails,
                                    currentPrice,
                                    baseReferencePrice));
                                break;
                            }

                            bool isGoodSell = baseReferencePrice > 0f && currentPrice >= baseReferencePrice * settings.GoodSellThreshold;
                            if (!isGoodSell)
                            {
                                float currentWeightNow = state.StartWeight + state.NetWeightAdded;
                                float fullness = state.UsableCapacity > 0f ? currentWeightNow / state.UsableCapacity : 0f;
                                float limit = settings.CargoLimitThreshold;
                                if (fullness < limit)
                                {
                                    stopReason = TradeBlockReason.SellBuyConflict;
                                    stopDetails = $"price below buy threshold (Stance: Balanced, Fullness {fullness:P0} < {limit:P0})";
                                    TradingEngine.WriteLog($"[Trace-Sell] {stack.Name}: SKIPPED (Conflict) | {stopDetails}");
                                    plan.RecordBlock(new TradeDecisionTrace(
                                        TradePlanPhase.Sell,
                                        stack.IdentityKey,
                                        stack.Name,
                                        stopReason,
                                        stopDetails,
                                        currentPrice,
                                        baseReferencePrice));
                                    break;
                                }
                            }
                        }
                    }

                    int price = currentPrice;
                    ApplyTransfer(request, logic, stack.ItemVm, stack.EquipmentElement, quantity: 1, isBuy: false);
                    sold++;
                    totalGoldGained += price;
                    state.CurrentBalance += price;
                    state.NetWeightAdded -= stack.Weight;
                    state.RecordSold(stack.IdentityKey, 1);

                    if (!isLoot || settings.LootHandling != LootHandlingMode.XPFarm)
                    {
                        localExcludedItems.Add(stack.IdentityKey);
                    }
                }

                WriteSellResult(settings, stack, isLoot, sold, startPrice, currentPrice, baseReferencePrice, totalGoldGained, stopReason, stopDetails);

                if (sold > 0)
                {
                    plan.RecordAction(new PlannedTradeAction(
                        PlannedTradeActionKind.Sell,
                        TradePlanPhase.Sell,
                        stack.EquipmentElement,
                        stack.IdentityKey,
                        stack.Name,
                        sold,
                        totalGoldGained,
                        startPrice,
                        currentPrice,
                        PricingService.GetWorldAveragePrice(stack.EquipmentElement),
                        0f,
                        isLoot));
                }
            }
        }

        private static void WriteSellResult(
            Settings settings,
            TradeMarketStack stack,
            bool isLoot,
            int sold,
            int startPrice,
            int currentPrice,
            float baseReferencePrice,
            int totalGoldGained,
            TradeBlockReason stopReason,
            string stopDetails)
        {
            if (isLoot && settings.LootHandling != LootHandlingMode.Profit)
            {
                if ((settings.LootHandling == LootHandlingMode.Liquidate || settings.LootHandling == LootHandlingMode.XPFarm) && sold > 0)
                {
                    TradingEngine.WriteLog($"[Sell Result Loot] {stack.Name}: SOLD {sold} units | StartPrice={startPrice}, EndPrice={currentPrice} | Gold={totalGoldGained}d (Loot Handling: {settings.LootHandling})");
                }
                else if (settings.LootHandling == LootHandlingMode.XPFarm)
                {
                    TradingEngine.WriteLog($"[Sell Result Loot] {stack.Name}: SKIPPED (No positive sell price for XP Farm)");
                }
                return;
            }

            if (baseReferencePrice <= 0f)
            {
                TradingEngine.WriteLog($"[Sell Result] {stack.Name}: SKIPPED (Valuation reference undetermined)");
                return;
            }

            float startRatio = baseReferencePrice > 0 ? startPrice / baseReferencePrice : 1f;
            float endRatio = baseReferencePrice > 0 ? currentPrice / baseReferencePrice : 1f;
            if (sold > 0)
            {
                TradingEngine.WriteLog($"[Sell Result] {stack.Name}: SOLD {sold} units | StartPrice={startPrice} (Ratio={startRatio:P1}), EndPrice={currentPrice} (Ratio={endRatio:P1}) | BaseRef={baseReferencePrice:F1}, Thresh={settings.SellPriceThresholdFactor:P1} | Gold={totalGoldGained}d");
            }
            else
            {
                string reasonString = stopReason != TradeBlockReason.None
                    ? $" ({TradeBlockReasonFormatter.Format(stopReason, stopDetails)})"
                    : "";
                TradingEngine.WriteLog($"[Sell Result] {stack.Name}: KEEP | Price={currentPrice} (Ratio={endRatio:P1}), BaseRef={baseReferencePrice:F1}, Thresh={settings.SellPriceThresholdFactor:P1}{reasonString}");
            }
        }

        private static void PlanBuyPhase(
            TradePlanningRequest request,
            TradeMarketSnapshot snapshot,
            TradeSimulationState state,
            TradePlan plan,
            ISet<string> localExcludedItems,
            InventoryLogic? logic)
        {
            var settings = request.Settings;
            var loggedSkipDiagnostics = new HashSet<string>(StringComparer.Ordinal);
            int buySkipDiagnosticsWritten = 0;
            bool buySkipDiagnosticsSuppressed = false;
            int buyLoopIterations = 0;
            bool marginSwappingEnabled = settings.EnableMarginSwapping;

            if (settings.LootHandling == LootHandlingMode.XPFarm)
            {
                PlanXpFarmActivation(request, snapshot, state, plan, logic);
            }

            while (true)
            {
                buyLoopIterations++;
                if (buyLoopIterations > MaxBuyLoopIterations)
                {
                    TradingEngine.WriteLog($"[Buy Loop Guard] Stopped buy optimization after {MaxBuyLoopIterations} iterations to keep town-entry automation responsive.");
                    break;
                }

                SwapCandidatePair swapCandidates = marginSwappingEnabled
                    ? FindWorstOwnedSwapCandidates(request, snapshot, state, logic)
                    : SwapCandidatePair.Empty;

                TradeMarketStack? bestItem = null;
                TradeBuyCandidateDecision? bestDecision = null;
                float bestProfitDensity = -1f;
                SwapCandidate? bestSwapCandidate = null;
                var iterationSkipReasons = new Dictionary<TradeBlockReason, int>();

                foreach (var stack in snapshot.MerchantStacks)
                {
                    bool categoryAllowed = TradeCandidatePolicy.CanTradeByMode(
                        stack.Item,
                        settings.FoodTradingMode,
                        settings.LivestockTradingMode,
                        settings.MountsTradingMode,
                        settings.CraftingMaterialsTradingMode,
                        isBuy: true);

                    int boughtSoFar = state.GetBoughtCount(stack.IdentityKey);
                    int currentPrice = GetBuyPrice(logic, stack);
                    float avgPrice = snapshot.GetBuyReferencePrice(stack.IdentityKey);
                    float unitProfit = avgPrice - currentPrice;
                    float itemWeightDivisor = stack.Weight > 0.01f ? stack.Weight : 0.01f;
                    float profitDensity = unitProfit / itemWeightDivisor;
                    int currentlyOwned = state.GetOwnedCount(snapshot, stack.IdentityKey);
                    bool isAnimalOrMount = IsAnimalOrMount(stack.Item);
                    float currentWeightNow = state.StartWeight + state.NetWeightAdded;
                    float fullness = state.UsableCapacity > 0f ? currentWeightNow / state.UsableCapacity : 0f;
                    float freeCargo = request.TradeContext.CargoCapacityBalance - state.NetWeightAdded;

                    var selectedSwap = SelectSwapCandidateFor(stack, swapCandidates);
                    var input = new TradeBuyCandidateInput
                    {
                        CategoryAllowed = categoryAllowed,
                        InitialMerchantCount = stack.InitialCount,
                        BoughtSoFar = boughtSoFar,
                        IsAnimalOrMount = isAnimalOrMount,
                        AnimalsBoughtSoFar = state.TotalAnimalsBoughtInSim,
                        RemainingAnimalSlots = request.TradeContext.FreeAnimalSlots,
                        EnforceCargoLimit = request.TradeContext.EnforceCargoLimit,
                        FreeCargo = freeCargo,
                        ItemWeight = stack.Weight,
                        MarginSwappingEnabled = marginSwappingEnabled,
                        SwapCandidateAvailable = selectedSwap != null,
                        SwapProfitDensity = selectedSwap?.ProfitDensity ?? float.MaxValue,
                        ProfitDensity = profitDensity,
                        Stance = settings.Stance,
                        CargoFullness = fullness,
                        CargoLimitThreshold = settings.CargoLimitThreshold,
                        GoodBuyLimit = avgPrice * settings.GoodBuyThreshold,
                        BuyCapPolicy = settings.BuyCapPolicy,
                        BuyCountCapMode = settings.BuyCountCapMode,
                        BuyValueCapMode = settings.BuyValueCapMode,
                        CurrentlyOwned = currentlyOwned,
                        VisitSpentSoFar = state.GetBuyGold(stack.IdentityKey),
                        CurrentBalance = state.CurrentBalance,
                        CurrentPrice = currentPrice,
                        AveragePrice = avgPrice,
                        BuyPriceThresholdFactor = settings.BuyPriceThresholdFactor,
                        MaxStackSizeToBuy = settings.MaxStackSizeToBuy,
                        MaxStackValueToBuy = settings.MaxStackValueToBuy,
                        SoldInSameStop = localExcludedItems.Contains(stack.IdentityKey)
                    };

                    var decision = TradeBuyCandidateEvaluator.Evaluate(input);
                    if (!decision.Accepted)
                    {
                        IncrementReason(iterationSkipReasons, decision.Reason);
                        plan.RecordBlock(new TradeDecisionTrace(
                            TradePlanPhase.DirectBuy,
                            stack.IdentityKey,
                            stack.Name,
                            decision.Reason,
                            decision.Details,
                            currentPrice,
                            avgPrice));

                        WriteBuySkipDiagnostic(
                            loggedSkipDiagnostics,
                            ref buySkipDiagnosticsWritten,
                            ref buySkipDiagnosticsSuppressed,
                            stack,
                            decision,
                            currentPrice,
                            avgPrice);
                        continue;
                    }

                    if (profitDensity > bestProfitDensity)
                    {
                        bestProfitDensity = profitDensity;
                        bestItem = stack;
                        bestDecision = decision;
                        bestSwapCandidate = selectedSwap;
                    }
                }

                if (bestItem == null || bestDecision == null)
                {
                    WriteBuyLoopCompletionLog(iterationSkipReasons);
                    break;
                }

                int buyPrice = GetBuyPrice(logic, bestItem);
                if (bestDecision.RequiresSwap && bestSwapCandidate != null)
                {
                    ExecuteSwapSell(request, snapshot, state, plan, logic, bestItem, bestSwapCandidate, bestProfitDensity);
                }

                ExecuteBuy(request, snapshot, state, plan, logic, bestItem, buyPrice, bestProfitDensity, bestDecision.RequiresSwap ? TradePlanPhase.MarginSwap : TradePlanPhase.DirectBuy);
            }

            WriteBuyActionLogs(plan);
        }

        private static void PlanXpFarmActivation(
            TradePlanningRequest request,
            TradeMarketSnapshot snapshot,
            TradeSimulationState state,
            TradePlan plan,
            InventoryLogic? logic)
        {
            foreach (var stack in snapshot.MerchantStacks)
            {
                if (!TradeCandidatePolicy.CanTradeByMode(
                        stack.Item,
                        request.Settings.FoodTradingMode,
                        request.Settings.LivestockTradingMode,
                        request.Settings.MountsTradingMode,
                        request.Settings.CraftingMaterialsTradingMode,
                        isBuy: true))
                {
                    continue;
                }

                var matchingPlayerStacks = snapshot.PlayerStacks
                    .Where(playerStack => playerStack.IdentityKey == stack.IdentityKey)
                    .ToList();
                var firstPlayerStack = matchingPlayerStacks.FirstOrDefault();
                int currentlyOwned = matchingPlayerStacks.Sum(playerStack => playerStack.InitialCount);
                if (firstPlayerStack == null || currentlyOwned <= 0)
                {
                    continue;
                }

                float trackedPrice = PricingService.GetTrackedAveragePrice(firstPlayerStack.RosterElement);
                if (trackedPrice > 0f)
                {
                    continue;
                }

                int currentPrice = GetBuyPrice(logic, stack);
                if (currentPrice <= 1)
                {
                    continue;
                }

                int requiredQty = (int)Math.Ceiling((double)currentlyOwned / (currentPrice - 1));
                int toBuy = Math.Min(requiredQty, stack.InitialCount - state.GetBoughtCount(stack.IdentityKey));
                if (IsAnimalOrMount(stack.Item))
                {
                    int remainingAnimalSlots = Math.Max(0, request.TradeContext.FreeAnimalSlots - state.TotalAnimalsBoughtInSim);
                    toBuy = Math.Min(toBuy, remainingAnimalSlots);
                }

                if (toBuy <= 0)
                {
                    continue;
                }

                int cost = toBuy * currentPrice;
                float weight = toBuy * stack.Weight;
                bool passesCargo = true;
                if (request.TradeContext.EnforceCargoLimit)
                {
                    float freeCargo = request.TradeContext.CargoCapacityBalance - state.NetWeightAdded;
                    passesCargo = freeCargo >= weight;
                }

                if (state.CurrentBalance < cost || !passesCargo)
                {
                    continue;
                }

                for (int i = 0; i < toBuy; i++)
                {
                    ApplyTransfer(request, logic, stack.ItemVm, stack.EquipmentElement, quantity: 1, isBuy: true);
                }

                state.CurrentBalance -= cost;
                state.NetWeightAdded += weight;
                if (IsAnimalOrMount(stack.Item))
                {
                    state.TotalAnimalsBoughtInSim += toBuy;
                }

                state.RecordBought(stack.IdentityKey, toBuy, cost);
                double resultAvg = (double)(toBuy * currentPrice) / (currentlyOwned + toBuy);
                if (toBuy >= requiredQty)
                {
                    TradingEngine.WriteLog($"[XP Farm Activation] Bought {toBuy}x {stack.Name} at {currentPrice}d to activate cost basis for {currentlyOwned} looted units. Stack unlocked (New Average: {resultAvg:F2}d)!");
                }
                else
                {
                    int remainingNeeded = requiredQty - toBuy;
                    TradingEngine.WriteLog($"[XP Farm Activation] Bought {toBuy}x {stack.Name} at {currentPrice}d (Partial Activation) for {currentlyOwned} looted units. Current Average: {resultAvg:F3}d (Need {remainingNeeded} more units at {currentPrice}d to unlock).");
                }

                plan.RecordAction(new PlannedTradeAction(
                    PlannedTradeActionKind.Buy,
                    TradePlanPhase.XpFarmActivation,
                    stack.EquipmentElement,
                    stack.IdentityKey,
                    stack.Name,
                    toBuy,
                    cost,
                    currentPrice,
                    currentPrice,
                    snapshot.GetBuyReferencePrice(stack.IdentityKey),
                    0f));
            }
        }

        private static void ExecuteSwapSell(
            TradePlanningRequest request,
            TradeMarketSnapshot snapshot,
            TradeSimulationState state,
            TradePlan plan,
            InventoryLogic? logic,
            TradeMarketStack targetBuy,
            SwapCandidate swapCandidate,
            float buyProfitDensity)
        {
            var swapStack = swapCandidate.Stack;
            int swapPrice = GetSellPrice(logic, swapStack);
            ApplyTransfer(request, logic, swapStack.ItemVm, swapStack.EquipmentElement, quantity: 1, isBuy: false);

            state.CurrentBalance += swapPrice;
            state.NetWeightAdded -= swapStack.Weight;
            if (IsAnimalOrMount(swapStack.Item))
            {
                state.TotalAnimalsBoughtInSim--;
            }

            state.RecordExtraSold(swapStack.IdentityKey, 1);
            plan.RecordAction(new PlannedTradeAction(
                PlannedTradeActionKind.Sell,
                TradePlanPhase.MarginSwap,
                swapStack.EquipmentElement,
                swapStack.IdentityKey,
                swapStack.Name,
                1,
                swapPrice,
                swapPrice,
                swapPrice,
                PricingService.GetWorldAveragePrice(swapStack.EquipmentElement),
                swapCandidate.ProfitDensity,
                isLoot: false,
                isMarginSwap: true));

            TradingEngine.WriteLog($"[Margin Swap] Sold 1x {swapStack.Name} (Future Profit Density: {swapCandidate.ProfitDensity:F1}, Price={swapPrice}) to buy {targetBuy.Name} (Buy Profit Density: {buyProfitDensity:F1})");
        }

        private static void ExecuteBuy(
            TradePlanningRequest request,
            TradeMarketSnapshot snapshot,
            TradeSimulationState state,
            TradePlan plan,
            InventoryLogic? logic,
            TradeMarketStack stack,
            int buyPrice,
            float profitDensity,
            TradePlanPhase phase)
        {
            ApplyTransfer(request, logic, stack.ItemVm, stack.EquipmentElement, quantity: 1, isBuy: true);
            state.CurrentBalance -= buyPrice;
            state.NetWeightAdded += stack.Weight;
            if (IsAnimalOrMount(stack.Item))
            {
                state.TotalAnimalsBoughtInSim++;
            }

            state.RecordBought(stack.IdentityKey, 1, buyPrice);
            plan.RecordAction(new PlannedTradeAction(
                PlannedTradeActionKind.Buy,
                phase,
                stack.EquipmentElement,
                stack.IdentityKey,
                stack.Name,
                1,
                buyPrice,
                buyPrice,
                buyPrice,
                snapshot.GetBuyReferencePrice(stack.IdentityKey),
                profitDensity));
        }

        private static void ApplyTransfer(
            TradePlanningRequest request,
            InventoryLogic? logic,
            SPItemVM itemVm,
            EquipmentElement equipmentElement,
            int quantity,
            bool isBuy)
        {
            if (!request.ApplyTransfers || quantity <= 0)
            {
                return;
            }

            if (logic != null && Hero.MainHero != null)
            {
                var command = TransferCommand.Transfer(
                    quantity,
                    isBuy ? InventoryLogic.InventorySide.OtherInventory : InventoryLogic.InventorySide.PlayerInventory,
                    isBuy ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory,
                    new ItemRosterElement(equipmentElement, quantity),
                    EquipmentIndex.None,
                    EquipmentIndex.None,
                    Hero.MainHero.CharacterObject);
                logic.AddTransferCommand(command);
                return;
            }

            for (int i = 0; i < quantity; i++)
            {
                if (isBuy)
                {
                    itemVm.ExecuteBuySingle();
                }
                else
                {
                    itemVm.ExecuteSellSingle();
                }
            }
        }

        private static SwapCandidatePair FindWorstOwnedSwapCandidates(
            TradePlanningRequest request,
            TradeMarketSnapshot snapshot,
            TradeSimulationState state,
            InventoryLogic? logic)
        {
            SwapCandidate? first = null;
            SwapCandidate? second = null;

            foreach (var ownStack in snapshot.PlayerStacks)
            {
                if (!TradeCandidatePolicy.CanTradeByMode(
                        ownStack.Item,
                        request.Settings.FoodTradingMode,
                        request.Settings.LivestockTradingMode,
                        request.Settings.MountsTradingMode,
                        request.Settings.CraftingMaterialsTradingMode,
                        isBuy: false))
                {
                    continue;
                }

                var sellableEntry = request.TradeContext.SellableItems.FirstOrDefault(s => s.Matches(ownStack.EquipmentElement));
                int maxSellable = (sellableEntry?.AvailableQuantity ?? 0)
                    - state.GetSoldCount(ownStack.IdentityKey)
                    - state.GetExtraSoldCount(ownStack.IdentityKey);
                if (maxSellable <= 0)
                {
                    continue;
                }

                int ownSellPrice = GetSellPrice(logic, ownStack);
                if (ownSellPrice <= 0)
                {
                    continue;
                }

                float ownAvgPrice = snapshot.GetSellReferencePrice(ownStack.IdentityKey);
                float ownWeightDivisor = ownStack.Weight > 0.01f ? ownStack.Weight : 0.01f;
                float ownFutureProfitDensity = (ownAvgPrice - ownSellPrice) / ownWeightDivisor;
                var candidate = new SwapCandidate(ownStack, ownFutureProfitDensity, ownSellPrice);

                if (first == null || candidate.ProfitDensity < first.ProfitDensity)
                {
                    second = first;
                    first = candidate;
                }
                else if (second == null || candidate.ProfitDensity < second.ProfitDensity)
                {
                    second = candidate;
                }
            }

            return new SwapCandidatePair(first, second);
        }

        private static SwapCandidate? SelectSwapCandidateFor(TradeMarketStack buyStack, SwapCandidatePair candidates)
        {
            var candidate = candidates.First;
            if (candidate != null && candidate.Stack.Item == buyStack.Item)
            {
                candidate = candidates.Second;
            }

            return candidate;
        }

        private static void WriteBuySkipDiagnostic(
            HashSet<string> loggedSkipDiagnostics,
            ref int buySkipDiagnosticsWritten,
            ref bool buySkipDiagnosticsSuppressed,
            TradeMarketStack stack,
            TradeBuyCandidateDecision decision,
            int currentPrice,
            float avgPrice)
        {
            if (currentPrice >= avgPrice && decision.Reason != TradeBlockReason.AveragePriceUndetermined)
            {
                return;
            }

            string diagnosticKey = $"{stack.IdentityKey}:{decision.Reason}:{decision.Details}";
            if (!loggedSkipDiagnostics.Add(diagnosticKey))
            {
                return;
            }

            if (buySkipDiagnosticsWritten < MaxBuySkipDiagnostics)
            {
                TradingEngine.WriteLog($"[Buy Skip Diagnostic] {stack.Name}: {TradeBlockReasonFormatter.Format(decision.Reason, decision.Details)}");
                buySkipDiagnosticsWritten++;
            }
            else if (!buySkipDiagnosticsSuppressed)
            {
                TradingEngine.WriteLog($"[Buy Skip Diagnostic] Additional buy-skip diagnostics suppressed after {MaxBuySkipDiagnostics} unique entries.");
                buySkipDiagnosticsSuppressed = true;
            }
        }

        private static void WriteBuyLoopCompletionLog(Dictionary<TradeBlockReason, int> reasons)
        {
            if (reasons.Count == 0)
            {
                TradingEngine.WriteLog("[Buy Loop Complete] No eligible merchant candidates remained.");
                return;
            }

            string summary = string.Join(", ", reasons
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => TradeBlockReasonFormatter.ToSummaryKey(pair.Key))
                .Select(pair => $"{TradeBlockReasonFormatter.ToSummaryKey(pair.Key)}={pair.Value}"));
            TradingEngine.WriteLog($"[Buy Loop Complete] No buy candidate passed all checks. Remaining candidate blocks: {summary}");
        }

        private static void WriteBuyActionLogs(TradePlan plan)
        {
            foreach (var buy in plan.Actions.Where(action => action.Kind == PlannedTradeActionKind.Buy))
            {
                TradingEngine.WriteLog($"[Buy Result] {buy.ItemName}: BOUGHT {buy.Quantity} units | StartPrice={buy.StartPrice}, EndPrice={buy.EndPrice} | RefPrice={buy.ReferencePrice:F1} | Gold=-{buy.Gold}d | Phase={buy.Phase}");
            }
        }

        private static void WriteOptimizationSummary(TradePlan plan)
        {
            var report = plan.ToTransactionReport();
            TradingEngine.WriteLog("=== Planned Optimization Results (before Core execution) ===");
            if (report.SoldItems.Count > 0)
            {
                TradingEngine.WriteLog("Sold Items:");
                foreach (var item in report.SoldItems)
                {
                    TradingEngine.WriteLog($"  - {item.Count}x {item.Name}: Estimated Gold=+{item.Gold}d (Avg={item.Gold / (float)item.Count:F1}d), RefPrice={item.MarketPrice:F1}d");
                }
            }

            if (report.BoughtItems.Count > 0)
            {
                TradingEngine.WriteLog("Bought Items:");
                foreach (var item in report.BoughtItems)
                {
                    TradingEngine.WriteLog($"  - {item.Count}x {item.Name}: Estimated Gold=-{item.Gold}d (Avg={item.Gold / (float)item.Count:F1}d), RefPrice={item.MarketPrice:F1}d");
                }
            }

            if (report.ArbitrageSlaughters.Count > 0)
            {
                TradingEngine.WriteLog("Slaughtered Items:");
                foreach (var slaughter in report.ArbitrageSlaughters)
                {
                    TradingEngine.WriteLog($"  - {slaughter.Amount}x {slaughter.EqElement.Item.Name}");
                }
            }

            if (report.SoldItems.Count == 0 && report.BoughtItems.Count == 0 && report.ArbitrageSlaughters.Count == 0)
            {
                TradingEngine.WriteLog("No trades executed.");
            }

            TradingEngine.WriteLog("=================================");
        }

        private static void IncrementReason(Dictionary<TradeBlockReason, int> reasons, TradeBlockReason reason)
        {
            reasons[reason] = reasons.TryGetValue(reason, out int count) ? count + 1 : 1;
        }

        private static int GetBuyPrice(InventoryLogic? logic, TradeMarketStack stack)
        {
            return logic != null ? logic.GetItemPrice(stack.EquipmentElement, true) : stack.Item.Value;
        }

        private static int GetSellPrice(InventoryLogic? logic, TradeMarketStack stack)
        {
            return logic != null ? logic.GetItemPrice(stack.EquipmentElement, false) : 0;
        }

        private static bool IsAnimalOrMount(ItemObject item)
        {
            return item.IsAnimal || (item.IsMountable && item.HorseComponent != null);
        }

        private sealed class SwapCandidate
        {
            public SwapCandidate(TradeMarketStack stack, float profitDensity, int sellPrice)
            {
                Stack = stack;
                ProfitDensity = profitDensity;
                SellPrice = sellPrice;
            }

            public TradeMarketStack Stack { get; }
            public float ProfitDensity { get; }
            public int SellPrice { get; }
        }

        private sealed class SwapCandidatePair
        {
            public static readonly SwapCandidatePair Empty = new SwapCandidatePair(null, null);

            public SwapCandidatePair(SwapCandidate? first, SwapCandidate? second)
            {
                First = first;
                Second = second;
            }

            public SwapCandidate? First { get; }
            public SwapCandidate? Second { get; }
        }
    }
}
