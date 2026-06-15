using System;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Inventory;

namespace EquipmentManager
{
    internal static class EquipmentComparison
    {
        public static bool StrictlyBeatsArmor(EquipmentElement candidate, EquipmentElement current, bool prioritizeStealth)
        {
            if (current.IsEmpty) return true;

            return EquipmentDecisionMath.StrictlyBeatsArmor(
                ToArmorStats(candidate),
                ToArmorStats(current),
                prioritizeStealth);
        }

        public static float GetArmorScore(EquipmentElement equipmentElement, bool prioritizeStealth)
        {
            if (equipmentElement.IsEmpty) return -9999f;

            return EquipmentDecisionMath.GetArmorScore(ToArmorStats(equipmentElement), prioritizeStealth);
        }

        public static bool StrictlyBeatsWeapon(EquipmentElement candidate, EquipmentElement current)
        {
            if (current.IsEmpty || candidate.IsEmpty) return false;

            var wC = candidate.Item?.PrimaryWeapon;
            var wE = current.Item?.PrimaryWeapon;
            if (wC == null || wE == null) return false;

            return EquipmentDecisionMath.StrictlyBeatsWeapon(
                ToWeaponStats(candidate),
                ToWeaponStats(current));
        }

        public static float GetWeaponScore(EquipmentElement equipmentElement)
        {
            return EquipmentDecisionMath.GetWeaponScore(ToWeaponStats(equipmentElement));
        }

        public static bool ShouldEvaluateWeaponSlot(EquipmentElement currentWeapon, InventoryLogic.InventorySide side)
        {
            bool isStealthLoadout = side == InventoryLogic.InventorySide.StealthEquipment;
            return ShouldEvaluateWeaponSlot(
                currentWeapon.IsEmpty,
                currentWeapon.Item?.PrimaryWeapon != null,
                isStealthLoadout,
                currentWeapon.Item?.ItemType ?? ItemObject.ItemTypeEnum.Invalid,
                currentWeapon.Item?.StringId ?? string.Empty);
        }

        public static bool ShouldEvaluateWeaponSlot(
            bool currentIsEmpty,
            bool currentHasPrimaryWeapon,
            bool isStealthLoadout,
            ItemObject.ItemTypeEnum currentItemType,
            string currentItemId)
        {
            if (currentIsEmpty || !currentHasPrimaryWeapon)
            {
                return false;
            }

            return !(isStealthLoadout &&
                     currentItemType == ItemObject.ItemTypeEnum.Thrown &&
                     currentItemId == "stealth_throwing_stone");
        }

        private static ArmorStats ToArmorStats(EquipmentElement equipmentElement)
        {
            if (equipmentElement.IsEmpty)
            {
                return new ArmorStats(0, 0, 0, 0, 0);
            }

            int stealthFactor = equipmentElement.Item?.ArmorComponent != null
                ? equipmentElement.Item.ArmorComponent.StealthFactor
                : 0;

            return new ArmorStats(
                equipmentElement.GetModifiedHeadArmor(),
                equipmentElement.GetModifiedBodyArmor(),
                equipmentElement.GetModifiedLegArmor(),
                equipmentElement.GetModifiedArmArmor(),
                stealthFactor);
        }

        private static WeaponStats ToWeaponStats(EquipmentElement equipmentElement)
        {
            if (equipmentElement.IsEmpty)
            {
                return default;
            }

            var weapon = equipmentElement.Item?.PrimaryWeapon;
            if (weapon == null)
            {
                return default;
            }

            return new WeaponStats(
                true,
                (int)weapon.WeaponClass,
                weapon.ThrustSpeed,
                weapon.SwingSpeed,
                weapon.MissileSpeed,
                weapon.ThrustDamage,
                weapon.SwingDamage,
                weapon.MissileDamage,
                weapon.WeaponLength,
                weapon.Handling,
                weapon.Accuracy,
                weapon.MaxDataValue,
                Convert.ToInt64(weapon.WeaponFlags),
                weapon.IsMeleeWeapon,
                weapon.IsRangedWeapon,
                weapon.IsShield || weapon.IsAmmo);
        }
    }
}
