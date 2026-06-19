using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;

namespace SettlementAutomationCore
{
    public class SubModule : MBSubModuleBase
    {
        public static event Action<Settlement>? OnAutomationCycleCompleted;
        private static Settlement? _pendingBackgroundTradeSettlement = null;
        private static readonly object QueueLock = new object();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                var campaignStarter = gameStarter as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    campaignStarter.AddBehavior(new SettlementAutomationCampaignBehavior());
                }
            }
        }

        public static void QueueBackgroundTrade(Settlement settlement)
        {
            lock (QueueLock)
            {
                _pendingBackgroundTradeSettlement = settlement;
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            Settlement? sett = null;
            lock (QueueLock)
            {
                if (_pendingBackgroundTradeSettlement != null)
                {
                    sett = _pendingBackgroundTradeSettlement;
                    _pendingBackgroundTradeSettlement = null;
                }
            }

            if (sett != null)
            {
                ExecuteBackgroundAutomation(sett);
            }
        }

        private static void ExecuteBackgroundAutomation(Settlement settlement)
        {
            if (settlement == null || MobileParty.MainParty == null || Hero.MainHero == null) return;

            try
            {
                var phasePolicy = AutomationPhasePolicy.Create(MobileParty.MainParty, settlement);
                if (!phasePolicy.CanRunAny)
                {
                    return;
                }

                // ----------------------------------------------------
                // Step 0: Preparation Phase
                // ----------------------------------------------------
                if (phasePolicy.Preparation)
                {
                    var preparationProviders = AutomationRegistry.ActivePreparationProviders;
                    foreach (var reg in preparationProviders)
                    {
                        try
                        {
                            reg.Provider.PrepareForAutomation(MobileParty.MainParty, settlement);
                        }
                        catch (Exception ex)
                        {
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error preparing automation from {reg.ProviderName}: {ex.Message}");
                        }
                    }
                }

                var marketReport = new AutomationMarketReport();

                // ----------------------------------------------------
                // Step 1: Gather Prioritized Requests and Reservations
                // ----------------------------------------------------
                AutomationRegistry.ClearRequests();
                AutomationRegistry.ClearReservations();
                AutomationRequestContext requestContext;
                if (phasePolicy.Requests)
                {
                    var visibilityLogic = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement, true);
                    if (visibilityLogic != null)
                    {
                        requestContext = AutomationRequestContext.FromInventoryLogic(MobileParty.MainParty, settlement, visibilityLogic);
                    }
                    else
                    {
                        requestContext = AutomationRequestContext.Empty(MobileParty.MainParty, settlement);
                    }

                    var requestProviders = AutomationRegistry.ActiveRequestProviders;
                    foreach (var reg in requestProviders)
                    {
                        try
                        {
                            reg.Provider.SubmitAutomationRequests(requestContext);
                        }
                        catch (Exception ex)
                        {
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error gathering requests from {reg.ProviderName}: {ex.Message}");
                        }
                    }
                }

                // ----------------------------------------------------
                // Step 2: Pre-Sell Phase (Revenue Generation)
                // ----------------------------------------------------
                var preSellOrders = new List<(TradeOrder Order, string ProviderName)>();
                if (phasePolicy.PreSell)
                {
                    var preSellProviders = AutomationRegistry.ActivePreSellProviders;
                    foreach (var reg in preSellProviders)
                    {
                        try
                        {
                            var orders = reg.Provider.GetPreSellOrders(MobileParty.MainParty, settlement);
                            if (orders != null)
                            {
                                preSellOrders.AddRange(orders.Select(order => (order, reg.ProviderName)));
                            }
                        }
                        catch (Exception ex)
                        {
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error gathering pre-sell orders from {reg.ProviderName}: {ex.Message}");
                        }
                    }
                }

                if (preSellOrders.Count > 0)
                {
                    var logic1 = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                    if (logic1 != null)
                    {
                        var lockKeys = InventoryLockHelper.GetCurrentLockKeys();
                        bool executedAny = false;
                        var preSoldList = new List<string>();
                        foreach (var preSellOrder in preSellOrders)
                        {
                            var order = preSellOrder.Order;
                            if (!order.IsBuy && order.EquipmentElement.Item != null) // Pre-sell only supports selling!
                            {
                                if (InventoryLockHelper.IsLocked(order.EquipmentElement, lockKeys))
                                {
                                    Helpers.Logger.WriteLog("SettlementAutomationCore", $"Skipped locked pre-sell order from {preSellOrder.ProviderName}: {order.Amount}x {order.EquipmentElement.Item.Name}");
                                    continue;
                                }

                                int estimatedGold = logic1.GetItemPrice(order.EquipmentElement, false) * order.Amount;
                                var command = TransferCommand.Transfer(
                                    order.Amount,
                                    InventoryLogic.InventorySide.PlayerInventory,
                                    InventoryLogic.InventorySide.OtherInventory,
                                    new ItemRosterElement(order.EquipmentElement, order.Amount),
                                    EquipmentIndex.None,
                                    EquipmentIndex.None,
                                    Hero.MainHero.CharacterObject
                                );
                                logic1.AddTransferCommand(command);
                                executedAny = true;
                                preSoldList.Add($"{order.Amount}x {order.EquipmentElement.Item.Name}");
                                marketReport.AddSold(order.EquipmentElement, order.Amount, estimatedGold, preSellOrder.ProviderName, AutomationTransactionStage.PreSell);
                            }
                        }
                        if (executedAny && logic1.IsThereAnyChanges())
                        {
                            int initialGold = Hero.MainHero?.Gold ?? 0;
                            logic1.DoneLogic();
                            int finalGold = Hero.MainHero?.Gold ?? 0;
                            int goldDiff = finalGold - initialGold;
                            string goldDiffSign = goldDiff >= 0 ? "+" : "";
                            marketReport.AddGoldDelta(goldDiff);
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Auto-pre-sold in {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d). Sold: {string.Join(", ", preSoldList)}");
                        }
                    }
                }

                // ----------------------------------------------------
                // Step 3: Tavern / Ransom & Mercenaries Phase
                // ----------------------------------------------------
                var ransomOrders = new List<RansomOrder>();
                var mercOrders = new List<MercenaryRecruitOrder>();
                if (phasePolicy.Tavern)
                {
                    var ransomRegistrations = AutomationRegistry.ActiveRansomProviders;
                    foreach (var reg in ransomRegistrations)
                    {
                        try
                        {
                            var rOrders = reg.Provider.GetRansomOrders(MobileParty.MainParty, settlement);
                            if (rOrders != null) ransomOrders.AddRange(rOrders);

                            var mOrders = reg.Provider.GetMercenaryRecruitOrders(MobileParty.MainParty, settlement);
                            if (mOrders != null) mercOrders.AddRange(mOrders);
                        }
                        catch {}
                    }
                }

                // Apply ransoms
                if (ransomOrders.Count > 0)
                {
                    try
                    {
                        var ransomRoster = TroopRoster.CreateDummyTroopRoster();
                        foreach (var order in ransomOrders)
                        {
                            if (order.Prisoner != null && order.Amount > 0)
                            {
                                ransomRoster.AddToCounts(order.Prisoner, order.Amount);
                            }
                        }
                        if (ransomRoster.Count > 0)
                        {
                            try
                            {
                                SellPrisonersAction.ApplyForSelectedPrisoners(MobileParty.MainParty.Party, null, ransomRoster);
                                var ransomParts = new List<string>();
                                int estimatedGold = 0;
                                foreach (var element in ransomRoster.GetTroopRoster())
                                {
                                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] Ransomed {element.Number}x {element.Character.Name}"));
                                    ransomParts.Add($"{element.Number}x {element.Character.Name}");
                                    try
                                    {
                                        var model = Campaign.Current?.Models?.RansomValueCalculationModel;
                                        if (model != null)
                                        {
                                            estimatedGold += model.PrisonerRansomValue(element.Character, Hero.MainHero) * element.Number;
                                        }
                                    }
                                    catch {}
                                }
                                Helpers.Logger.WriteLog("SettlementAutomationCore", $"Ransomed at {settlement.Name}: {string.Join(", ", ransomParts)} (Est. Gold: +{estimatedGold}d)");
                            }
                            catch (Exception ex)
                            {
                                Helpers.Logger.WriteLog("SettlementAutomationCore", $"Native ApplyForSelectedPrisoners failed ({ex.Message}). Applying manual ransom fallback.");
                                int totalRansomGold = 0;
                                var ransomParts = new List<string>();
                                foreach (var element in ransomRoster.GetTroopRoster())
                                {
                                    var character = element.Character;
                                    int count = element.Number;
                                    if (character == null || count <= 0) continue;

                                    int unitValue = 0;
                                    var model = Campaign.Current?.Models?.RansomValueCalculationModel;
                                    if (model != null)
                                    {
                                        unitValue = model.PrisonerRansomValue(character, Hero.MainHero);
                                    }
                                    else
                                    {
                                        unitValue = character.Tier * 15;
                                    }

                                    int ransomAmount = unitValue * count;
                                    totalRansomGold += ransomAmount;

                                    MobileParty.MainParty.PrisonRoster.AddToCounts(character, -count);
                                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] Ransomed {count}x {character.Name} for {ransomAmount} denars"));
                                    ransomParts.Add($"{count}x {character.Name} (+{ransomAmount}d)");
                                }

                                if (totalRansomGold > 0 && Hero.MainHero != null)
                                {
                                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, totalRansomGold, false);
                                    try
                                    {
                                        Campaign.Current.SkillLevelingManager?.OnPrisonerSell(MobileParty.MainParty, in ransomRoster);
                                    }
                                    catch (Exception xpEx)
                                    {
                                        Helpers.Logger.WriteLog("SettlementAutomationCore", $"Failed to award Roguery XP via SkillLevelingManager: {xpEx.Message}");
                                    }
                                }
                                Helpers.Logger.WriteLog("SettlementAutomationCore", $"[Manual Fallback] Ransomed at {settlement.Name}: {string.Join(", ", ransomParts)} (Total: +{totalRansomGold}d)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Helpers.Logger.WriteLog("SettlementAutomationCore", $"Ransom phase error: {ex}");
                    }
                }

                // Apply mercenary recruitment
                if (mercOrders.Count > 0)
                {
                    try
                    {
                        var recruitmentBehavior = Campaign.Current?.GetCampaignBehavior<TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior>();
                        if (recruitmentBehavior != null)
                        {
                            var applyMercMethod = recruitmentBehavior.GetType().GetMethod("ApplyRecruitMercenary", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (applyMercMethod != null)
                            {
                                foreach (var order in mercOrders)
                                {
                                    if (order.Troop != null && order.Amount > 0)
                                    {
                                        applyMercMethod.Invoke(recruitmentBehavior, new object[] { MobileParty.MainParty, settlement, order.Troop, order.Amount });
                                        InformationManager.DisplayMessage(new InformationMessage($"[Automation] Recruited {order.Amount}x {order.Troop.Name} (Mercenary)"));
                                    }
                                }
                            }
                        }
                    }
                    catch {}
                }

                // ----------------------------------------------------
                // Step 4: Notable Recruitment Phase
                // ----------------------------------------------------
                var recruitOrders = new List<RecruitOrder>();
                if (phasePolicy.Recruitment)
                {
                    var recruitRegistrations = AutomationRegistry.ActiveRecruitProviders;
                    foreach (var reg in recruitRegistrations)
                    {
                        try
                        {
                            var orders = reg.Provider.GetRecruitOrders(MobileParty.MainParty, settlement);
                            if (orders != null) recruitOrders.AddRange(orders);
                        }
                        catch {}
                    }
                }
                else
                {
                    Helpers.Logger.WriteLog("SettlementAutomationCore", $"Skipped notable recruitment at {settlement.Name}: settlement is not eligible.");
                }

                var recruitedMap = new Dictionary<CharacterObject, int>();
                int totalCount = 0;
                foreach (var order in recruitOrders)
                {
                    try
                    {
                        if (order.Notable == null || order.Notable.VolunteerTypes == null) continue;
                        if (order.SlotIndex < 0 || order.SlotIndex >= order.Notable.VolunteerTypes.Length) continue;

                        var troop = order.Notable.VolunteerTypes[order.SlotIndex];
                        if (troop == null) continue;

                        // Check party size limit before recruiting. Providers decide whether a
                        // specific order may overfill for their own follow-up automation.
                        int currentSize = MobileParty.MainParty.MemberRoster.TotalManCount;
                        int limit = MobileParty.MainParty.Party.PartySizeLimit;

                        if (currentSize >= limit && !order.AllowOverPartySize) continue;

                        int cost = (int)Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, Hero.MainHero, false).ResultNumber;
                        if (Hero.MainHero.Gold >= cost)
                        {
                            order.Notable.VolunteerTypes[order.SlotIndex] = null;
                            MobileParty.MainParty.MemberRoster.AddToCounts(troop, 1);
                            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, order.Notable, cost, false);
                            CampaignEventDispatcher.Instance.OnTroopRecruited(Hero.MainHero, settlement, order.Notable, troop, 1);
                            
                            if (recruitedMap.ContainsKey(troop))
                            {
                                recruitedMap[troop]++;
                            }
                            else
                            {
                                recruitedMap[troop] = 1;
                            }
                            totalCount++;
                        }
                    }
                    catch {}
                }

                if (totalCount > 0)
                {
                    var troopParts = recruitedMap.Select(kvp => $"{kvp.Value}x {kvp.Key.Name}");
                    string msg = $"Recruited in {settlement.Name}: {string.Join(", ", troopParts)} (Total: {totalCount})";
                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] {msg}"));
                    Helpers.Logger.WriteLog("SettlementAutomationCore", msg);
                }

                // ----------------------------------------------------
                // Step 5: Garrison Donation Phase
                // ----------------------------------------------------
                var garrisonOrders = new List<GarrisonOrder>();
                if (phasePolicy.GarrisonDonation)
                {
                    var garrisonRegistrations = AutomationRegistry.ActiveGarrisonProviders;
                    foreach (var reg in garrisonRegistrations)
                    {
                        try
                        {
                            var orders = reg.Provider.GetGarrisonOrders(MobileParty.MainParty, settlement);
                            if (orders != null) garrisonOrders.AddRange(orders);
                        }
                        catch {}
                    }
                }

                var garrisonParty = settlement.Town?.GarrisonParty;
                if (garrisonOrders.Count > 0 && garrisonParty != null)
                {
                    try
                    {
                        var donatorRoster = TroopRoster.CreateDummyTroopRoster();
                        var donatedLogParts = new List<string>();
                        foreach (var order in garrisonOrders)
                        {
                            if (order.Troop != null && order.Amount > 0)
                            {
                                int available = MobileParty.MainParty.MemberRoster.GetTroopCount(order.Troop);
                                int toDonate = Math.Min(order.Amount, available);
                                if (toDonate > 0)
                                {
                                    MobileParty.MainParty.MemberRoster.AddToCounts(order.Troop, -toDonate);
                                    garrisonParty.MemberRoster.AddToCounts(order.Troop, toDonate);
                                    donatorRoster.AddToCounts(order.Troop, toDonate);
                                    donatedLogParts.Add($"{toDonate}x {order.Troop.Name}");
                                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] Donated {toDonate}x {order.Troop.Name} to Garrison"));
                                }
                            }
                        }
                        if (donatorRoster.Count > 0)
                        {
                            CampaignEventDispatcher.Instance.OnTroopGivenToSettlement(Hero.MainHero, settlement, donatorRoster);
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Donated to Garrison at {settlement.Name}: {string.Join(", ", donatedLogParts)}");
                        }
                    }
                    catch {}
                }

                // ----------------------------------------------------
                // Step 6: Dungeon Donation Phase
                // ----------------------------------------------------
                var dungeonOrders = new List<DungeonOrder>();
                if (phasePolicy.DungeonDonation)
                {
                    var dungeonRegistrations = AutomationRegistry.ActiveDungeonProviders;
                    foreach (var reg in dungeonRegistrations)
                    {
                        try
                        {
                            var orders = reg.Provider.GetDungeonOrders(MobileParty.MainParty, settlement);
                            if (orders != null) dungeonOrders.AddRange(orders);
                        }
                        catch {}
                    }
                }

                if (dungeonOrders.Count > 0)
                {
                    try
                    {
                        var flattenedPrisoners = new FlattenedTroopRoster();
                        var dungeonLogParts = new List<string>();
                        foreach (var order in dungeonOrders)
                        {
                            if (order.Prisoner != null && order.Amount > 0)
                            {
                                int available = MobileParty.MainParty.PrisonRoster.GetTroopCount(order.Prisoner);
                                int toDonate = Math.Min(order.Amount, available);
                                if (toDonate > 0)
                                {
                                    MobileParty.MainParty.PrisonRoster.AddToCounts(order.Prisoner, -toDonate);
                                    settlement.Party.PrisonRoster.AddToCounts(order.Prisoner, toDonate);
                                    flattenedPrisoners.Add(order.Prisoner, toDonate, 0);
                                    dungeonLogParts.Add($"{toDonate}x {order.Prisoner.Name}");
                                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] Donated {toDonate}x {order.Prisoner.Name} to Dungeon"));
                                }
                            }
                        }
                        if (flattenedPrisoners.Count() > 0)
                        {
                            CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(MobileParty.MainParty, flattenedPrisoners, settlement);
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Donated to Dungeon at {settlement.Name}: {string.Join(", ", dungeonLogParts)}");
                        }
                    }
                    catch {}
                }

                // ----------------------------------------------------
                // Step 7: Priority Needs Phase (Need Fulfillment)
                // ----------------------------------------------------
                var activeRequests = RequestPolicy.SortRequests(AutomationRegistry.ActiveRequests).ToList();
                LogRequestSummary(settlement, activeRequests);

                if (phasePolicy.PriorityNeeds && activeRequests.Count > 0 && Hero.MainHero != null)
                {
                    var needsLogic = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                    if (needsLogic != null)
                    {
                        var state = new RequestExecutionState(
                            needsLogic,
                            Settings.Instance,
                            settlement,
                            marketReport,
                            Hero.MainHero.Gold,
                            Helpers.InventoryHelper.GetRosterWeight(MobileParty.MainParty.ItemRoster),
                            HerdingCalculator.GetRemainingAnimalSlots(MobileParty.MainParty));

                        foreach (var req in activeRequests)
                        {
                            ExecuteItemRequest(req, state);
                        }

                        if (state.ExecutedAnyItemTransfers && needsLogic.IsThereAnyChanges())
                        {
                            int initialGold = Hero.MainHero?.Gold ?? 0;
                            needsLogic.DoneLogic();
                            int finalGold = Hero.MainHero?.Gold ?? 0;
                            int goldDiff = finalGold - initialGold;
                            string goldDiffSign = goldDiff >= 0 ? "+" : "";
                            marketReport.AddGoldDelta(goldDiff);
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Prioritized items fulfilled at {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d). Purchased: {string.Join(", ", state.FulfilledItemParts)}");
                        }
                    }
                    else
                    {
                        Helpers.Logger.WriteLog("SettlementAutomationCore", $"Skipped {activeRequests.Count} prioritized item requests at {settlement.Name}: inventory logic was unavailable.");
                    }
                }

                // ----------------------------------------------------
                // Step 8: Fief Minimum Phase
                // ----------------------------------------------------
                var fiefRegistrations = AutomationRegistry.ActiveFiefProviders;
                if (phasePolicy.FiefMinimum)
                {
                    foreach (var reg in fiefRegistrations)
                    {
                        try
                        {
                            reg.Provider.ProcessFiefAutomation(MobileParty.MainParty, settlement, false);
                        }
                        catch (Exception ex)
                        {
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error in Fief Minimum Phase from {reg.ProviderName}: {ex.Message}");
                        }
                    }
                }

                // ----------------------------------------------------
                // Step 9: Free Trade Phase
                // ----------------------------------------------------
                if (phasePolicy.FreeTrade)
                {
                    var explicitReservations = AutomationRegistry.ActiveReservations;
                    var sellableItems = new List<SellableItem>();
                    var playerRoster = MobileParty.MainParty.ItemRoster;
                    var freeTradeLockKeys = InventoryLockHelper.GetCurrentLockKeys();
                    for (int i = 0; i < playerRoster.Count; i++)
                    {
                        var element = playerRoster.GetElementCopyAtIndex(i);
                        if (element.EquipmentElement.Item != null)
                        {
                            var item = element.EquipmentElement.Item;
                            if (InventoryLockHelper.IsLocked(element.EquipmentElement, freeTradeLockKeys))
                            {
                                sellableItems.Add(new SellableItem(element.EquipmentElement, 0));
                                continue;
                            }

                            int reserved = 0;
                            foreach (var res in explicitReservations)
                            {
                                if (res.MatchesItem(item))
                                {
                                    reserved = Math.Max(reserved, res.Quantity);
                                }
                            }
                            foreach (var req in activeRequests)
                            {
                                if (!RequestPolicy.CreatesImplicitSellReservation(req))
                                {
                                    continue;
                                }

                                if (req.MatchesItem(item))
                                {
                                    reserved = Math.Max(reserved, req.Quantity);
                                }
                            }
                            int available = Math.Max(0, element.Amount - reserved);
                            sellableItems.Add(new SellableItem(element.EquipmentElement, available));
                        }
                    }

                    var logic8 = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                    if (logic8 != null)
                    {
                        var context = TradeContextFactory.Create(MobileParty.MainParty, settlement, logic8, sellableItems);

                        var analyzers = AutomationRegistry.ActiveFreeTradeAnalyzers;
                        foreach (var reg in analyzers)
                        {
                            try
                            {
                                var proposal = reg.Provider.AnalyzeMarket(context);
                                if (proposal != null && proposal.Actions != null && proposal.Actions.Count > 0)
                                {
                                    context = ExecuteTradeProposal(proposal, context, logic8, marketReport, reg.ProviderName);
                                }
                            }
                            catch (Exception ex)
                            {
                                Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error executing free trade analyzer {reg.ProviderName}: {ex.Message}");
                            }
                        }

                        if (logic8.IsThereAnyChanges())
                        {
                            int initialGold = Hero.MainHero?.Gold ?? 0;
                            logic8.DoneLogic();
                            int finalGold = Hero.MainHero?.Gold ?? 0;
                            int goldDiff = finalGold - initialGold;
                            string goldDiffSign = goldDiff >= 0 ? "+" : "";
                            marketReport.AddGoldDelta(goldDiff);
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Free trade execution completed in {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d)");
                        }
                    }
                }

                ReportMarketActivity(settlement, marketReport);

                // ----------------------------------------------------
                // Step 10: Fief Surplus Phase
                // ----------------------------------------------------
                if (phasePolicy.FiefSurplus)
                {
                    foreach (var reg in fiefRegistrations)
                    {
                        try
                        {
                            reg.Provider.ProcessFiefAutomation(MobileParty.MainParty, settlement, true);
                        }
                        catch (Exception ex)
                        {
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error in Fief Surplus Phase from {reg.ProviderName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.Logger.WriteLog("SettlementAutomationCore", $"EXCEPTION in ExecuteBackgroundAutomation: {ex}");
            }

            try
            {
                OnAutomationCycleCompleted?.Invoke(settlement);
            }
            catch (Exception ex)
            {
                Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error executing OnAutomationCycleCompleted callback: {ex}");
            }

            // Refresh active game menu UI if we are in one to show the updated gold, prisoners, etc.
            try
            {
            Campaign.Current?.CurrentMenuContext?.Refresh();
            }
            catch {}
        }

        internal sealed class AutomationPhasePolicy
        {
            public bool Preparation { get; }
            public bool Requests { get; }
            public bool PreSell { get; }
            public bool Tavern { get; }
            public bool Recruitment { get; }
            public bool GarrisonDonation { get; }
            public bool DungeonDonation { get; }
            public bool PriorityNeeds { get; }
            public bool FiefMinimum { get; }
            public bool FreeTrade { get; }
            public bool FiefSurplus { get; }

            public bool CanRunAny =>
                Preparation ||
                Requests ||
                PreSell ||
                Tavern ||
                Recruitment ||
                GarrisonDonation ||
                DungeonDonation ||
                PriorityNeeds ||
                FiefMinimum ||
                FreeTrade ||
                FiefSurplus;

            private AutomationPhasePolicy(
                bool preparation,
                bool requests,
                bool preSell,
                bool tavern,
                bool recruitment,
                bool garrisonDonation,
                bool dungeonDonation,
                bool priorityNeeds,
                bool fiefMinimum,
                bool freeTrade,
                bool fiefSurplus)
            {
                Preparation = preparation;
                Requests = requests;
                PreSell = preSell;
                Tavern = tavern;
                Recruitment = recruitment;
                GarrisonDonation = garrisonDonation;
                DungeonDonation = dungeonDonation;
                PriorityNeeds = priorityNeeds;
                FiefMinimum = fiefMinimum;
                FreeTrade = freeTrade;
                FiefSurplus = fiefSurplus;
            }

            public static AutomationPhasePolicy Create(MobileParty party, Settlement settlement)
            {
                if (party == null || settlement == null)
                {
                    return ForFacts(false, false, false, false, false, false, false);
                }

                bool isHostile = false;
                bool isSameFaction = false;
                try
                {
                    isHostile = party.MapFaction != null &&
                                settlement.MapFaction != null &&
                                party.MapFaction.IsAtWarWith(settlement.MapFaction);
                    isSameFaction = party.MapFaction != null &&
                                    settlement.MapFaction != null &&
                                    party.MapFaction == settlement.MapFaction;
                }
                catch {}

                bool isOwnedByPlayerClan = settlement.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan;

                return ForFacts(
                    settlement.IsTown,
                    settlement.IsVillage,
                    settlement.IsCastle,
                    isHostile,
                    isSameFaction,
                    isOwnedByPlayerClan,
                    settlement.IsRaided || settlement.IsUnderRaid);
            }

            internal static AutomationPhasePolicy ForFacts(
                bool isTown,
                bool isVillage,
                bool isCastle,
                bool isHostile,
                bool isSameFaction,
                bool isOwnedByPlayerClan,
                bool isRaidedOrUnderRaid)
            {
                bool isTownOrVillage = isTown || isVillage;
                bool isKeep = isTown || isCastle;

                bool canUseMarket = !isRaidedOrUnderRaid &&
                                    ((isTown && !isHostile) ||
                                     isVillage);
                bool canUseTavern = isTown && !isHostile && !isRaidedOrUnderRaid;
                bool canRecruitNotables = isTownOrVillage && !isHostile && !isRaidedOrUnderRaid;
                bool canDonateToKeep = isKeep && isSameFaction && !isRaidedOrUnderRaid;
                bool canManageOwnedFief = isKeep && isOwnedByPlayerClan && !isHostile;

                return new AutomationPhasePolicy(
                    preparation: canUseMarket,
                    requests: canUseMarket,
                    preSell: canUseMarket,
                    tavern: canUseTavern,
                    recruitment: canRecruitNotables,
                    garrisonDonation: canDonateToKeep,
                    dungeonDonation: canDonateToKeep,
                    priorityNeeds: canUseMarket,
                    fiefMinimum: canManageOwnedFief,
                    freeTrade: canUseMarket,
                    fiefSurplus: canManageOwnedFief);
            }
        }

        private sealed class AutomationMarketReport
        {
            private const string CoreProviderName = "SettlementAutomationCore";
            private readonly MarketActivitySummary _summary = new MarketActivitySummary();
            private readonly List<ProviderStageSummary> _providerSummaries = new List<ProviderStageSummary>();

            public bool HasActivity => _summary.HasActivity;

            public void AddBought(EquipmentElement equipmentElement, int quantity, int gold)
            {
                AddBought(equipmentElement, quantity, gold, CoreProviderName, AutomationTransactionStage.FreeTrade);
            }

            public void AddBought(EquipmentElement equipmentElement, int quantity, int gold, string providerName, AutomationTransactionStage stage)
            {
                string itemName = GetItemName(equipmentElement);
                var category = GetItemCategory(equipmentElement);
                int marketValue = GetItemMarketValue(equipmentElement, quantity);
                _summary.AddBought(itemName, category, quantity, gold, marketValue);
                GetProviderSummary(providerName, stage).Summary.AddBought(itemName, category, quantity, gold, marketValue);
            }

            public void AddSold(EquipmentElement equipmentElement, int quantity, int gold)
            {
                AddSold(equipmentElement, quantity, gold, CoreProviderName, AutomationTransactionStage.FreeTrade);
            }

            public void AddSold(EquipmentElement equipmentElement, int quantity, int gold, string providerName, AutomationTransactionStage stage)
            {
                string itemName = GetItemName(equipmentElement);
                var category = GetItemCategory(equipmentElement);
                int marketValue = GetItemMarketValue(equipmentElement, quantity);
                _summary.AddSold(itemName, category, quantity, gold, marketValue);
                GetProviderSummary(providerName, stage).Summary.AddSold(itemName, category, quantity, gold, marketValue);
            }

            public void AddSlaughtered(EquipmentElement equipmentElement, int quantity)
            {
                AddSlaughtered(equipmentElement, quantity, CoreProviderName, AutomationTransactionStage.FreeTrade);
            }

            public void AddSlaughtered(EquipmentElement equipmentElement, int quantity, string providerName, AutomationTransactionStage stage)
            {
                string itemName = GetItemName(equipmentElement);
                var category = GetItemCategory(equipmentElement);
                int marketValue = GetItemMarketValue(equipmentElement, quantity);
                _summary.AddSlaughtered(itemName, category, quantity, marketValue);
                GetProviderSummary(providerName, stage).Summary.AddSlaughtered(itemName, category, quantity, marketValue);
            }

            public void AddGoldDelta(int goldDelta)
            {
                _summary.AddGoldDelta(goldDelta);
            }

            public IReadOnlyList<string> BuildInGameLines(Settlement settlement, string? cargoStatus)
            {
                return _summary.BuildInGameLines(settlement.Name.ToString(), cargoStatus);
            }

            public IReadOnlyList<AutomationProviderReport> BuildProviderReports()
            {
                return _providerSummaries
                    .Where(summary => summary.Summary.HasActivity)
                    .Select(summary => new AutomationProviderReport(
                        summary.ProviderName,
                        summary.Stage,
                        summary.Summary.BoughtItems,
                        summary.Summary.SoldItems,
                        summary.Summary.SlaughteredItems))
                    .ToList();
            }

            public string BuildLogSummary(Settlement settlement)
            {
                return _summary.BuildLogSummary(settlement.Name.ToString());
            }

            private ProviderStageSummary GetProviderSummary(string providerName, AutomationTransactionStage stage)
            {
                providerName = string.IsNullOrWhiteSpace(providerName) ? CoreProviderName : providerName;
                var summary = _providerSummaries.FirstOrDefault(item => item.ProviderName == providerName && item.Stage == stage);
                if (summary == null)
                {
                    summary = new ProviderStageSummary(providerName, stage);
                    _providerSummaries.Add(summary);
                }

                return summary;
            }

            private static string GetItemName(EquipmentElement equipmentElement)
            {
                return equipmentElement.Item?.Name?.ToString() ??
                       equipmentElement.Item?.StringId ??
                       "Unknown item";
            }

            private static InventoryItemCategory GetItemCategory(EquipmentElement equipmentElement)
            {
                return InventoryItemView.Classify(equipmentElement.Item);
            }

            private static int GetItemMarketValue(EquipmentElement equipmentElement, int quantity)
            {
                return Math.Max(0, equipmentElement.Item?.Value ?? 0) * Math.Max(0, quantity);
            }

            private sealed class ProviderStageSummary
            {
                public ProviderStageSummary(string providerName, AutomationTransactionStage stage)
                {
                    ProviderName = providerName;
                    Stage = stage;
                    Summary = new MarketActivitySummary();
                }

                public string ProviderName { get; }
                public AutomationTransactionStage Stage { get; }
                public MarketActivitySummary Summary { get; }
            }
        }

        private sealed class RequestExecutionState
        {
            public InventoryLogic Logic { get; }
            public Settings? Settings { get; }
            public Settlement Settlement { get; }
            public AutomationMarketReport MarketReport { get; }
            public int ProjectedGold;
            public float ProjectedWeight;
            public int ProjectedAnimalSlots;
            public bool ExecutedAnyItemTransfers;
            public List<string> FulfilledItemParts { get; } = new List<string>();

            public RequestExecutionState(
                InventoryLogic logic,
                Settings? settings,
                Settlement settlement,
                AutomationMarketReport marketReport,
                int projectedGold,
                float projectedWeight,
                int projectedAnimalSlots)
            {
                Logic = logic;
                Settings = settings;
                Settlement = settlement;
                MarketReport = marketReport;
                ProjectedGold = projectedGold;
                ProjectedWeight = projectedWeight;
                ProjectedAnimalSlots = projectedAnimalSlots;
            }
        }

        private static int GetProfileOrder(RequestProfile profile)
        {
            return RequestPolicy.GetProfileOrder(profile);
        }

        private static void LogRequestSummary(Settlement settlement, IReadOnlyList<AutomationRequest> requests)
        {
            if (requests.Count <= 0) return;

            foreach (var request in requests)
            {
            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Request gathered at {settlement.Name}: {DescribeRequest(request)}");
            }
        }

        private static void ExecuteItemRequest(AutomationRequest request, RequestExecutionState state)
        {
            if (request.Quantity <= 0)
            {
                LogRequestSkip(request, state, RejectedOrderReason.InvalidQuantity, "quantity is zero");
                return;
            }

            int goldReserve = GetGoldReserveForRequest(request, state.Settings);
            if (state.ProjectedGold <= goldReserve)
            {
                LogRequestSkip(request, state, RejectedOrderReason.GoldReserveReached, $"projected gold {state.ProjectedGold} is at or below reserve {goldReserve}");
                return;
            }

            if (request.QuantityMode == RequestQuantityMode.PurchaseCount)
            {
                ExecuteMarketPurchaseRequest(request, state, goldReserve);
                return;
            }

            if (request.QuantityMode == RequestQuantityMode.DesiredInventoryCount)
            {
                ExecuteInventoryTargetRequest(request, state, goldReserve);
                return;
            }

            LogRequestSkip(request, state, RejectedOrderReason.UnsupportedQuantityMode, $"unsupported quantity mode {request.QuantityMode}");
        }

        private static void ExecuteMarketPurchaseRequest(AutomationRequest request, RequestExecutionState state, int goldReserve)
        {
            if (request.MarketCandidates.Count <= 0)
            {
                LogRequestSkip(request, state, RejectedOrderReason.NoMarketCandidates, "no market candidates");
                return;
            }

            var marketElements = state.Logic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
            int remainingToBuy = request.Quantity;
            bool purchasedAny = false;
            string lastSkipReason = "no matching merchant stock";
            RejectedOrderReason lastSkipReasonCode = RejectedOrderReason.NoMatchingMerchantStock;

            foreach (var requestedCandidate in request.MarketCandidates)
            {
                if (remainingToBuy <= 0) break;
                if (state.ProjectedGold <= goldReserve)
                {
                    lastSkipReason = $"projected gold {state.ProjectedGold} reached reserve {goldReserve}";
                    lastSkipReasonCode = RejectedOrderReason.GoldReserveReached;
                    break;
                }

                if (requestedCandidate.Side != InventoryLogic.InventorySide.OtherInventory)
                {
                    lastSkipReason = "candidate was not from merchant inventory";
                    lastSkipReasonCode = RejectedOrderReason.CandidateNotFromMerchantInventory;
                    continue;
                }

                var matchingCandidates = new List<ItemRosterElement>();
                for (int i = 0; i < marketElements.Count; i++)
                {
                    var element = marketElements[i];
                    if (element.IsEmpty || element.Amount <= 0 || element.EquipmentElement.Item == null) continue;
                    if (requestedCandidate.MatchesEquipmentElement(element.EquipmentElement))
                    {
                        matchingCandidates.Add(element);
                    }
                }

                if (matchingCandidates.Count == 0)
                {
                    lastSkipReason = $"merchant no longer has {requestedCandidate.Item.Name}";
                    lastSkipReasonCode = RejectedOrderReason.MerchantStockMissing;
                    continue;
                }

                foreach (var candidateElement in matchingCandidates)
                {
                    if (remainingToBuy <= 0) break;
                    if (state.ProjectedGold <= goldReserve)
                    {
                        lastSkipReason = $"projected gold {state.ProjectedGold} reached reserve {goldReserve}";
                        lastSkipReasonCode = RejectedOrderReason.GoldReserveReached;
                        break;
                    }

                    if (TryAddPurchaseTransfer(request, candidateElement, remainingToBuy, goldReserve, state, out int purchasedCount, out RejectedOrderReason skipReasonCode, out string skipReason))
                    {
                        remainingToBuy -= purchasedCount;
                        purchasedAny = true;
                    }
                    else
                    {
                        lastSkipReason = skipReason;
                        lastSkipReasonCode = skipReasonCode;
                    }
                }
            }

            if (!purchasedAny)
            {
                LogRequestSkip(request, state, lastSkipReasonCode, lastSkipReason);
            }
            else if (remainingToBuy > 0)
            {
                Helpers.Logger.WriteLog("SettlementAutomationCore", $"Request partially fulfilled at {state.Settlement.Name}: {DescribeRequest(request)} remaining={remainingToBuy}");
            }
        }

        private static void ExecuteInventoryTargetRequest(AutomationRequest request, RequestExecutionState state, int goldReserve)
        {
            int currentHeld = 0;
            var playerElements = state.Logic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
            for (int i = 0; i < playerElements.Count; i++)
            {
                var element = playerElements[i];
                if (element.EquipmentElement.Item != null && request.MatchesItem(element.EquipmentElement.Item))
                {
                    currentHeld += element.Amount;
                }
            }

            int inventoryDeficit = request.Quantity - currentHeld;
            if (inventoryDeficit <= 0) return;

            var candidates = new List<ItemRosterElement>();
            var marketElements = state.Logic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
            for (int i = 0; i < marketElements.Count; i++)
            {
                var element = marketElements[i];
                if (element.EquipmentElement.Item != null && request.MatchesItem(element.EquipmentElement.Item) && element.Amount > 0)
                {
                    candidates.Add(element);
                }
            }

            if (candidates.Count == 0)
            {
                LogRequestSkip(request, state, RejectedOrderReason.NoMatchingMerchantStock, "no matching merchant stock");
                return;
            }

            var sortedCandidates = candidates.Select(candidate => new
            {
                Element = candidate,
                Price = state.Logic.GetItemPrice(candidate.EquipmentElement, true)
            }).OrderBy(candidate => candidate.Price).ToList();

            bool purchasedAny = false;
            string lastSkipReason = "no affordable matching stock";
            RejectedOrderReason lastSkipReasonCode = RejectedOrderReason.NoAffordableMatchingStock;
            foreach (var candidate in sortedCandidates)
            {
                if (inventoryDeficit <= 0) break;
                if (state.ProjectedGold <= goldReserve)
                {
                    lastSkipReason = $"projected gold {state.ProjectedGold} reached reserve {goldReserve}";
                    lastSkipReasonCode = RejectedOrderReason.GoldReserveReached;
                    break;
                }

                if (TryAddPurchaseTransfer(request, candidate.Element, inventoryDeficit, goldReserve, state, out int purchasedCount, out RejectedOrderReason skipReasonCode, out string skipReason))
                {
                    inventoryDeficit -= purchasedCount;
                    purchasedAny = true;
                }
                else
                {
                    lastSkipReason = skipReason;
                    lastSkipReasonCode = skipReasonCode;
                }
            }

            if (!purchasedAny)
            {
                LogRequestSkip(request, state, lastSkipReasonCode, lastSkipReason);
            }
            else if (inventoryDeficit > 0)
            {
                Helpers.Logger.WriteLog("SettlementAutomationCore", $"Request partially fulfilled at {state.Settlement.Name}: {DescribeRequest(request)} remaining={inventoryDeficit}");
            }
        }

        private static bool TryAddPurchaseTransfer(
            AutomationRequest request,
            ItemRosterElement candidateElement,
            int requestedCount,
            int goldReserve,
            RequestExecutionState state,
            out int purchasedCount,
            out RejectedOrderReason skipReasonCode,
            out string skipReason)
        {
            purchasedCount = 0;
            skipReasonCode = RejectedOrderReason.NoAffordableMatchingStock;
            skipReason = "";

            var item = candidateElement.EquipmentElement.Item;
            if (item == null)
            {
                skipReasonCode = RejectedOrderReason.CandidateItemMissing;
                skipReason = "candidate item was null";
                return false;
            }

            int price = state.Logic.GetItemPrice(candidateElement.EquipmentElement, true);
            if (!IsPriceAllowedForRequest(request, item, price, state.Settings))
            {
                skipReasonCode = RejectedOrderReason.PricePolicyExceeded;
                skipReason = $"{item.Name} price {price} exceeded {request.Profile} price policy";
                return false;
            }

            int toBuy = Math.Min(requestedCount, candidateElement.Amount);
            int maxAfford = (state.ProjectedGold - goldReserve) / Math.Max(1, price);
            toBuy = Math.Min(toBuy, maxAfford);
            if (toBuy <= 0)
            {
                skipReasonCode = RejectedOrderReason.GoldReserveBreach;
                skipReason = $"{item.Name} would breach reserve {goldReserve}";
                return false;
            }

            bool isCargo = !item.IsAnimal && !item.IsMountable;
            if (isCargo && (state.Settings?.LimitToInventoryCapacity ?? true))
            {
                float remainingCargoSpace = TradeContextFactory.CalculateFreeCargoCapacity(
                    MobileParty.MainParty.InventoryCapacity,
                    state.ProjectedWeight,
                    state.Settings?.ReserveCarryCapacityPercent ?? 0);
                if (item.Weight > 0f)
                {
                    int maxWeightBuy = (int)(remainingCargoSpace / item.Weight);
                    toBuy = Math.Min(toBuy, maxWeightBuy);
                }
            }

            bool isAnimalOrMount = item.IsAnimal || item.IsMountable;
            if (isAnimalOrMount)
            {
                toBuy = Math.Min(toBuy, state.ProjectedAnimalSlots);
            }

            if (toBuy <= 0)
            {
                skipReasonCode = isAnimalOrMount ? RejectedOrderReason.HerdingLimitExceeded : RejectedOrderReason.CargoCapacityExceeded;
                skipReason = isAnimalOrMount ? $"{item.Name} would exceed herding allowance" : $"{item.Name} would exceed cargo capacity";
                return false;
            }

            var command = TransferCommand.Transfer(
                toBuy,
                InventoryLogic.InventorySide.OtherInventory,
                InventoryLogic.InventorySide.PlayerInventory,
                new ItemRosterElement(candidateElement.EquipmentElement, toBuy),
                EquipmentIndex.None,
                EquipmentIndex.None,
                Hero.MainHero.CharacterObject
            );
            state.Logic.AddTransferCommand(command);
            state.ExecutedAnyItemTransfers = true;
            state.ProjectedGold -= toBuy * price;
            if (isCargo)
            {
                state.ProjectedWeight += toBuy * item.Weight;
            }
            if (isAnimalOrMount)
            {
                state.ProjectedAnimalSlots -= toBuy;
            }

            purchasedCount = toBuy;
            state.FulfilledItemParts.Add($"{toBuy}x {item.Name}");
            state.MarketReport.AddBought(candidateElement.EquipmentElement, toBuy, toBuy * price, request.RequestorId, AutomationTransactionStage.PriorityRequest);
            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Request purchased at {state.Settlement.Name}: {DescribeRequest(request)} -> {toBuy}x {item.Name} ({toBuy * price}d, {price}d each)");
            return true;
        }

        internal static string BuildRejectedOrderLogLine(RejectedOrderDetail detail)
        {
            return BuildRejectedOrderLogLine(
                detail.SettlementName,
                DescribeRequest(detail.Request),
                detail.Reason,
                detail.Detail);
        }

        internal static string BuildRejectedOrderLogLine(
            string settlementName,
            string requestDescription,
            RejectedOrderReason reason,
            string detail)
        {
            return $"Request skipped at {settlementName}: {requestDescription} [{reason}] ({detail})";
        }

        private static void LogRequestSkip(AutomationRequest request, RequestExecutionState state, RejectedOrderReason reason, string detail)
        {
            if (!(state.Settings?.LogRejectedOrderDetails ?? false)) return;

            var rejectedOrder = new RejectedOrderDetail(state.Settlement.Name.ToString(), request, reason, detail);
            Helpers.Logger.WriteLog("SettlementAutomationCore", BuildRejectedOrderLogLine(rejectedOrder));
        }

        private static string DescribeRequest(AutomationRequest request)
        {
            return RequestPolicy.DescribeRequest(request);
        }

        private static int GetGoldReserveForRequest(AutomationRequest request, Settings? settings)
        {
            int dailyWage = MobileParty.MainParty?.TotalWage ?? 0;
            int minimumGoldReserve = settings?.MinimumGoldReserve ?? 1000;
            int daysToKeep = settings?.MinDaysExpensesToKeep ?? 10;
            return RequestPolicy.GetGoldReserveForRequest(request, minimumGoldReserve, daysToKeep, dailyWage);
        }

        private static bool IsPriceAllowedForRequest(AutomationRequest request, ItemObject item, int price, Settings? settings)
        {
            return RequestPolicy.IsPriceAllowedForRequest(
                request,
                item,
                price,
                settings?.RoutinePriceLimitMultiplier ?? 1.5f,
                settings?.OpportunisticPriceLimitMultiplier ?? 1.1f);
        }

        private static void ReportMarketActivity(Settlement settlement, AutomationMarketReport marketReport)
        {
            if (!marketReport.HasActivity) return;

            var reportingMode = Settings.Instance?.CoreReportingModeSetting ?? CoreReportingMode.Full;
            if (reportingMode == CoreReportingMode.Off)
            {
                Helpers.Logger.WriteLog("SettlementAutomationCore", marketReport.BuildLogSummary(settlement));
                return;
            }

            string? cargoStatus = GetCargoStatus();
            var providerReports = marketReport.BuildProviderReports();
            var registeredReporters = AutomationRegistry.ActiveReportProviders;
            var displayedProviderKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool displayedAnyReport = false;

            foreach (var report in providerReports)
            {
                var reg = registeredReporters.FirstOrDefault(item =>
                    string.Equals(item.ProviderName, report.ProviderName, StringComparison.OrdinalIgnoreCase));
                if (reg == null) continue;

                try
                {
                    var context = new AutomationReportContext(settlement, reg.ProviderName, report, cargoStatus);
                    var lines = reg.Provider.BuildAutomationReportLines(context);
                    if (lines == null) continue;

                    bool displayedThisProviderReport = false;
                    uint headerColor = GetProviderHeaderColor(report.ProviderName, reg.Provider);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        DisplayReportMessage(line, headerColor);
                        displayedAnyReport = true;
                        displayedThisProviderReport = true;
                    }

                    if (displayedThisProviderReport)
                    {
                        displayedProviderKeys.Add(GetProviderReportKey(report));
                    }
                }
                catch (Exception ex)
                {
                    Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error building {report.Stage} automation report from {reg.ProviderName}: {ex.Message}");
                }
            }

            var coreSummaryReports = SelectCoreSummaryReports(providerReports, displayedProviderKeys, reportingMode);

            foreach (var report in coreSummaryReports)
            {
                string line = BuildGenericProviderReportLine(report);
                if (string.IsNullOrWhiteSpace(line)) continue;

                DisplayCoreReportMessage(line);
                displayedAnyReport = true;
            }

            if (displayedAnyReport && !string.IsNullOrWhiteSpace(cargoStatus))
            {
                DisplayCoreReportMessage($"[Core] Cargo: {cargoStatus}");
            }

            Helpers.Logger.WriteLog("SettlementAutomationCore", marketReport.BuildLogSummary(settlement));
        }

        private static string GetProviderReportKey(AutomationProviderReport report)
        {
            return $"{report.ProviderName}:{report.Stage}";
        }

        internal static IReadOnlyList<AutomationProviderReport> SelectCoreSummaryReports(
            IReadOnlyList<AutomationProviderReport> providerReports,
            ISet<string> displayedProviderKeys,
            CoreReportingMode reportingMode)
        {
            if (reportingMode == CoreReportingMode.Off)
            {
                return new List<AutomationProviderReport>();
            }

            if (reportingMode == CoreReportingMode.Full)
            {
                return providerReports;
            }

            return providerReports
                .Where(report => !displayedProviderKeys.Contains(GetProviderReportKey(report)))
                .ToList();
        }

        internal static uint GetProviderHeaderColor(string providerName, IAutomationReportProvider? reportProvider = null)
        {
            if (reportProvider is IAutomationReportStyleProvider styleProvider && styleProvider.ReportHeaderColor.HasValue)
            {
                return styleProvider.ReportHeaderColor.Value;
            }

            return DeriveProviderHeaderColor(providerName);
        }

        internal static uint DeriveProviderHeaderColor(string providerName)
        {
            string normalizedName = string.IsNullOrWhiteSpace(providerName)
                ? "SettlementAutomationCore"
                : providerName.Trim().ToUpperInvariant();

            uint hash = 2166136261u;
            unchecked
            {
                foreach (char c in normalizedName)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
            }

            double hue = hash % 360;
            double saturation = 0.62 + ((hash >> 8) % 18) / 100.0;
            double lightness = 0.58 + ((hash >> 16) % 10) / 100.0;
            return HslToRgba(hue, saturation, lightness);
        }

        private static uint HslToRgba(double hue, double saturation, double lightness)
        {
            double chroma = (1.0 - Math.Abs(2.0 * lightness - 1.0)) * saturation;
            double huePrime = hue / 60.0;
            double x = chroma * (1.0 - Math.Abs(huePrime % 2.0 - 1.0));

            double r1 = 0;
            double g1 = 0;
            double b1 = 0;

            if (huePrime < 1.0)
            {
                r1 = chroma;
                g1 = x;
            }
            else if (huePrime < 2.0)
            {
                r1 = x;
                g1 = chroma;
            }
            else if (huePrime < 3.0)
            {
                g1 = chroma;
                b1 = x;
            }
            else if (huePrime < 4.0)
            {
                g1 = x;
                b1 = chroma;
            }
            else if (huePrime < 5.0)
            {
                r1 = x;
                b1 = chroma;
            }
            else
            {
                r1 = chroma;
                b1 = x;
            }

            double match = lightness - chroma / 2.0;
            uint red = ToColorByte(r1 + match);
            uint green = ToColorByte(g1 + match);
            uint blue = ToColorByte(b1 + match);

            return (red << 24) | (green << 16) | (blue << 8) | 0xFFu;
        }

        private static uint ToColorByte(double value)
        {
            return (uint)Math.Max(0, Math.Min(255, (int)Math.Round(value * 255.0)));
        }

        private static void DisplayReportMessage(string line, uint color)
        {
            InformationManager.DisplayMessage(new InformationMessage(line, Color.FromUint(color)));
        }

        private static void DisplayCoreReportMessage(string line)
        {
            InformationManager.DisplayMessage(new InformationMessage(line));
        }

        internal static string BuildGenericProviderReportLine(AutomationProviderReport report)
        {
            string summary = BuildGenericProviderActivitySummary(report);
            if (string.IsNullOrWhiteSpace(summary)) return "";

            return $"[Core] {GetCompactReportLabel(report)}: {summary}";
        }

        internal static string BuildGenericProviderActivitySummary(AutomationProviderReport report)
        {
            var parts = new List<string>();
            if (report.SoldItems.Count > 0)
            {
                parts.Add($"sold {FormatReportItems(report.SoldItems, isSale: true)}");
            }
            if (report.BoughtItems.Count > 0)
            {
                parts.Add($"bought {FormatReportItems(report.BoughtItems, isSale: false)}");
            }
            if (report.SlaughteredItems.Count > 0)
            {
                parts.Add($"slaughtered {FormatReportItems(report.SlaughteredItems, isSale: false, includeGold: false)}");
            }

            return string.Join("; ", parts);
        }

        private static string FormatReportItems(IReadOnlyList<AutomationReportItem> items, bool isSale, bool includeGold = true)
        {
            var visible = items
                .GroupBy(item => item.CategoryName)
                .OrderBy(group => GetCategorySortOrder(group.First().Category))
                .ThenBy(group => group.Key)
                .Select(group => FormatReportCategoryTotal(group.Key, group.ToList(), isSale, includeGold))
                .ToList();

            return string.Join(", ", visible);
        }

        private static string FormatReportCategoryTotal(string categoryName, IReadOnlyList<AutomationReportItem> items, bool isSale, bool includeGold)
        {
            int quantity = items.Sum(item => item.Quantity);
            int gold = items.Sum(item => item.Gold);
            if (!includeGold || gold == 0)
            {
                return $"{categoryName} {quantity}x";
            }

            string sign = isSale ? "+" : "-";
            return $"{categoryName} {quantity}x ({sign}{Math.Abs(gold)}d)";
        }

        private static string GetCompactProviderName(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return "Automation";
            if (string.Equals(providerName, "SettlementAutomationCore", StringComparison.OrdinalIgnoreCase)) return "Core";
            if (providerName.EndsWith("Manager", StringComparison.OrdinalIgnoreCase))
            {
                return providerName.Substring(0, providerName.Length - "Manager".Length);
            }
            if (providerName.EndsWith("Optimizer", StringComparison.OrdinalIgnoreCase))
            {
                return providerName.Substring(0, providerName.Length - "Optimizer".Length);
            }

            return providerName;
        }

        private static string GetCompactReportLabel(AutomationProviderReport report)
        {
            if (report.Stage == AutomationTransactionStage.FreeTrade)
            {
                return "Free trade";
            }

            return $"{GetCompactProviderName(report.ProviderName)} {GetStageLabel(report.Stage)}";
        }

        private static int GetCategorySortOrder(InventoryItemCategory category)
        {
            if ((category & InventoryItemCategory.Armor) == InventoryItemCategory.Armor) return 0;
            if ((category & InventoryItemCategory.Weapon) == InventoryItemCategory.Weapon) return 1;
            if ((category & InventoryItemCategory.Food) == InventoryItemCategory.Food) return 2;
            if ((category & InventoryItemCategory.Mount) == InventoryItemCategory.Mount) return 3;
            if ((category & InventoryItemCategory.PackAnimal) == InventoryItemCategory.PackAnimal) return 4;
            if ((category & InventoryItemCategory.Livestock) == InventoryItemCategory.Livestock) return 5;
            if ((category & InventoryItemCategory.TradeGood) == InventoryItemCategory.TradeGood) return 6;
            return 7;
        }

        private static string FormatReportItem(AutomationReportItem item, bool isSale, bool includeGold)
        {
            if (!includeGold || item.Gold == 0)
            {
                return $"{item.Quantity}x {item.ItemName}";
            }

            string sign = isSale ? "+" : "-";
            return $"{item.Quantity}x {item.ItemName} ({sign}{Math.Abs(item.Gold)}d)";
        }

        private static string GetStageLabel(AutomationTransactionStage stage)
        {
            switch (stage)
            {
                case AutomationTransactionStage.PreSell:
                    return "pre-sell";
                case AutomationTransactionStage.PriorityRequest:
                    return "requests";
                case AutomationTransactionStage.FreeTrade:
                    return "free trade";
                default:
                    return "market";
            }
        }

        private static string? GetCargoStatus()
        {
            var party = MobileParty.MainParty;
            if (party == null || party.InventoryCapacity <= 0f) return null;

            float currentWeight = Helpers.InventoryHelper.GetRosterWeight(party.ItemRoster);
            float capacity = party.InventoryCapacity;
            int percent = (int)Math.Round((currentWeight / capacity) * 100);
            return $"{(int)currentWeight} / {(int)capacity} capacity ({percent}%)";
        }

        internal static bool CanRunNotableRecruitment(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null)
            {
                return false;
            }

            bool isAtWar = false;
            try
            {
                isAtWar = party.MapFaction != null &&
                          settlement.MapFaction != null &&
                          party.MapFaction.IsAtWarWith(settlement.MapFaction);
            }
            catch {}

            return CanRunNotableRecruitment(settlement.IsTown, settlement.IsVillage, isAtWar, settlement.IsRaided, settlement.IsUnderRaid);
        }

        internal static bool CanRunNotableRecruitment(bool isTown, bool isVillage, bool isAtWar, bool isRaided, bool isUnderRaid)
        {
            if (!isTown && !isVillage)
            {
                return false;
            }

            if (isAtWar || isRaided || isUnderRaid)
            {
                return false;
            }

            return true;
        }

        internal static IReadOnlyList<SellableItem> ConsumeSellableItems(IReadOnlyList<SellableItem> sellableItems, EquipmentElement equipmentElement, int quantity)
        {
            if (sellableItems == null || sellableItems.Count == 0 || quantity <= 0)
            {
                return sellableItems ?? Array.Empty<SellableItem>();
            }

            var updated = new List<SellableItem>(sellableItems.Count);
            bool consumed = false;
            foreach (var sellableItem in sellableItems)
            {
                if (!consumed && sellableItem.Matches(equipmentElement))
                {
                    updated.Add(new SellableItem(
                        sellableItem.EquipmentElement,
                        Math.Max(0, sellableItem.AvailableQuantity - quantity)));
                    consumed = true;
                }
                else
                {
                    updated.Add(sellableItem);
                }
            }

            return updated;
        }

        private static TradeContext ExecuteTradeProposal(TradeProposal proposal, TradeContext context, InventoryLogic logic, AutomationMarketReport marketReport, string providerName)
        {
            var sells = proposal.Actions.Where(a => a.ActionType == TradeActionType.Sell).ToList();
            var slaughters = proposal.Actions.Where(a => a.ActionType == TradeActionType.Slaughter).ToList();
            var buys = proposal.Actions.Where(a => a.ActionType == TradeActionType.Buy).ToList();

            int availableGold = context.AvailableGold;
            float freeCargo = context.FreeCargoCapacity;
            int freeAnimalSlots = context.FreeAnimalSlots;
            bool enforceCargo = context.EnforceCargoLimit;
            IReadOnlyList<SellableItem> sellableItems = context.SellableItems;

            // 1. Sells
            foreach (var action in sells)
            {
                if (action.EquipmentElement.Item == null) continue;
                var sellable = sellableItems.FirstOrDefault(s => s.Matches(action.EquipmentElement));
                if (sellable == null || sellable.AvailableQuantity <= 0) continue;

                int toSell = Math.Min(action.Quantity, sellable.AvailableQuantity);
                if (toSell > 0)
                {
                    int price = logic.GetItemPrice(action.EquipmentElement, false); // false = sell price
                    var command = TransferCommand.Transfer(
                        toSell,
                        InventoryLogic.InventorySide.PlayerInventory,
                        InventoryLogic.InventorySide.OtherInventory,
                        new ItemRosterElement(action.EquipmentElement, toSell),
                        EquipmentIndex.None,
                        EquipmentIndex.None,
                        Hero.MainHero.CharacterObject
                    );
                    logic.AddTransferCommand(command);

                    availableGold += toSell * price;
                    sellableItems = ConsumeSellableItems(sellableItems, action.EquipmentElement, toSell);
                    marketReport.AddSold(action.EquipmentElement, toSell, toSell * price, providerName, AutomationTransactionStage.FreeTrade);
                    if (!action.EquipmentElement.Item.IsAnimal && !action.EquipmentElement.Item.IsMountable)
                    {
                        freeCargo += toSell * action.EquipmentElement.Item.Weight;
                    }
                    else
                    {
                        freeAnimalSlots += toSell;
                    }
                }
            }

            // 2. Slaughters
            foreach (var action in slaughters)
            {
                if (action.EquipmentElement.Item != null && action.Quantity > 0)
                {
                    var itemRosterEl = new ItemRosterElement(action.EquipmentElement, action.Quantity);
                    if (logic.CanSlaughterItem(itemRosterEl, InventoryLogic.InventorySide.PlayerInventory))
                    {
                        logic.SlaughterItem(itemRosterEl);
                        sellableItems = ConsumeSellableItems(sellableItems, action.EquipmentElement, action.Quantity);
                        marketReport.AddSlaughtered(action.EquipmentElement, action.Quantity, providerName, AutomationTransactionStage.FreeTrade);
                        if (!action.EquipmentElement.Item.IsAnimal && !action.EquipmentElement.Item.IsMountable)
                        {
                            freeCargo += action.Quantity * action.EquipmentElement.Item.Weight;
                        }
                        else
                        {
                            freeAnimalSlots += action.Quantity;
                        }
                    }
                }
            }

            // 3. Buys
            foreach (var action in buys)
            {
                if (action.EquipmentElement.Item == null) continue;
                int price = logic.GetItemPrice(action.EquipmentElement, true); // true = buy price

                int toBuy = action.Quantity;
                int maxAfford = availableGold / Math.Max(1, price);
                toBuy = Math.Min(toBuy, maxAfford);

                bool isCargo = !action.EquipmentElement.Item.IsAnimal && !action.EquipmentElement.Item.IsMountable;
                if (isCargo && enforceCargo)
                {
                    float itemWeight = action.EquipmentElement.Item.Weight;
                    if (itemWeight > 0)
                    {
                        int maxWeightBuy = (int)(freeCargo / itemWeight);
                        toBuy = Math.Min(toBuy, maxWeightBuy);
                    }
                }

                bool isAnimalOrMount = action.EquipmentElement.Item.IsAnimal || action.EquipmentElement.Item.IsMountable;
                if (isAnimalOrMount)
                {
                    toBuy = Math.Min(toBuy, freeAnimalSlots);
                }

                if (toBuy > 0)
                {
                    var command = TransferCommand.Transfer(
                        toBuy,
                        InventoryLogic.InventorySide.OtherInventory,
                        InventoryLogic.InventorySide.PlayerInventory,
                        new ItemRosterElement(action.EquipmentElement, toBuy),
                        EquipmentIndex.None,
                        EquipmentIndex.None,
                        Hero.MainHero.CharacterObject
                    );
                    logic.AddTransferCommand(command);

                    availableGold -= toBuy * price;
                    marketReport.AddBought(action.EquipmentElement, toBuy, toBuy * price, providerName, AutomationTransactionStage.FreeTrade);
                    if (isCargo)
                    {
                        freeCargo -= toBuy * action.EquipmentElement.Item.Weight;
                    }
                    else
                    {
                        freeAnimalSlots -= toBuy;
                    }
                }
            }

            return new TradeContext(
                context.Settlement,
                context.Party,
                logic,
                availableGold,
                freeCargo,
                enforceCargo,
                freeAnimalSlots,
                context.MaxPackAnimalPurchases,
                sellableItems);
        }
    }

    public class SettlementAutomationCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (party == MobileParty.MainParty && settlement != null && (settlement.IsTown || settlement.IsVillage || settlement.IsCastle))
            {
                SubModule.QueueBackgroundTrade(settlement);
            }
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
