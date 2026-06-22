using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;

namespace EquipmentManager
{
    internal sealed class EquipmentProtectionItem
    {
        public EquipmentElement EquipmentElement { get; }
        public int Quantity { get; }
        public float SellPrice { get; }

        public EquipmentProtectionItem(EquipmentElement equipmentElement, int quantity, float sellPrice)
        {
            EquipmentElement = equipmentElement;
            Quantity = quantity;
            SellPrice = sellPrice;
        }
    }

    internal sealed class EquipmentProtectionPlan
    {
        private readonly Dictionary<string, int> _protectedQuantities = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _totalQuantities = new Dictionary<string, int>();

        public EquipmentProtectionPlan(IEnumerable<EquipmentProtectionItem> items)
        {
            foreach (var item in items)
            {
                string key = EquipmentSaleProtector.GetEquipmentKey(item.EquipmentElement);
                if (string.IsNullOrEmpty(key)) continue;

                _totalQuantities[key] = _totalQuantities.TryGetValue(key, out int current)
                    ? current + item.Quantity
                    : item.Quantity;
            }
        }

        public void Protect(EquipmentElement equipmentElement, int quantity)
        {
            if (quantity <= 0) return;

            string key = EquipmentSaleProtector.GetEquipmentKey(equipmentElement);
            if (string.IsNullOrEmpty(key)) return;

            int total = _totalQuantities.TryGetValue(key, out int totalQuantity) ? totalQuantity : quantity;
            int current = _protectedQuantities.TryGetValue(key, out int currentQuantity) ? currentQuantity : 0;
            _protectedQuantities[key] = Math.Min(total, current + quantity);
        }

        public int GetSellableQuantity(EquipmentElement equipmentElement, int quantity)
        {
            string key = EquipmentSaleProtector.GetEquipmentKey(equipmentElement);
            if (string.IsNullOrEmpty(key)) return quantity;

            int protectedQuantity = _protectedQuantities.TryGetValue(key, out int current) ? current : 0;
            return Math.Max(0, quantity - protectedQuantity);
        }
    }

    internal static class EquipmentSaleProtector
    {
        private static readonly EquipmentIndex[] ArmorSlots =
        {
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Leg,
            EquipmentIndex.Gloves,
            EquipmentIndex.Cape
        };

        public static EquipmentProtectionPlan BuildProtectionPlan(
            IEnumerable<EquipmentProtectionItem> inventoryItems,
            List<Hero> targets,
            Settings settings,
            bool hasWeaponDonationPerk,
            bool hasArmorDonationPerk)
        {
            var items = inventoryItems
                .Where(item => item.Quantity > 0 && item.EquipmentElement.Item != null)
                .ToList();
            var plan = new EquipmentProtectionPlan(items);

            foreach (var inventoryItem in items)
            {
                var equipmentElement = inventoryItem.EquipmentElement;
                var item = equipmentElement.Item;
                if (item == null || !IsEquipment(item)) continue;

                if (ShouldProtectWholeStack(inventoryItem, targets, settings, hasWeaponDonationPerk, hasArmorDonationPerk))
                {
                    plan.Protect(equipmentElement, inventoryItem.Quantity);
                }
            }

            AddSpareArmorSetProtection(plan, items, settings);
            return plan;
        }

        public static string GetEquipmentKey(EquipmentElement equipmentElement)
        {
            var item = equipmentElement.Item;
            if (item == null) return string.Empty;

            string modifierId = equipmentElement.ItemModifier != null ? equipmentElement.ItemModifier.StringId : string.Empty;
            return item.StringId + "::" + modifierId;
        }

