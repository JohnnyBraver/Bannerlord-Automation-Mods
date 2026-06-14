using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    public enum RequestType
    {
        ItemCategory,    // E.g. Food, Horse, PackAnimal, Livestock
        SpecificItem,    // E.g. Hardwood, IronIngot
        MarketItem,      // Exact merchant inventory entry selected from AutomationRequestContext
        TroopFilter      // E.g. MeleeCavalry, Ranged, AnyRecruit
    }

    public class AutomationRequest
    {
        public string RequestorId { get; }
        public RequestType Type { get; }
        public string TargetId { get; }         // Category name, Item string ID, or troop filter name
        public int TargetQuantity { get; }      // Cumulative quantity we want to have in party/inventory
        public int Priority { get; }            // 1 to 100
        public int MinGoldReserve { get; }      // Threshold below which core skips this request
        public float MaxPriceMultiplier { get; } // Max price we pay relative to standard value (e.g. 1.5 = 150%)
        public InventoryItemView? TargetMarketItem { get; }

        public AutomationRequest(
            string requestorId,
            RequestType type,
            string targetId,
            int targetQuantity,
            int priority,
            int minGoldReserve = 1000,
            float maxPriceMultiplier = 1.5f,
            InventoryItemView? targetMarketItem = null)
        {
            RequestorId = requestorId;
            Type = type;
            TargetId = targetId;
            TargetQuantity = targetQuantity;
            Priority = Math.Max(1, Math.Min(100, priority));
            MinGoldReserve = minGoldReserve;
            MaxPriceMultiplier = maxPriceMultiplier;
            TargetMarketItem = targetMarketItem;
        }

        public static AutomationRequest ForMarketItem(
            string requestorId,
            InventoryItemView marketItem,
            int quantity,
            int priority,
            int minGoldReserve = 1000)
        {
            return new AutomationRequest(
                requestorId,
                RequestType.MarketItem,
                marketItem.SnapshotId,
                quantity,
                priority,
                minGoldReserve,
                float.MaxValue,
                marketItem);
        }

        public bool MatchesItem(ItemObject item)
        {
            if (item == null) return false;

            if (Type == RequestType.MarketItem && TargetMarketItem != null)
            {
                return TargetMarketItem.Item.StringId == item.StringId;
            }

            if (Type == RequestType.SpecificItem)
            {
                return item.StringId == TargetId;
            }

            if (Type == RequestType.ItemCategory)
            {
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
            }

            return false;
        }
    }

    public interface IAutomationRequestProvider
    {
        string ProviderName { get; }
        void SubmitAutomationRequests(AutomationRequestContext context);
    }
}
