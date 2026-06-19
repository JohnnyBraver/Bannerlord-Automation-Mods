using System;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SettlementAutomationCore.Transactions
{
    public sealed class TradePricingSession : IDisposable
    {
        private TradePricingSession(InventoryLogic logic, bool isSimulation)
        {
            Logic = logic;
            IsSimulation = isSimulation;
        }

        public InventoryLogic Logic { get; }
        public bool IsSimulation { get; }

        public static TradePricingSession? CreateSimulated(MobileParty party, Settlement settlement)
        {
            var logic = Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement, useClones: true);
            return logic == null ? null : new TradePricingSession(logic, true);
        }

        public static TradePricingSession? CreateLive(MobileParty party, Settlement settlement)
        {
            var logic = Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement, useClones: false);
            return logic == null ? null : new TradePricingSession(logic, false);
        }

        public int GetBuyPrice(EquipmentElement equipmentElement)
        {
            return Logic.GetItemPrice(equipmentElement, true);
        }

        public int GetSellPrice(EquipmentElement equipmentElement)
        {
            return Logic.GetItemPrice(equipmentElement, false);
        }

        public void Dispose()
        {
            // InventoryLogic has no disposable state; cloned sessions are isolated by cloned rosters.
        }
    }
}
