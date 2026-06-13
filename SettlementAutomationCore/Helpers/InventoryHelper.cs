using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace SettlementAutomationCore.Helpers
{
    public class AutomationInventoryListener : InventoryListener
    {
        private readonly Settlement _settlement;
        private int _initialGold;
        private int _gold;

        public AutomationInventoryListener(Settlement settlement)
        {
            _settlement = settlement;
            _initialGold = settlement.Town?.Gold ?? settlement.Village?.Bound?.Town?.Gold ?? 50000;
            _gold = _initialGold;
        }

        public override int GetGold()
        {
            return _gold;
        }

        public override TextObject GetTraderName()
        {
            return _settlement.Name;
        }

        public override void SetGold(int gold)
        {
            // Apply the delta to the real settlement so town gold actually changes in-game
            int delta = gold - _gold;
            _gold = gold;
            var component = _settlement.Town ?? _settlement.Village?.Bound?.Town;
            if (component != null && delta != 0)
            {
                component.ChangeGold(delta);
            }
        }

        public override PartyBase GetOppositeParty()
        {
            return _settlement.Party;
        }

        public override void OnTransaction()
        {
        }
    }

    public static class InventoryHelper
    {
        public static IMarketData? GetMarketData(Settlement settlement)
        {
            if (settlement.IsTown) return settlement.Town?.MarketData;
            if (settlement.IsVillage) return settlement.Village?.Bound?.Town?.MarketData;
            return null;
        }

        public static InventoryLogic? CreateAndInitInventoryLogic(MobileParty party, Settlement settlement)
        {
            if (settlement == null || party == null || Hero.MainHero == null) return null;
            try
            {
                var logic = new InventoryLogic(party, Hero.MainHero.CharacterObject, settlement.Party);

                var initMethod = typeof(InventoryLogic).GetMethods()
                    .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 13);

                if (initMethod == null)
                {
                    Logger.WriteLog("SettlementAutomationCore", "ERROR: Could not find InventoryLogic.Initialize with 13 parameters. Signatures might have changed!");
                    return null;
                }

                var categoryTypeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryCategoryType");
                var modeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryMode");
                if (categoryTypeEnum == null || modeEnum == null)
                {
                    Logger.WriteLog("SettlementAutomationCore", "ERROR: Could not find InventoryCategoryType or InventoryMode enum type via reflection.");
                    return null;
                }

                var categoryTypeAll = Enum.Parse(categoryTypeEnum, "All");
                var modeTrade = Enum.Parse(modeEnum, "Trade");

                initMethod.Invoke(logic, new object[] {
                    settlement.ItemRoster,
                    party.ItemRoster,
                    party.MemberRoster,
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

                // IMPORTANT: Set our custom listener AFTER Initialize() to prevent it from being
                // overwritten by Initialize()'s internal 'InventoryListener = new FakeInventoryListener()' call.
                // This ensures gold and XP are properly tracked and applied on DoneLogic().
                var listener = new AutomationInventoryListener(settlement);
                var listenerProp = typeof(InventoryLogic).GetProperty("InventoryListener", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                listenerProp?.SetValue(logic, listener);

                return logic;
            }
            catch (Exception ex)
            {
                Logger.WriteLog("SettlementAutomationCore", $"ERROR in CreateAndInitInventoryLogic: {ex}");
                return null;
            }
        }
    }
}
