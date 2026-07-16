using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Inventory;

namespace EquipmentManager
{
    internal static class EquipmentComparison
    {
        private static readonly long HardRestrictionFlags =
            Convert.ToInt64(WeaponFlags.NotUsableWithOneHand) |
            Convert.ToInt64(WeaponFlags.CantReloadOnHorseback);

        private static readonly long MinorPropertyFlags =
            Convert.ToInt64(WeaponFlags.WideGrip) |
            Convert.ToInt64(WeaponFlags.CanHook) |
            Convert.ToInt64(WeaponFlags.CanKnockDown);

        private static readonly long CantReloadOnHorsebackFlag = Convert.ToInt64(WeaponFlags.CantReloadOnHorseback);

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

        public static bool StrictlyBeatsBanner(EquipmentElement candidate, EquipmentElement current)
        {
            if (candidate.IsEmpty || candidate.Item == null) return false;
            var candidateComponent = candidate.Item.ItemComponent as BannerComponent;
            if (candidateComponent == null || candidateComponent.BannerEffect == null) return false;

            if (current.IsEmpty || current.Item == null)
            {
                return true;
            }

            var currentComponent = current.Item.ItemComponent as BannerComponent;
            if (currentComponent == null)
            {
                return true;
            }

            if (currentComponent.BannerEffect == null)
            {
                return false;
            }

            if (candidateComponent.BannerEffect.StringId == currentComponent.BannerEffect.StringId)
            {
                return candidateComponent.BannerLevel > currentComponent.BannerLevel;
            }

            return false;
        }

        public static bool StrictlyBeatsWeapon(
            EquipmentElement candidate,
            EquipmentElement current,
            ProjectileUpgradePreference ammoPreference = ProjectileUpgradePreference.CountAndDamage,
            ProjectileUpgradePreference throwingPreference = ProjectileUpgradePreference.CountAndDamage,
            bool ignoreThrowingMeleeStats = true)
        {
            if (current.IsEmpty || candidate.IsEmpty) return false;

            var wC = candidate.Item?.PrimaryWeapon;
            var wE = current.Item?.PrimaryWeapon;
            if (wC == null || wE == null) return false;

            return EquipmentDecisionMath.StrictlyBeatsWeapon(
                ToWeaponStats(candidate),
                ToWeaponStats(current),
                ammoPreference,
                throwingPreference,
                ignoreThrowingMeleeStats);
        }

        public static bool StrictlyBeatsWeapon(
            EquipmentElement candidate,
            EquipmentElement current,
            WeaponEvaluationContext context)
        {
            return EvaluateWeaponUpgrade(candidate, current, context).IsUpgrade;
        }

        public static WeaponEvaluationResult EvaluateWeaponUpgrade(
            EquipmentElement candidate,
            EquipmentElement current,
            WeaponEvaluationContext context)
        {
            if (candidate.IsEmpty) return new WeaponEvaluationResult(false, -9999f, GetWeaponScore(current, context), "candidate is empty");
            return EquipmentDecisionMath.EvaluateWeaponUpgrade(
                ToWeaponStats(candidate),
                ToWeaponStats(current),
                context);
        }

        public static float GetWeaponScore(
            EquipmentElement equipmentElement,
            ProjectileUpgradePreference ammoPreference = ProjectileUpgradePreference.CountAndDamage,
            ProjectileUpgradePreference throwingPreference = ProjectileUpgradePreference.CountAndDamage,
            bool ignoreThrowingMeleeStats = true)
        {
            return EquipmentDecisionMath.GetWeaponScore(
                ToWeaponStats(equipmentElement),
                ammoPreference,
                throwingPreference,
                ignoreThrowingMeleeStats);
        }

        public static float GetWeaponScore(EquipmentElement equipmentElement, WeaponEvaluationContext context)
        {
            return EquipmentDecisionMath.GetWeaponScore(ToWeaponStats(equipmentElement), context);
        }

