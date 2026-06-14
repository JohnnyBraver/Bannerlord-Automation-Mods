using System;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    public class ItemReservation
    {
        public string RequestorId { get; }    // "PartyManager", "EquipmentManager"
        public string TargetId { get; }       // Category name or Item StringId
        public bool IsCategory { get; }       // true = category match, false = specific item
        public int Quantity { get; }          // How many of this type to protect

        public ItemReservation(string requestorId, string targetId, bool isCategory, int quantity)
        {
            RequestorId = requestorId;
            TargetId = targetId;
            IsCategory = isCategory;
            Quantity = quantity;
        }

        public bool MatchesItem(ItemObject item)
        {
            if (item == null) return false;

            if (!IsCategory)
            {
                return item.StringId == TargetId;
            }

            if (string.Equals(TargetId, "Food", StringComparison.OrdinalIgnoreCase))
            {
                return item.IsFood;
            }
            if (string.Equals(TargetId, "Horse", StringComparison.OrdinalIgnoreCase) || string.Equals(TargetId, "Mount", StringComparison.OrdinalIgnoreCase))
            {
                return item.IsMountable && item.HorseComponent != null && !item.HorseComponent.IsPackAnimal;
            }
            if (string.Equals(TargetId, "PackAnimal", StringComparison.OrdinalIgnoreCase))
            {
                return item.IsMountable && item.HorseComponent != null && item.HorseComponent.IsPackAnimal;
            }
            if (string.Equals(TargetId, "Livestock", StringComparison.OrdinalIgnoreCase))
            {
                return item.IsAnimal && !item.IsMountable;
            }

            return false;
        }
    }
}