        private static bool ShouldProtectWholeStack(
            EquipmentProtectionItem inventoryItem,
            List<Hero> targets,
            Settings settings,
            bool hasWeaponDonationPerk,
            bool hasArmorDonationPerk)
        {
            var equipmentElement = inventoryItem.EquipmentElement;
            var item = equipmentElement.Item;
            if (item == null) return false;

            if (item.StringId == "stealth_throwing_stone")
            {
                return true;
            }

            var modifier = equipmentElement.ItemModifier;
            if (settings.KeepPositiveModifiers && modifier != null && modifier.PriceMultiplier > 1.0f)
            {
                return true;
            }

            if (ShouldKeepForDonation(inventoryItem, settings, hasWeaponDonationPerk, hasArmorDonationPerk))
            {
                return true;
            }

            if (EquipmentManagerProvider.IsUpgradeForAnyTarget(equipmentElement, targets, settings))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldKeepForDonation(
            EquipmentProtectionItem inventoryItem,
            Settings settings,
            bool hasWeaponDonationPerk,
            bool hasArmorDonationPerk)
        {
            var item = inventoryItem.EquipmentElement.Item;
            if (item == null || settings.KeepDonationCategorySetting == KeepDonationCategory.None) return false;

            float baseValue = item.Value;
            float costPerXp = baseValue > 0 ? inventoryItem.SellPrice / baseValue : 9999f;
            if (costPerXp > settings.MaxCostPerXp) return false;

            if (item.HasArmorComponent && hasArmorDonationPerk)
            {
                return settings.KeepDonationCategorySetting == KeepDonationCategory.ArmorOnly ||
                       settings.KeepDonationCategorySetting == KeepDonationCategory.WeaponsAndArmor;
            }

            bool isWeapon = item.WeaponComponent != null || item.PrimaryWeapon != null;
            if (isWeapon && hasWeaponDonationPerk)
            {
                return settings.KeepDonationCategorySetting == KeepDonationCategory.WeaponsOnly ||
                       settings.KeepDonationCategorySetting == KeepDonationCategory.WeaponsAndArmor;
            }

            return false;
        }

        private static void AddSpareArmorSetProtection(
            EquipmentProtectionPlan plan,
            List<EquipmentProtectionItem> items,
            Settings settings)
        {
            int setsToKeep = settings.AdditionalArmorSetsToKeep;
            if (setsToKeep <= 0) return;

            if (settings.KeepSpareCombatArmorSets)
            {
                AddSpareArmorSetProtectionForLoadout(plan, items, setsToKeep, InventoryLogic.InventorySide.BattleEquipment);
            }
            if (settings.KeepSpareCivilianArmorSets)
            {
                AddSpareArmorSetProtectionForLoadout(plan, items, setsToKeep, InventoryLogic.InventorySide.CivilianEquipment);
            }
            if (settings.KeepSpareSneakingArmorSets)
            {
                AddSpareArmorSetProtectionForLoadout(plan, items, setsToKeep, InventoryLogic.InventorySide.StealthEquipment);
            }
        }

        private static void AddSpareArmorSetProtectionForLoadout(
            EquipmentProtectionPlan plan,
            List<EquipmentProtectionItem> items,
            int setsToKeep,
            InventoryLogic.InventorySide side)
        {
            foreach (var slot in ArmorSlots)
            {
                int remaining = setsToKeep;
                var candidates = items
                    .Where(item => CanServeArmorSlot(item.EquipmentElement, side, slot))
                    .OrderByDescending(item => GetArmorScore(item.EquipmentElement, side == InventoryLogic.InventorySide.StealthEquipment))
                    .ThenByDescending(item => item.EquipmentElement.Item != null ? (int)item.EquipmentElement.Item.Tier : 0)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    if (remaining <= 0) break;

                    int toProtect = Math.Min(candidate.Quantity, remaining);
                    plan.Protect(candidate.EquipmentElement, toProtect);
                    remaining -= toProtect;
                }
            }
        }

        private static bool CanServeArmorSlot(EquipmentElement equipmentElement, InventoryLogic.InventorySide side, EquipmentIndex slot)
        {
            var item = equipmentElement.Item;
            if (item == null || !item.HasArmorComponent) return false;
            if (!Equipment.IsItemFitsToSlot(slot, item)) return false;
            if (side == InventoryLogic.InventorySide.CivilianEquipment && !item.IsCivilian) return false;
            if (side == InventoryLogic.InventorySide.StealthEquipment && !item.IsStealthItem) return false;
            return true;
        }

        private static bool IsEquipment(ItemObject item)
        {
            return item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null || item.StringId == "stealth_throwing_stone";
        }

        private static float GetArmorScore(EquipmentElement equipmentElement, bool prioritizeStealth)
        {
            if (equipmentElement.IsEmpty) return -9999f;

            float armorScore = equipmentElement.GetModifiedHeadArmor() +
                               equipmentElement.GetModifiedBodyArmor() +
                               equipmentElement.GetModifiedLegArmor() +
                               equipmentElement.GetModifiedArmArmor();
            if (!prioritizeStealth) return armorScore;

            int stealthFactor = equipmentElement.Item?.ArmorComponent != null
                ? equipmentElement.Item.ArmorComponent.StealthFactor
                : 0;
            return stealthFactor * 1000f + armorScore;
        }
    }
}
