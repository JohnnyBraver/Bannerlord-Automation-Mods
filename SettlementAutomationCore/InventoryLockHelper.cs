using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    public static class InventoryLockHelper
    {
        public static IReadOnlyCollection<string> GetCurrentLockKeys()
        {
            var tracker = Campaign.Current?.GetCampaignBehavior<IViewDataTracker>();
            return new HashSet<string>(tracker?.GetInventoryLocks() ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        public static bool IsLocked(EquipmentElement equipmentElement, IReadOnlyCollection<string> lockKeys)
        {
            string itemId = equipmentElement.Item?.StringId ?? "";
            string modifierId = equipmentElement.ItemModifier?.StringId ?? "";
            return IsLocked(itemId, modifierId, lockKeys);
        }

        public static bool IsLocked(string itemId, string modifierId, IReadOnlyCollection<string> lockKeys)
        {
            if (lockKeys == null || lockKeys.Count == 0) return false;

            foreach (var key in GetPossibleLockKeys(itemId, modifierId))
            {
                if (lockKeys.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<string> GetPossibleLockKeys(EquipmentElement equipmentElement)
        {
            string itemId = equipmentElement.Item?.StringId ?? "";
            if (string.IsNullOrEmpty(itemId))
            {
                return Array.Empty<string>();
            }

            string modifierId = equipmentElement.ItemModifier?.StringId ?? "";
            return GetPossibleLockKeys(itemId, modifierId);
        }

        public static IReadOnlyList<string> GetPossibleLockKeys(string itemId, string modifierId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return Array.Empty<string>();
            }

            if (string.IsNullOrEmpty(modifierId))
            {
                return new[] { itemId };
            }

            return new[]
            {
                itemId + modifierId,
                $"{itemId}::{modifierId}",
                $"{itemId}:{modifierId}"
            };
        }
    }
}
