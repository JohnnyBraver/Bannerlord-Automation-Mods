using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SettlementAutomationCore
{
    public class SubModule : MBSubModuleBase
    {
        private static Settlement? _pendingBackgroundTradeSettlement = null;

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
            _pendingBackgroundTradeSettlement = settlement;
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            if (_pendingBackgroundTradeSettlement != null)
            {
                var sett = _pendingBackgroundTradeSettlement;
                _pendingBackgroundTradeSettlement = null;
                ExecuteBackgroundAutomation(sett);
            }
        }

        private static IMarketData? GetMarketData(Settlement settlement)
        {
            if (settlement.IsTown) return settlement.Town?.MarketData;
            if (settlement.IsVillage) return settlement.Village?.Bound?.Town?.MarketData;
            return null;
        }

        private static InventoryLogic? CreateAndInitInventoryLogic(Settlement settlement)
        {
            if (settlement == null || MobileParty.MainParty == null || Hero.MainHero == null) return null;
            try
            {
                var logic = new InventoryLogic(MobileParty.MainParty, Hero.MainHero.CharacterObject, settlement.Party);

                var initMethod = typeof(InventoryLogic).GetMethods()
                    .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 13);

                if (initMethod == null) return null;

                var categoryTypeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryCategoryType");
                var modeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryMode");
                if (categoryTypeEnum == null || modeEnum == null) return null;

                var categoryTypeAll = Enum.Parse(categoryTypeEnum, "All");
                var modeTrade = Enum.Parse(modeEnum, "Trade");

                initMethod.Invoke(logic, new object[] {
                    settlement.ItemRoster,
                    MobileParty.MainParty.ItemRoster,
                    MobileParty.MainParty.MemberRoster,
                    true, // isTrading
                    false, // isSpecialActionsPermitted
                    Hero.MainHero.CharacterObject,
                    categoryTypeAll,
                    GetMarketData(settlement)!,
                    false, // useBasePrices
                    modeTrade,
                    settlement.Name,
                    null!, // leftMemberRoster
                    null! // otherSideCapacityData
                });

                return logic;
            }
            catch
            {
                return null;
            }
        }

        private static void ExecuteBackgroundAutomation(Settlement settlement)
        {
            if (settlement == null || MobileParty.MainParty == null || Hero.MainHero == null) return;

            try
            {
                var registrations = AutomationRegistry.ActiveTradeProviders;
                if (registrations.Count == 0) return;

                // Step 1: Pre-Sell Phase (Revenue Generation)
                var preSellOrders = new List<TradeOrder>();
                foreach (var reg in registrations)
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
                    var logic1 = CreateAndInitInventoryLogic(settlement);
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

                // Step 2: Main Phase (Reconciliation & Purchases)
                var logic2 = CreateAndInitInventoryLogic(settlement);
                if (logic2 != null)
                {
                    var mainOrders = new List<TradeOrder>();
                    foreach (var reg in registrations)
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
                        var itemOrderNetMap = new Dictionary<ItemObject, int>();
                        var eqElementMap = new Dictionary<ItemObject, EquipmentElement>();

                        foreach (var order in mainOrders)
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

                        bool executedAny = false;
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

                        if (executedAny && logic2.IsThereAnyChanges())
                        {
                            logic2.DoneLogic();
                        }
                    }
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
