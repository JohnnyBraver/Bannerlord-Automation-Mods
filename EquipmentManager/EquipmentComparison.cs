using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Inventory;

namespace EquipmentManager
{
    internal static class EquipmentComparison
    {
        public static bool StrictlyBeatsArmor(EquipmentElement candidate, EquipmentElement current, bool prioritizeStealth)
        {
            if (current.IsEmpty) return true;

            int headC = candidate.GetModifiedHeadArmor();
            int bodyC = candidate.GetModifiedBodyArmor();
            int legC = candidate.GetModifiedLegArmor();
            int armC = candidate.GetModifiedArmArmor();

            int headE = current.GetModifiedHeadArmor();
            int bodyE = current.GetModifiedBodyArmor();
            int legE = current.GetModifiedLegArmor();
            int armE = current.GetModifiedArmArmor();

            bool protectionEqualOrBetter = headC >= headE && bodyC >= bodyE && legC >= legE && armC >= armE;
            bool protectionStrictlyBetter = headC > headE || bodyC > bodyE || legC > legE || armC > armE;

            if (prioritizeStealth)
            {
                int stealthC = candidate.Item?.ArmorComponent != null ? candidate.Item.ArmorComponent.StealthFactor : 0;
                int stealthE = current.Item?.ArmorComponent != null ? current.Item.ArmorComponent.StealthFactor : 0;

                if (stealthC > stealthE) return true;
                if (stealthC < stealthE) return false;

                return protectionEqualOrBetter && protectionStrictlyBetter;
            }

            return protectionEqualOrBetter && protectionStrictlyBetter;
        }

        public static float GetArmorScore(EquipmentElement equipmentElement, bool prioritizeStealth)
        {
            if (equipmentElement.IsEmpty) return -9999f;

            float armorSum = equipmentElement.GetModifiedHeadArmor() +
                             equipmentElement.GetModifiedBodyArmor() +
                             equipmentElement.GetModifiedLegArmor() +
                             equipmentElement.GetModifiedArmArmor();
            if (!prioritizeStealth)
            {
                return armorSum;
            }

            int stealthFactor = equipmentElement.Item?.ArmorComponent != null
                ? equipmentElement.Item.ArmorComponent.StealthFactor
                : 0;
            return stealthFactor * 1000f + armorSum;
        }

        public static bool StrictlyBeatsWeapon(EquipmentElement candidate, EquipmentElement current)
        {
            if (current.IsEmpty || candidate.IsEmpty) return false;

            var wC = candidate.Item?.PrimaryWeapon;
            var wE = current.Item?.PrimaryWeapon;
            if (wC == null || wE == null) return false;

            if (wC.WeaponClass != wE.WeaponClass) return false;

            bool speedC = wC.ThrustSpeed >= wE.ThrustSpeed && wC.SwingSpeed >= wE.SwingSpeed && wC.MissileSpeed >= wE.MissileSpeed;
            bool damageC = wC.ThrustDamage >= wE.ThrustDamage && wC.SwingDamage >= wE.SwingDamage && wC.MissileDamage >= wE.MissileDamage;
            bool lengthC = wC.WeaponLength >= wE.WeaponLength;
            bool handlingC = wC.Handling >= wE.Handling;
            bool accuracyC = wC.Accuracy >= wE.Accuracy;
            bool durabilityC = wC.MaxDataValue >= wE.MaxDataValue;

            bool statsEqualOrBetter = speedC && damageC && lengthC && handlingC && accuracyC && durabilityC;

            bool speedS = wC.ThrustSpeed > wE.ThrustSpeed || wC.SwingSpeed > wE.SwingSpeed || wC.MissileSpeed > wE.MissileSpeed;
            bool damageS = wC.ThrustDamage > wE.ThrustDamage || wC.SwingDamage > wE.SwingDamage || wC.MissileDamage > wE.MissileDamage;
            bool lengthS = wC.WeaponLength > wE.WeaponLength;
            bool handlingS = wC.Handling > wE.Handling;
            bool accuracyS = wC.Accuracy > wE.Accuracy;
            bool durabilityS = wC.MaxDataValue > wE.MaxDataValue;

            bool statsStrictlyBetter = speedS || damageS || lengthS || handlingS || accuracyS || durabilityS;

            if (!statsEqualOrBetter || !statsStrictlyBetter) return false;

            var flagsC = wC.WeaponFlags;
            var flagsE = wE.WeaponFlags;
            return (flagsC & flagsE) == flagsE;
        }

        public static float GetWeaponScore(EquipmentElement equipmentElement)
        {
            if (equipmentElement.IsEmpty) return -9999f;
            var weapon = equipmentElement.Item?.PrimaryWeapon;
            if (weapon == null) return -9999f;

            if (weapon.IsMeleeWeapon)
            {
                return weapon.ThrustDamage * weapon.ThrustSpeed * 0.01f +
                       weapon.SwingDamage * weapon.SwingSpeed * 0.01f +
                       weapon.Handling * 10f +
                       weapon.WeaponLength;
            }
            if (weapon.IsRangedWeapon)
            {
                return weapon.MissileDamage * weapon.MissileSpeed * 0.01f + weapon.Accuracy * 10f;
            }
            if (weapon.IsShield || weapon.IsAmmo)
            {
                return weapon.MaxDataValue;
            }

            return 0f;
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
    }
}
