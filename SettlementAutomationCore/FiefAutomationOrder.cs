using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace SettlementAutomationCore
{
    public enum FiefAutomationOrderType
    {
        QueueBuilding,
        DepositBoostGold
    }

    public sealed class FiefAutomationOrder
    {
        private FiefAutomationOrder(
            FiefAutomationOrderType orderType,
            IReadOnlyList<Building> buildings,
            int amount,
            string description)
        {
            OrderType = orderType;
            Buildings = buildings;
            Amount = amount;
            Description = description;
        }

        public FiefAutomationOrderType OrderType { get; }
        public IReadOnlyList<Building> Buildings { get; }
        public Building? Building => Buildings.Count > 0 ? Buildings[0] : null;
        public int Amount { get; }
        public string Description { get; }

        public static FiefAutomationOrder QueueBuilding(Building building, string description)
        {
            return QueueBuildings(new[] { building }, description);
        }

        public static FiefAutomationOrder QueueBuildings(IEnumerable<Building> buildings, string description)
        {
            return new FiefAutomationOrder(FiefAutomationOrderType.QueueBuilding, buildings.ToList(), 0, description);
        }

        public static FiefAutomationOrder DepositBoostGold(int amount, string description)
        {
            return new FiefAutomationOrder(FiefAutomationOrderType.DepositBoostGold, new List<Building>(), amount, description);
        }
    }
}