        public static WeaponEvaluationContext CreateWeaponEvaluationContext(
            Settings settings,
            Hero? hero,
            InventoryLogic.InventorySide side)
        {
            bool isMountedBattle = side == InventoryLogic.InventorySide.BattleEquipment && HasBattleMount(hero);
            bool canUseAllBowsMounted = hero?.GetPerkValue(DefaultPerks.Bow.MountedArchery) ?? false;
            bool canReloadAllCrossbowsMounted = hero?.GetPerkValue(DefaultPerks.Crossbow.MountedCrossbowman) ?? false;

            return new WeaponEvaluationContext(
                ammoPreference: settings.AmmoUpgradePreferenceSetting,
                throwingPreference: settings.ThrowingWeaponUpgradePreferenceSetting,
                ignoreThrowingMeleeStats: settings.IgnoreThrowingWeaponMeleeStats,
                oneHandedSwordPreference: settings.OneHandedSwordPreferenceSetting,
                oneHandedAxeMacePreference: settings.OneHandedAxeMacePreferenceSetting,
                twoHandedPreference: settings.TwoHandedPreferenceSetting,
                thrustPolearmPreference: settings.ThrustPolearmPreferenceSetting,
                swingPolearmPreference: settings.SwingPolearmPreferenceSetting,
                rangedPreference: settings.RangedWeaponPreferenceSetting,
                shieldPreference: settings.ShieldPreferenceSetting,
                propertyMatching: settings.WeaponPropertyMatchingSetting,
                isMountedBattle: isMountedBattle,
                canUseAllBowsMounted: canUseAllBowsMounted,
                canReloadAllCrossbowsMounted: canReloadAllCrossbowsMounted,
                hardRestrictionFlags: HardRestrictionFlags,
                minorPropertyFlags: MinorPropertyFlags,
                cantReloadOnHorsebackFlag: CantReloadOnHorsebackFlag);
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
            var item = equipmentElement.Item;
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
                weapon.IsShield || weapon.IsAmmo,
                weapon.IsAmmo,
                item?.ItemType == ItemObject.ItemTypeEnum.Thrown,
                ToWeaponRole(item, weapon),
                item != null ? (int)item.Tier : 0,
                weapon.ItemUsage);
        }

        private static WeaponRole ToWeaponRole(ItemObject? item, WeaponComponentData weapon)
        {
            if (weapon.IsAmmo) return WeaponRole.Ammo;
            if (weapon.IsShield || item?.ItemType == ItemObject.ItemTypeEnum.Shield) return WeaponRole.Shield;
            if (item?.ItemType == ItemObject.ItemTypeEnum.Thrown) return WeaponRole.Throwing;

            string weaponClass = weapon.WeaponClass.ToString();
            switch (weaponClass)
            {
                case "Dagger":
                    return WeaponRole.Dagger;
                case "OneHandedSword":
                    return WeaponRole.OneHandedSword;
                case "OneHandedAxe":
                case "Mace":
                    return WeaponRole.OneHandedAxeMace;
                case "TwoHandedSword":
                case "TwoHandedAxe":
                    return WeaponRole.TwoHanded;
                case "Bow":
                case "Crossbow":
                    return WeaponRole.Ranged;
            }

            if (item?.ItemType == ItemObject.ItemTypeEnum.Polearm || weaponClass == "LowGripPolearm" || weaponClass == "TwoHandedPolearm")
            {
                return weapon.SwingDamage > 0 && weapon.SwingDamage >= weapon.ThrustDamage
                    ? WeaponRole.SwingPolearm
                    : WeaponRole.ThrustPolearm;
            }

            if (weapon.IsRangedWeapon && !weapon.IsMeleeWeapon)
            {
                return WeaponRole.Ranged;
            }

            return WeaponRole.Other;
        }

        private static bool HasBattleMount(Hero? hero)
        {
            if (hero == null) return false;
            var horse = hero.BattleEquipment.Horse;
            return !horse.IsEmpty && horse.Item != null;
        }
    }
}
