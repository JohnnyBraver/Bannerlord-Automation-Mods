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
        private int _gold;

        public AutomationInventoryListener(Settlement settlement)
        {
            _settlement = settlement;
            _gold = settlement.Town?.Gold ?? settlement.Village?.Bound?.Town?.Gold ?? 50000;
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
            _gold = gold;
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

                // Set private InventoryListener property via reflection to avoid NullReferenceException in DoneLogic
                var listener = new AutomationInventoryListener(settlement);
                var listenerProp = typeof(InventoryLogic).GetProperty("InventoryListener", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                listenerProp?.SetValue(logic, listener);

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
