using System.Collections.Generic;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    public enum TradeActionType { Buy, Sell, Slaughter }

    public class TradeAction
    {
        public EquipmentElement EquipmentElement { get; }
        public int Quantity { get; }
        public TradeActionType ActionType { get; }  // Buy, Sell, Slaughter

        public TradeAction(EquipmentElement equipmentElement, int quantity, TradeActionType actionType)
        {
            EquipmentElement = equipmentElement;
            Quantity = quantity;
            ActionType = actionType;
        }
    }

    public class TradeProposal
    {
        public List<TradeAction> Actions { get; }

        public TradeProposal(List<TradeAction> actions)
        {
            Actions = actions ?? new List<TradeAction>();
        }
    }
}
