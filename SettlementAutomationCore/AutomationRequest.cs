using System;
using System.Collections.Generic;
using System.Linq;
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
        MarketItem       // Exact merchant inventory entry selected from AutomationRequestContext
    }

    public enum RequestProfile
    {
        Critical,
        Essential,
        Routine,
        Opportunistic,
        Luxury
    }

    public class RequestProfileOption
    {
        private readonly string _name;
        public RequestProfile Value { get; }

        public RequestProfileOption(string name, RequestProfile value)
        {
            _name = name;
            Value = value;
        }

        public override string ToString() => _name;
    }

    public static class RequestProfileOptions
    {
        public static readonly IReadOnlyList<RequestProfileOption> All = new List<RequestProfileOption>
        {
            new RequestProfileOption("Critical - buy before everything", RequestProfile.Critical),
            new RequestProfileOption("Essential - important party need", RequestProfile.Essential),
            new RequestProfileOption("Routine - normal maintenance", RequestProfile.Routine),
            new RequestProfileOption("Opportunistic - cheap only", RequestProfile.Opportunistic),
            new RequestProfileOption("Luxury - want, but do not need", RequestProfile.Luxury)
        };

        public static int IndexOf(RequestProfile profile)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Value == profile)
                {
                    return i;
                }
            }

            return 0;
        }
    }

    public enum RequestQuantityMode
    {
        DesiredInventoryCount,
        PurchaseCount
    }

    public enum BudgetPolicyKind
    {
        CoreReserve,
        ExplicitReserve
    }

    public class AutomationRequest
    {
        public string RequestorId { get; }
        public RequestType Type { get; }
        public string TargetId { get; }         // Category name, item string ID, or market request label
        public int Quantity { get; }
        public RequestQuantityMode QuantityMode { get; }
        public RequestProfile Profile { get; }
        public int Priority { get; }            // 1 to 9, default 5
        public BudgetPolicyKind BudgetPolicy { get; }
        public int ExplicitGoldReserve { get; }
        public IReadOnlyList<InventoryItemView> MarketCandidates { get; }

        private AutomationRequest(
            string requestorId,
            RequestType type,
            string targetId,
            int quantity,
            RequestQuantityMode quantityMode,
            RequestProfile profile,
            int priority,
            BudgetPolicyKind budgetPolicy,
            int explicitGoldReserve,
            IReadOnlyList<InventoryItemView>? marketCandidates = null)
        {
            RequestorId = requestorId;
            Type = type;
            TargetId = targetId;
            Quantity = Math.Max(0, quantity);
            QuantityMode = quantityMode;
            Profile = profile;
            Priority = Math.Max(1, Math.Min(9, priority));
            BudgetPolicy = budgetPolicy;
            ExplicitGoldReserve = Math.Max(0, explicitGoldReserve);
            MarketCandidates = marketCandidates ?? new List<InventoryItemView>();
        }

        public static AutomationRequest ForInventoryTarget(
            string requestorId,
            RequestType type,
            string targetId,
            int desiredCount,
            RequestProfile profile,
            int priority = 5,
            BudgetPolicyKind budgetPolicy = BudgetPolicyKind.CoreReserve,
            int explicitGoldReserve = 0)
        {
            if (type != RequestType.ItemCategory && type != RequestType.SpecificItem)
            {
                throw new ArgumentException("Inventory targets must use ItemCategory or SpecificItem.", nameof(type));
            }

            return new AutomationRequest(
                requestorId,
                type,
                targetId,
                desiredCount,
                RequestQuantityMode.DesiredInventoryCount,
                profile,
                priority,
                budgetPolicy,
                explicitGoldReserve);
        }

        public static AutomationRequest ForMarketItems(
            string requestorId,
            IEnumerable<InventoryItemView> candidates,
            int purchaseCount,
            RequestProfile profile = RequestProfile.Luxury,
            int priority = 5,
            int explicitGoldReserve = 0)
        {
            var orderedCandidates = candidates?.Where(c => c != null).ToList() ?? new List<InventoryItemView>();
            return new AutomationRequest(
                requestorId,
                RequestType.MarketItem,
                orderedCandidates.Count > 0 ? orderedCandidates[0].SnapshotId : "MarketItems",
                purchaseCount,
                RequestQuantityMode.PurchaseCount,
                profile,
                priority,
                explicitGoldReserve > 0 ? BudgetPolicyKind.ExplicitReserve : BudgetPolicyKind.CoreReserve,
                explicitGoldReserve,
                orderedCandidates);
        }

        public bool MatchesItem(ItemObject item)
        {
            if (item == null) return false;

            if (Type == RequestType.MarketItem)
            {
                return MarketCandidates.Any(c => c.Item.StringId == item.StringId);
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

        public bool MatchesEquipmentElement(EquipmentElement equipmentElement)
        {
            var item = equipmentElement.Item;
            if (item == null) return false;

            if (Type == RequestType.MarketItem)
            {
                return MarketCandidates.Any(candidate => candidate.MatchesEquipmentElement(equipmentElement));
            }

            return MatchesItem(item);
        }
    }

    public interface IAutomationRequestProvider
    {
        string ProviderName { get; }
        void SubmitAutomationRequests(AutomationRequestContext context);
    }
}
