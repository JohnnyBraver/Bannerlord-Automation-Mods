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
                // Step 0: Collect Logistics Goals from active providers
                // ----------------------------------------------------
                AutomationRegistry.ClearLogisticsGoals();
                var goalProviders = AutomationRegistry.ActiveGoalProviders;
                foreach (var reg in goalProviders)
                {
                    try
                    {
                        reg.Provider.SubmitLogisticsGoals(MobileParty.MainParty, settlement);
                    }
                    catch {}
                }

                // ----------------------------------------------------
                // Step 1: Pre-Sell Phase (Revenue Generation)
                // ----------------------------------------------------
                var tradeRegistrations = AutomationRegistry.ActiveTradeProviders;
                var preSellOrders = new List<TradeOrder>();
                foreach (var reg in tradeRegistrations)
                {
                    try
                    {
                        var orders = reg.Provider.GetPreSellOrders(MobileParty.MainParty, settlement);
                        if (orders != null) preSellOrders.AddRange(orders);
                    }
                    catch {}
                }

                if (preSellOrders.Count > 0)
                {
                    var logic1 = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
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
                            // OnTroopRecruited handles Leadership XP, FamousCommander troop XP, and bandit Roguery XP.
                            // Do NOT also fire OnUnitRecruited — it duplicates SkillLevelingManager.OnTroopRecruited,
                            // which would award Leadership XP twice per recruit.
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
                // Step 6: Main Trade Phase (Reconciliation & Purchases)
                // ----------------------------------------------------
                var logic2 = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(MobileParty.MainParty, settlement);
                if (logic2 != null)
                {
                    var mainOrders = new List<TradeOrder>();
                    foreach (var reg in tradeRegistrations)
                    {
                        try
                        {
                            var orders = reg.Provider.GetMainOrders(MobileParty.MainParty, settlement, logic2);
                            if (orders != null) mainOrders.AddRange(orders);
                        }
                        catch {}
                    }

                    if (mainOrders.Count > 0)
                    {
                        var slaughters = mainOrders.Where(o => o.IsSlaughter).ToList();
                        var normalOrders = mainOrders.Where(o => !o.IsSlaughter).ToList();

                        bool executedAny = false;
                        var soldList = new List<string>();
                        var boughtList = new List<string>();
                        var slaughteredList = new List<string>();

                        // Execute Normal Trades first (so arbitrage buys are in player inventory before slaughtering)
                        if (normalOrders.Count > 0)
                        {
                            var itemOrderNetMap = new Dictionary<ItemObject, int>();
                            var eqElementMap = new Dictionary<ItemObject, EquipmentElement>();

                            foreach (var order in normalOrders)
                            {
                                if (order.EquipmentElement.Item == null) continue;
                                var item = order.EquipmentElement.Item;
                                eqElementMap[item] = order.EquipmentElement;

                                int netChange = order.IsBuy ? order.Amount : -order.Amount;
                                if (itemOrderNetMap.ContainsKey(item))
                                {
                                    itemOrderNetMap[item] += netChange;
                                }
                                else
                                {
                                    itemOrderNetMap[item] = netChange;
                                }
                            }

                            foreach (var pair in itemOrderNetMap)
                            {
                                var item = pair.Key;
                                int netAmount = pair.Value;
                                if (netAmount == 0) continue;

                                bool isBuy = netAmount > 0;
                                int absAmount = Math.Abs(netAmount);

                                var sideFrom = isBuy ? InventoryLogic.InventorySide.OtherInventory : InventoryLogic.InventorySide.PlayerInventory;
                                var sideTo = isBuy ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory;

                                var command = TransferCommand.Transfer(
                                    absAmount,
                                    sideFrom,
                                    sideTo,
                                    new ItemRosterElement(eqElementMap[item], absAmount),
                                    EquipmentIndex.None,
                                    EquipmentIndex.None,
                                    Hero.MainHero.CharacterObject
                                );
                                logic2.AddTransferCommand(command);
                                executedAny = true;

                                if (isBuy)
                                {
                                    boughtList.Add($"{absAmount}x {item.Name}");
                                }
                                else
                                {
                                    soldList.Add($"{absAmount}x {item.Name}");
                                }
                            }
                        }

                        // Execute Slaughters
                        foreach (var order in slaughters)
                        {
                            if (order.EquipmentElement.Item != null && order.Amount > 0)
                            {
                                var itemRosterEl = new ItemRosterElement(order.EquipmentElement, order.Amount);
                                if (logic2.CanSlaughterItem(itemRosterEl, InventoryLogic.InventorySide.PlayerInventory))
                                {
                                    logic2.SlaughterItem(itemRosterEl);
                                    executedAny = true;
                                    slaughteredList.Add($"{order.Amount}x {order.EquipmentElement.Item.Name}");
                                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] Slaughtered {order.Amount}x {order.EquipmentElement.Item.Name}"));
                                }
                            }
                        }
                        if (executedAny && logic2.IsThereAnyChanges())
                        {
                            int initialGold = Hero.MainHero?.Gold ?? 0;
                            logic2.DoneLogic();
                            int finalGold = Hero.MainHero?.Gold ?? 0;
                            int goldDiff = finalGold - initialGold;

                            var logParts = new List<string>();
                            if (soldList.Count > 0) logParts.Add($"Sold: {string.Join(", ", soldList)}");
                            if (boughtList.Count > 0) logParts.Add($"Bought: {string.Join(", ", boughtList)}");
                            if (slaughteredList.Count > 0) logParts.Add($"Slaughtered: {string.Join(", ", slaughteredList)}");

                            string goldDiffSign = goldDiff >= 0 ? "+" : "";
                            string logMsg = $"Auto-traded in {settlement.Name} (Gold change: {goldDiffSign}{goldDiff}d). {string.Join(" | ", logParts)}";
                            Helpers.Logger.WriteLog("SettlementAutomationCore", logMsg);
                        }
                    }
                }

                // ----------------------------------------------------
                // Step 7: Fief Management Phase
                // ----------------------------------------------------
                var fiefRegistrations = AutomationRegistry.ActiveFiefProviders;
                foreach (var reg in fiefRegistrations)
                {
                    try
                    {
                        reg.Provider.ProcessFiefAutomation(MobileParty.MainParty, settlement);
                    }
                    catch {}
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
