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
                        foreach (var order in preSellOrders)
                        {
                            if (!order.IsBuy) // Pre-sell only supports selling!
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
                            }
                        }
                        if (executedAny && logic1.IsThereAnyChanges())
                        {
                            logic1.DoneLogic();
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
                                InformationManager.DisplayMessage(new InformationMessage($"[Automation] Ransomed {order.Amount}x {order.Prisoner.Name}"));
                            }
                        }
                        if (ransomRoster.Count > 0)
                        {
                            SellPrisonersAction.ApplyForSelectedPrisoners(MobileParty.MainParty.Party, settlement.Party, ransomRoster);
                        }
                    }
                    catch {}
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

                        int cost = (int)Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, Hero.MainHero, false).ResultNumber;
                        if (Hero.MainHero.Gold >= cost)
                        {
                            order.Notable.VolunteerTypes[order.SlotIndex] = null;
                            MobileParty.MainParty.MemberRoster.AddToCounts(troop, 1);
                            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, order.Notable, cost, false);
                            CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
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
                                    InformationManager.DisplayMessage(new InformationMessage($"[Automation] Slaughtered {order.Amount}x {order.EquipmentElement.Item.Name}"));
                                }
                            }
                        }
                        if (executedAny && logic2.IsThereAnyChanges())
                        {
                            logic2.DoneLogic();
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
            catch {}
        }
    }

    public class SettlementAutomationCampaignBehavior : CampaignBehaviorBase
    {
        private string _lastVisitedSettlementId = "";

        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (party == MobileParty.MainParty && settlement != null && (settlement.IsTown || settlement.IsVillage))
            {
                if (settlement.StringId != _lastVisitedSettlementId)
                {
                    _lastVisitedSettlementId = settlement.StringId;
                    SubModule.QueueBackgroundTrade(settlement);
                }
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if (party == MobileParty.MainParty && settlement != null)
            {
                _lastVisitedSettlementId = "";
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_lastVisitedSettlementId", ref _lastVisitedSettlementId);
            }
            catch {}
        }
    }
}
