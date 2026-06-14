using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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
                // ----------------------------------------------------
                // Step 0: Gather Prioritized Requests and Reservations
                // ----------------------------------------------------
                AutomationRegistry.ClearRequests();
                AutomationRegistry.ClearReservations();
                AutomationRequestContext requestContext;
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

                // ----------------------------------------------------
                // Step 1: Pre-Sell Phase (Revenue Generation)
                // ----------------------------------------------------
                var preSellProviders = AutomationRegistry.ActivePreSellProviders;
                var preSellOrders = new List<TradeOrder>();
                foreach (var reg in preSellProviders)
                {
                    try
                    {
                        var orders = reg.Provider.GetPreSellOrders(MobileParty.MainParty, settlement);
                        if (orders != null) preSellOrders.AddRange(orders);
                    }
                    catch (Exception ex)
                    {
                        Helpers.Logger.WriteLog("SettlementAutomationCore", $"Error gathering pre-sell orders from {reg.ProviderName}: {ex.Message}");
                    }
                }

                if (preSellOrders.Count > 0)
                {
                    var logic1 = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                    if (logic1 != null)
                    {
                        bool executedAny = false;
                        var preSoldList = new List<string>();
                        foreach (var order in preSellOrders)
                        {
                            if (!order.IsBuy && order.EquipmentElement.Item != null) // Pre-sell only supports selling!
                            {
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
                            }
                        }
                        if (executedAny && logic1.IsThereAnyChanges())
                        {
                            int initialGold = Hero.MainHero?.Gold ?? 0;
                            logic1.DoneLogic();
                            int finalGold = Hero.MainHero?.Gold ?? 0;
                            int goldDiff = finalGold - initialGold;
                            string goldDiffSign = goldDiff >= 0 ? "+" : "";
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Auto-pre-sold in {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d). Sold: {string.Join(", ", preSoldList)}");
                        }
                    }
                }

                // ----------------------------------------------------
                // Step 2: Tavern / Ransom & Mercenaries Phase
                // ----------------------------------------------------
                var ransomRegistrations = AutomationRegistry.ActiveRansomProviders;
                var ransomOrders = new List<RansomOrder>();
                var mercOrders = new List<MercenaryRecruitOrder>();
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
                // Step 3: Notable Recruitment Phase
                // ----------------------------------------------------
                var recruitRegistrations = AutomationRegistry.ActiveRecruitProviders;
                var recruitOrders = new List<RecruitOrder>();
                foreach (var reg in recruitRegistrations)
                {
                    try
                    {
                        var orders = reg.Provider.GetRecruitOrders(MobileParty.MainParty, settlement);
                        if (orders != null) recruitOrders.AddRange(orders);
                    }
                    catch {}
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

                        // Check party size limit before recruiting
                        int currentSize = MobileParty.MainParty.MemberRoster.TotalManCount;
                        int limit = MobileParty.MainParty.Party.PartySizeLimit;

                        bool canOverRecruit = false;
                        try
                        {
                            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                            {
                                if (assembly.GetName().Name == "PartyManager")
                                {
                                    var settingsType = assembly.GetType("PartyManager.Settings");
                                    if (settingsType != null)
                                    {
                                        var instanceProp = settingsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                        var settingsInstance = instanceProp?.GetValue(null);
                                        if (settingsInstance != null)
                                        {
                                            bool enableGarrisonDonation = (bool)(settingsType.GetProperty("EnableGarrisonDonation")?.GetValue(settingsInstance) ?? false);
                                            int maxGarrisonSize = (int)(settingsType.GetProperty("MaxGarrisonSize")?.GetValue(settingsInstance) ?? 400);

                                            canOverRecruit = enableGarrisonDonation && settlement.Town != null &&
                                                             settlement.Town.GarrisonParty != null &&
                                                             settlement.Town.GarrisonParty.MemberRoster.TotalManCount < maxGarrisonSize;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        catch {}

                        if (currentSize >= limit && !canOverRecruit) continue;

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
                // Step 4: Garrison Donation Phase
                // ----------------------------------------------------
                var garrisonRegistrations = AutomationRegistry.ActiveGarrisonProviders;
                var garrisonOrders = new List<GarrisonOrder>();
                foreach (var reg in garrisonRegistrations)
                {
                    try
                    {
                        var orders = reg.Provider.GetGarrisonOrders(MobileParty.MainParty, settlement);
                        if (orders != null) garrisonOrders.AddRange(orders);
                    }
                    catch {}
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
                // Step 5: Dungeon Donation Phase
                // ----------------------------------------------------
                var dungeonRegistrations = AutomationRegistry.ActiveDungeonProviders;
                var dungeonOrders = new List<DungeonOrder>();
                foreach (var reg in dungeonRegistrations)
                {
                    try
                    {
                        var orders = reg.Provider.GetDungeonOrders(MobileParty.MainParty, settlement);
                        if (orders != null) dungeonOrders.AddRange(orders);
                    }
                    catch {}
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
                // Step 6: Priority Needs Phase (Need Fulfillment)
                // ----------------------------------------------------
                var activeRequests = AutomationRegistry.ActiveRequests
                    .OrderBy(r => GetProfileOrder(r.Profile))
                    .ThenByDescending(r => r.Priority)
                    .ToList();
                LogRequestSummary(settlement, activeRequests);

                if (activeRequests.Count > 0 && Hero.MainHero != null)
                {
                    var needsLogic = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                    if (needsLogic != null)
                    {
                        var state = new RequestExecutionState(
                            needsLogic,
                            Settings.Instance,
                            settlement,
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
                            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Prioritized items fulfilled at {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d). Purchased: {string.Join(", ", state.FulfilledItemParts)}");
                        }
                    }
                    else
                    {
                        Helpers.Logger.WriteLog("SettlementAutomationCore", $"Skipped {activeRequests.Count} prioritized item requests at {settlement.Name}: inventory logic was unavailable.");
                    }
                }

                // ----------------------------------------------------
                // Step 7: Fief Minimum Phase
                // ----------------------------------------------------
                var fiefRegistrations = AutomationRegistry.ActiveFiefProviders;
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

                // ----------------------------------------------------
                // Step 8: Free Trade Phase
                // ----------------------------------------------------
                var explicitReservations = AutomationRegistry.ActiveReservations;
                var sellableItems = new List<SellableItem>();
                var playerRoster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < playerRoster.Count; i++)
                {
                    var element = playerRoster.GetElementCopyAtIndex(i);
                    if (element.EquipmentElement.Item != null)
                    {
                        var item = element.EquipmentElement.Item;
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
                            if (req.QuantityMode == RequestQuantityMode.PurchaseCount)
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

                var coreSettingsForTrade = Settings.Instance;
                var mainPartyForTrade = MobileParty.MainParty!;
                var mainHeroForTrade = Hero.MainHero!;
                int dailyWage = mainPartyForTrade.TotalWage;
                int minimumGoldReserveForTrade = coreSettingsForTrade?.MinimumGoldReserve ?? 1000;
                int minDaysExpensesForTrade = coreSettingsForTrade?.MinDaysExpensesToKeep ?? 10;
                int expenseReserve = Math.Max(minimumGoldReserveForTrade, dailyWage * minDaysExpensesForTrade);
                int availableGold = Math.Max(0, mainHeroForTrade.Gold - expenseReserve);
                float currentWeightForTrade = Helpers.InventoryHelper.GetRosterWeight(mainPartyForTrade.ItemRoster);
                float freeCargo = Math.Max(0f, mainPartyForTrade.InventoryCapacity - currentWeightForTrade);
                int freeAnimalSlots = HerdingCalculator.GetRemainingAnimalSlots(mainPartyForTrade);

                var logic8 = Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                if (logic8 != null)
                {
                    var context = new TradeContext(
                        settlement,
                        MobileParty.MainParty,
                        logic8,
                        availableGold,
                        freeCargo,
                        coreSettingsForTrade?.LimitToInventoryCapacity ?? true,
                        freeAnimalSlots,
                        freeAnimalSlots,
                        sellableItems
                    );

                    var analyzers = AutomationRegistry.ActiveFreeTradeAnalyzers;
                    foreach (var reg in analyzers)
                    {
                        try
                        {
                            var proposal = reg.Provider.AnalyzeMarket(context);
                            if (proposal != null && proposal.Actions != null && proposal.Actions.Count > 0)
                            {
                                ExecuteTradeProposal(proposal, context, logic8);
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
                        Helpers.Logger.WriteLog("SettlementAutomationCore", $"Free trade execution completed in {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d)");
                    }
                }

                // ----------------------------------------------------
                // Step 9: Fief Surplus Phase
                // ----------------------------------------------------
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

        private sealed class RequestExecutionState
        {
            public InventoryLogic Logic { get; }
            public Settings? Settings { get; }
            public Settlement Settlement { get; }
            public int ProjectedGold;
            public float ProjectedWeight;
            public int ProjectedAnimalSlots;
            public bool ExecutedAnyItemTransfers;
            public List<string> FulfilledItemParts { get; } = new List<string>();

            public RequestExecutionState(
                InventoryLogic logic,
                Settings? settings,
                Settlement settlement,
                int projectedGold,
                float projectedWeight,
                int projectedAnimalSlots)
            {
                Logic = logic;
                Settings = settings;
                Settlement = settlement;
                ProjectedGold = projectedGold;
                ProjectedWeight = projectedWeight;
                ProjectedAnimalSlots = projectedAnimalSlots;
            }
        }

        private static int GetProfileOrder(RequestProfile profile)
        {
            return profile switch
            {
                RequestProfile.Critical => 0,
                RequestProfile.Essential => 1,
                RequestProfile.Routine => 2,
                RequestProfile.Opportunistic => 3,
                RequestProfile.Luxury => 4,
                _ => 99
            };
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
                LogRequestSkip(request, state, "quantity is zero");
                return;
            }

            int goldReserve = GetGoldReserveForRequest(request, state.Settings);
            if (state.ProjectedGold <= goldReserve)
            {
                LogRequestSkip(request, state, $"projected gold {state.ProjectedGold} is at or below reserve {goldReserve}");
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

            LogRequestSkip(request, state, $"unsupported quantity mode {request.QuantityMode}");
        }

        private static void ExecuteMarketPurchaseRequest(AutomationRequest request, RequestExecutionState state, int goldReserve)
        {
            if (request.MarketCandidates.Count <= 0)
            {
                LogRequestSkip(request, state, "no market candidates");
                return;
            }

            var marketElements = state.Logic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
            int remainingToBuy = request.Quantity;
            bool purchasedAny = false;
            string lastSkipReason = "no matching merchant stock";

            foreach (var requestedCandidate in request.MarketCandidates)
            {
                if (remainingToBuy <= 0) break;
                if (state.ProjectedGold <= goldReserve)
                {
                    lastSkipReason = $"projected gold {state.ProjectedGold} reached reserve {goldReserve}";
                    break;
                }

                if (requestedCandidate.Side != InventoryLogic.InventorySide.OtherInventory)
                {
                    lastSkipReason = "candidate was not from merchant inventory";
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
                    continue;
                }

                foreach (var candidateElement in matchingCandidates)
                {
                    if (remainingToBuy <= 0) break;
                    if (state.ProjectedGold <= goldReserve)
                    {
                        lastSkipReason = $"projected gold {state.ProjectedGold} reached reserve {goldReserve}";
                        break;
                    }

                    if (TryAddPurchaseTransfer(request, candidateElement, remainingToBuy, goldReserve, state, out int purchasedCount, out string skipReason))
                    {
                        remainingToBuy -= purchasedCount;
                        purchasedAny = true;
                    }
                    else
                    {
                        lastSkipReason = skipReason;
                    }
                }
            }

            if (!purchasedAny)
            {
                LogRequestSkip(request, state, lastSkipReason);
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
                LogRequestSkip(request, state, "no matching merchant stock");
                return;
            }

            var sortedCandidates = candidates.Select(candidate => new
            {
                Element = candidate,
                Price = state.Logic.GetItemPrice(candidate.EquipmentElement, true)
            }).OrderBy(candidate => candidate.Price).ToList();

            bool purchasedAny = false;
            string lastSkipReason = "no affordable matching stock";
            foreach (var candidate in sortedCandidates)
            {
                if (inventoryDeficit <= 0) break;
                if (state.ProjectedGold <= goldReserve)
                {
                    lastSkipReason = $"projected gold {state.ProjectedGold} reached reserve {goldReserve}";
                    break;
                }

                if (TryAddPurchaseTransfer(request, candidate.Element, inventoryDeficit, goldReserve, state, out int purchasedCount, out string skipReason))
                {
                    inventoryDeficit -= purchasedCount;
                    purchasedAny = true;
                }
                else
                {
                    lastSkipReason = skipReason;
                }
            }

            if (!purchasedAny)
            {
                LogRequestSkip(request, state, lastSkipReason);
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
            out string skipReason)
        {
            purchasedCount = 0;
            skipReason = "";

            var item = candidateElement.EquipmentElement.Item;
            if (item == null)
            {
                skipReason = "candidate item was null";
                return false;
            }

            int price = state.Logic.GetItemPrice(candidateElement.EquipmentElement, true);
            if (!IsPriceAllowedForRequest(request, item, price, state.Settings))
            {
                skipReason = $"{item.Name} price {price} exceeded {request.Profile} price policy";
                return false;
            }

            int toBuy = Math.Min(requestedCount, candidateElement.Amount);
            int maxAfford = (state.ProjectedGold - goldReserve) / Math.Max(1, price);
            toBuy = Math.Min(toBuy, maxAfford);
            if (toBuy <= 0)
            {
                skipReason = $"{item.Name} would breach reserve {goldReserve}";
                return false;
            }

            bool isCargo = !item.IsAnimal && !item.IsMountable;
            if (isCargo && (state.Settings?.LimitToInventoryCapacity ?? true))
            {
                float capacity = MobileParty.MainParty.InventoryCapacity;
                float remainingCargoSpace = capacity - state.ProjectedWeight;
                if (remainingCargoSpace < 0f) remainingCargoSpace = 0f;
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
            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Request purchased at {state.Settlement.Name}: {DescribeRequest(request)} -> {toBuy}x {item.Name} ({toBuy * price}d, {price}d each)");
            return true;
        }

        private static void LogRequestSkip(AutomationRequest request, RequestExecutionState state, string reason)
        {
            Helpers.Logger.WriteLog("SettlementAutomationCore", $"Request skipped at {state.Settlement.Name}: {DescribeRequest(request)} ({reason})");
        }

        private static string DescribeRequest(AutomationRequest request)
        {
            string target = request.QuantityMode == RequestQuantityMode.PurchaseCount
                ? $"{request.MarketCandidates.Count} market candidates"
                : request.TargetId;
            string reserve = request.BudgetPolicy == BudgetPolicyKind.ExplicitReserve
                ? $"explicitReserve={request.ExplicitGoldReserve}"
                : "coreReserve";
            return $"{request.RequestorId} {request.Profile}/{request.Priority} {request.QuantityMode} {request.Type}:{target} qty={request.Quantity} {reserve}";
        }

        private static int GetGoldReserveForRequest(AutomationRequest request, Settings? settings)
        {
            if (request.BudgetPolicy == BudgetPolicyKind.ExplicitReserve)
            {
                return request.ExplicitGoldReserve;
            }

            int dailyWage = MobileParty.MainParty?.TotalWage ?? 0;
            int minimumGoldReserve = settings?.MinimumGoldReserve ?? 1000;
            int daysToKeep = settings?.MinDaysExpensesToKeep ?? 10;
            return Math.Max(minimumGoldReserve, dailyWage * daysToKeep);
        }

        private static bool IsPriceAllowedForRequest(AutomationRequest request, ItemObject item, int price, Settings? settings)
        {
            if (item == null) return false;

            float? maxMultiplier = request.Profile switch
            {
                RequestProfile.Routine => settings?.RoutinePriceLimitMultiplier ?? 1.5f,
                RequestProfile.Opportunistic => settings?.OpportunisticPriceLimitMultiplier ?? 1.1f,
                _ => null
            };

            if (maxMultiplier == null || item.Value <= 0)
            {
                return true;
            }

            return price <= item.Value * maxMultiplier.Value;
        }

        private static void ExecuteTradeProposal(TradeProposal proposal, TradeContext context, InventoryLogic logic)
        {
            var sells = proposal.Actions.Where(a => a.ActionType == TradeActionType.Sell).ToList();
            var slaughters = proposal.Actions.Where(a => a.ActionType == TradeActionType.Slaughter).ToList();
            var buys = proposal.Actions.Where(a => a.ActionType == TradeActionType.Buy).ToList();

            int availableGold = context.AvailableGold;
            float freeCargo = context.FreeCargoCapacity;
            int freeAnimalSlots = context.FreeAnimalSlots;
            bool enforceCargo = context.EnforceCargoLimit;

            // 1. Sells
            foreach (var action in sells)
            {
                if (action.EquipmentElement.Item == null) continue;
                var sellable = context.SellableItems.FirstOrDefault(s => s.EquipmentElement.Item == action.EquipmentElement.Item);
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
            if (party == MobileParty.MainParty && settlement != null && (settlement.IsTown || settlement.IsVillage))
            {
                SubModule.QueueBackgroundTrade(settlement);
            }
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
