using EquipmentManager;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentDecisionMathTests
    {
        [Fact]
        public void StrictlyBeatsArmor_AcceptsOnlyEqualOrBetterProtectionForNormalLoadouts()
        {
            var current = new ArmorStats(head: 10, body: 20, leg: 8, arm: 5, stealth: 0);
            var strictUpgrade = new ArmorStats(head: 10, body: 22, leg: 8, arm: 5, stealth: 0);
            var sideGrade = new ArmorStats(head: 9, body: 26, leg: 8, arm: 5, stealth: 0);

            Assert.True(EquipmentDecisionMath.StrictlyBeatsArmor(strictUpgrade, current, prioritizeStealth: false));
            Assert.False(EquipmentDecisionMath.StrictlyBeatsArmor(sideGrade, current, prioritizeStealth: false));
            Assert.True(EquipmentDecisionMath.GetArmorScore(sideGrade, prioritizeStealth: false) > EquipmentDecisionMath.GetArmorScore(current, prioritizeStealth: false));
        }

        [Fact]
        public void StrictlyBeatsArmor_ForStealthLoadouts_PrioritizesStealthBeforeProtection()
        {
            var current = new ArmorStats(head: 10, body: 20, leg: 8, arm: 5, stealth: 5);
            var stealthUpgrade = new ArmorStats(head: 0, body: 1, leg: 0, arm: 0, stealth: 6);
            var stealthDowngrade = new ArmorStats(head: 99, body: 99, leg: 99, arm: 99, stealth: 4);

            Assert.True(EquipmentDecisionMath.StrictlyBeatsArmor(stealthUpgrade, current, prioritizeStealth: true));
            Assert.False(EquipmentDecisionMath.StrictlyBeatsArmor(stealthDowngrade, current, prioritizeStealth: true));
            Assert.Equal(6043f, EquipmentDecisionMath.GetArmorScore(new ArmorStats(10, 20, 8, 5, 6), prioritizeStealth: true));
        }

        [Fact]
        public void StrictlyBeatsWeapon_AcceptsPreferredStatTradeoffsWhenScoreImproves()
        {
            var current = Weapon(
                swingDamage: 70,
                swingSpeed: 80,
                handling: 80,
                weaponLength: 100);
            var strictUpgrade = Weapon(
                swingDamage: 70,
                swingSpeed: 80,
                handling: 81,
                weaponLength: 100);
            var sideGrade = Weapon(
                swingDamage: 90,
                swingSpeed: 80,
                handling: 79,
                weaponLength: 100);

            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(strictUpgrade, current));
            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(sideGrade, current));
            Assert.True(EquipmentDecisionMath.GetWeaponScore(sideGrade) > EquipmentDecisionMath.GetWeaponScore(current));
        }

        [Fact]
        public void EvaluateWeaponUpgrade_BalancedAcceptsDamageAndReachTradeoffForSwords()
        {
            var falchion = Weapon(
                swingSpeed: 98,
                swingDamage: 63,
                weaponLength: 71,
                handling: 102,
                role: WeaponRole.OneHandedSword,
                itemTier: 2);
            var spatha = Weapon(
                swingSpeed: 89,
                swingDamage: 69,
                weaponLength: 108,
                handling: 90,
                role: WeaponRole.OneHandedSword,
                itemTier: 4);

            var result = EquipmentDecisionMath.EvaluateWeaponUpgrade(spatha, falchion, new WeaponEvaluationContext());

            Assert.True(result.IsUpgrade);
            Assert.True(result.CandidateScore > result.CurrentScore);
        }

        [Fact]
        public void EvaluateWeaponUpgrade_SpeedHandlingPreferenceCanKeepFasterSword()
        {
            var falchion = Weapon(
                swingSpeed: 98,
                swingDamage: 63,
                weaponLength: 71,
                handling: 102,
                role: WeaponRole.OneHandedSword,
                itemTier: 2);
            var spatha = Weapon(
                swingSpeed: 89,
                swingDamage: 69,
                weaponLength: 108,
                handling: 90,
                role: WeaponRole.OneHandedSword,
                itemTier: 4);
            var context = new WeaponEvaluationContext(
                oneHandedSwordPreference: MeleeWeaponUpgradePreference.SpeedAndHandling);

            var result = EquipmentDecisionMath.EvaluateWeaponUpgrade(spatha, falchion, context);

            Assert.False(result.IsUpgrade);
            Assert.True(result.CandidateScore <= result.CurrentScore);
        }

        [Theory]
        [InlineData((int)WeaponRole.OneHandedAxeMace)]
        [InlineData((int)WeaponRole.TwoHanded)]
        [InlineData((int)WeaponRole.ThrustPolearm)]
        [InlineData((int)WeaponRole.SwingPolearm)]
        public void EvaluateWeaponUpgrade_MeleeCategoryPreferencesApplyToEachRole(int roleValue)
        {
            var role = (WeaponRole)roleValue;
            var current = MeleeRoleWeapon(role, damage: 60, speed: 95, reach: 80, handling: 100);
            var candidate = MeleeRoleWeapon(role, damage: 72, speed: 84, reach: 115, handling: 82);

            var damageReach = ContextForMeleeRole(role, MeleeWeaponUpgradePreference.DamageAndReach);
            var speedHandling = ContextForMeleeRole(role, MeleeWeaponUpgradePreference.SpeedAndHandling);

            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(candidate, current, damageReach).IsUpgrade);
            Assert.False(EquipmentDecisionMath.EvaluateWeaponUpgrade(candidate, current, speedHandling).IsUpgrade);
        }

        [Fact]
        public void StrictlyBeatsWeapon_RequiresCandidateFlagsToPreserveCurrentCapabilities()
        {
            var current = Weapon(flags: 0b011);
            var missingCapability = Weapon(handling: 81, flags: 0b001);
            var supersetCapability = Weapon(handling: 81, flags: 0b111);

            Assert.False(EquipmentDecisionMath.StrictlyBeatsWeapon(missingCapability, current));
            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(supersetCapability, current));
        }

        [Fact]
        public void EvaluateWeaponUpgrade_IgnoreMinorPropertiesAllowsMissingMinorFlag()
        {
            var current = Weapon(swingDamage: 50, flags: 0b101);
            var candidate = Weapon(swingDamage: 60, flags: 0b001);

            var preserveAll = new WeaponEvaluationContext(
                propertyMatching: WeaponPropertyMatching.PreserveAll,
                minorPropertyFlags: 0b100);
            var ignoreMinor = new WeaponEvaluationContext(
                propertyMatching: WeaponPropertyMatching.IgnoreMinor,
                minorPropertyFlags: 0b100);

            Assert.False(EquipmentDecisionMath.EvaluateWeaponUpgrade(candidate, current, preserveAll).IsUpgrade);
            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(candidate, current, ignoreMinor).IsUpgrade);
        }

        [Fact]
        public void EvaluateWeaponUpgrade_MountedBattleRejectsMountedUsageRestrictionsUnlessOverridden()
        {
            const long cantReloadOnHorseback = 0b1000;
            var current = Weapon(swingDamage: 40, flags: cantReloadOnHorseback, role: WeaponRole.Ranged, isMelee: false, isRanged: true);
            var candidate = Weapon(swingDamage: 50, missileDamage: 60, flags: cantReloadOnHorseback, role: WeaponRole.Ranged, isMelee: false, isRanged: true);
            var bowCurrent = Weapon(swingDamage: 40, flags: 0, role: WeaponRole.Ranged, isMelee: false, isRanged: true);
            var longBowCandidate = Weapon(swingDamage: 50, missileDamage: 60, role: WeaponRole.Ranged, isMelee: false, isRanged: true, itemUsage: "long_bow");

            var mountedBlocked = new WeaponEvaluationContext(
                isMountedBattle: true,
                canReloadAllCrossbowsMounted: false,
                cantReloadOnHorsebackFlag: cantReloadOnHorseback);
            var mountedAllowed = new WeaponEvaluationContext(
                isMountedBattle: true,
                canReloadAllCrossbowsMounted: true,
                cantReloadOnHorsebackFlag: cantReloadOnHorseback);

            Assert.False(EquipmentDecisionMath.EvaluateWeaponUpgrade(candidate, current, mountedBlocked).IsUpgrade);
            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(candidate, current, mountedAllowed).IsUpgrade);

            Assert.False(EquipmentDecisionMath.EvaluateWeaponUpgrade(longBowCandidate, bowCurrent, mountedBlocked).IsUpgrade);
            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(longBowCandidate, bowCurrent, new WeaponEvaluationContext(
                isMountedBattle: true,
                canUseAllBowsMounted: true,
                cantReloadOnHorsebackFlag: cantReloadOnHorseback)).IsUpgrade);
        }

        [Fact]
        public void EvaluateWeaponUpgrade_RangedPreferenceChoosesDamageOrAccuracy()
        {
            var current = Weapon(
                missileDamage: 60,
                missileSpeed: 80,
                accuracy: 90,
                isMelee: false,
                isRanged: true,
                role: WeaponRole.Ranged);
            var damageCandidate = Weapon(
                missileDamage: 70,
                missileSpeed: 80,
                accuracy: 80,
                isMelee: false,
                isRanged: true,
                role: WeaponRole.Ranged);
            var accuracyCandidate = Weapon(
                missileDamage: 58,
                missileSpeed: 80,
                accuracy: 100,
                isMelee: false,
                isRanged: true,
                role: WeaponRole.Ranged);

            var damageContext = new WeaponEvaluationContext(rangedPreference: RangedWeaponUpgradePreference.Damage);
            var accuracyContext = new WeaponEvaluationContext(rangedPreference: RangedWeaponUpgradePreference.Accuracy);

            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(damageCandidate, current, damageContext).IsUpgrade);
            Assert.False(EquipmentDecisionMath.EvaluateWeaponUpgrade(accuracyCandidate, current, damageContext).IsUpgrade);
            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(accuracyCandidate, current, accuracyContext).IsUpgrade);
        }

        [Fact]
        public void EvaluateWeaponUpgrade_ShieldPreferenceChoosesHitPointsOrSize()
        {
            var current = Weapon(
                weaponLength: 80,
                durability: 200,
                isMelee: false,
                isShieldOrAmmo: true,
                role: WeaponRole.Shield);
            var hitPointCandidate = Weapon(
                weaponLength: 70,
                durability: 260,
                isMelee: false,
                isShieldOrAmmo: true,
                role: WeaponRole.Shield);
            var sizeCandidate = Weapon(
                weaponLength: 110,
                durability: 180,
                isMelee: false,
                isShieldOrAmmo: true,
                role: WeaponRole.Shield);

            var hpContext = new WeaponEvaluationContext(shieldPreference: ShieldUpgradePreference.MaxHitPoints);
            var sizeContext = new WeaponEvaluationContext(shieldPreference: ShieldUpgradePreference.MaxSize);

            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(hitPointCandidate, current, hpContext).IsUpgrade);
            Assert.False(EquipmentDecisionMath.EvaluateWeaponUpgrade(sizeCandidate, current, hpContext).IsUpgrade);
            Assert.True(EquipmentDecisionMath.EvaluateWeaponUpgrade(sizeCandidate, current, sizeContext).IsUpgrade);
        }

        [Fact]
        public void StrictlyBeatsWeapon_ForAmmoDefaultRequiresCountAndDamage()
        {
            var current = Ammo(durability: 20, missileDamage: 4);
            var moreAmmoLessDamage = Ammo(durability: 25, missileDamage: 3);
            var moreDamageSameAmmo = Ammo(durability: 20, missileDamage: 5);

            Assert.False(EquipmentDecisionMath.StrictlyBeatsWeapon(moreAmmoLessDamage, current, ProjectileUpgradePreference.CountAndDamage));
            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(moreDamageSameAmmo, current, ProjectileUpgradePreference.CountAndDamage));
            Assert.True(EquipmentDecisionMath.GetWeaponScore(moreAmmoLessDamage, ProjectileUpgradePreference.CountAndDamage) > EquipmentDecisionMath.GetWeaponScore(current, ProjectileUpgradePreference.CountAndDamage));
        }

        [Fact]
        public void StrictlyBeatsWeapon_ForAmmoCountOnlyIgnoresDamageDrop()
        {
            var current = Ammo(durability: 20, missileDamage: 4);
            var moreAmmoLessDamage = Ammo(durability: 25, missileDamage: 3);

            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(moreAmmoLessDamage, current, ProjectileUpgradePreference.CountOnly));
            Assert.True(EquipmentDecisionMath.GetWeaponScore(moreAmmoLessDamage, ProjectileUpgradePreference.CountOnly) > EquipmentDecisionMath.GetWeaponScore(current, ProjectileUpgradePreference.CountOnly));
        }

        [Fact]
        public void StrictlyBeatsWeapon_ForAmmoDamageOnlyIgnoresCountDrop()
        {
            var current = Ammo(durability: 20, missileDamage: 4);
            var moreDamageLessAmmo = Ammo(durability: 15, missileDamage: 5);

            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(moreDamageLessAmmo, current, ProjectileUpgradePreference.DamageOnly));
            Assert.True(EquipmentDecisionMath.GetWeaponScore(moreDamageLessAmmo, ProjectileUpgradePreference.DamageOnly) > EquipmentDecisionMath.GetWeaponScore(current, ProjectileUpgradePreference.DamageOnly));
        }

        [Fact]
        public void StrictlyBeatsWeapon_ForThrowingCountOnlyIgnoresDamageAndMeleeDropsByDefault()
        {
            var current = ThrowingWeapon(durability: 4, missileDamage: 50, handling: 90);
            var moreThrowingWeapons = ThrowingWeapon(durability: 5, missileDamage: 45, handling: 70);

            Assert.True(EquipmentDecisionMath.StrictlyBeatsWeapon(
                moreThrowingWeapons,
                current,
                throwingPreference: ProjectileUpgradePreference.CountOnly,
                ignoreThrowingMeleeStats: true));
        }

        [Fact]
        public void StrictlyBeatsWeapon_ForThrowingCanRequireMeleeStats()
        {
            var current = ThrowingWeapon(durability: 4, missileDamage: 50, handling: 90);
            var moreThrowingWeapons = ThrowingWeapon(durability: 5, missileDamage: 45, handling: 70);

            Assert.False(EquipmentDecisionMath.StrictlyBeatsWeapon(
                moreThrowingWeapons,
                current,
                throwingPreference: ProjectileUpgradePreference.CountOnly,
                ignoreThrowingMeleeStats: false));
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(15, 0, 30)]
        [InlineData(15, 4, 150)]
        public void GetCascadeIterationLimit_BoundsDisplacedItemPasses(int slotTargetCount, int initialQueueCount, int expected)
        {
            Assert.Equal(expected, EquipmentDecisionMath.GetCascadeIterationLimit(slotTargetCount, initialQueueCount));
        }

        private static WeaponStats Weapon(
            int weaponClass = 1,
            int thrustSpeed = 10,
            int swingSpeed = 10,
            int missileSpeed = 10,
            int thrustDamage = 10,
            int swingDamage = 10,
            int missileDamage = 10,
            int weaponLength = 10,
            int handling = 10,
            int accuracy = 10,
            int durability = 10,
            long flags = 0b001,
            bool isMelee = true,
            bool isRanged = false,
            bool isShieldOrAmmo = false,
            bool isAmmo = false,
            bool isThrown = false,
            WeaponRole role = WeaponRole.Other,
            int itemTier = 0,
            string itemUsage = "")
        {
            return new WeaponStats(
                true,
                weaponClass,
                thrustSpeed,
                swingSpeed,
                missileSpeed,
                thrustDamage,
                swingDamage,
                missileDamage,
                weaponLength,
                handling,
                accuracy,
                durability,
                flags,
                isMelee,
                isRanged,
                isShieldOrAmmo,
                isAmmo,
                isThrown,
                role,
                itemTier,
                itemUsage);
        }

        private static WeaponStats Ammo(int durability, int missileDamage)
        {
            return Weapon(
                weaponClass: 2,
                missileDamage: missileDamage,
                durability: durability,
                isMelee: false,
                isShieldOrAmmo: true,
                isAmmo: true);
        }

        private static WeaponStats ThrowingWeapon(int durability, int missileDamage, int handling)
        {
            return Weapon(
                weaponClass: 3,
                missileDamage: missileDamage,
                durability: durability,
                handling: handling,
                isMelee: true,
                isRanged: true,
                isThrown: true);
        }

        private static WeaponStats MeleeRoleWeapon(WeaponRole role, int damage, int speed, int reach, int handling)
        {
            if (role == WeaponRole.ThrustPolearm)
            {
                return Weapon(
                    weaponClass: 4,
                    thrustDamage: damage,
                    thrustSpeed: speed,
                    swingDamage: 0,
                    swingSpeed: 0,
                    weaponLength: reach,
                    handling: handling,
                    role: role);
            }

            return Weapon(
                weaponClass: 4,
                swingDamage: damage,
                swingSpeed: speed,
                thrustDamage: 0,
                thrustSpeed: 0,
                weaponLength: reach,
                handling: handling,
                role: role);
        }

        private static WeaponEvaluationContext ContextForMeleeRole(WeaponRole role, MeleeWeaponUpgradePreference preference)
        {
            switch (role)
            {
                case WeaponRole.OneHandedAxeMace:
                    return new WeaponEvaluationContext(oneHandedAxeMacePreference: preference);
                case WeaponRole.TwoHanded:
                    return new WeaponEvaluationContext(twoHandedPreference: preference);
                case WeaponRole.ThrustPolearm:
                    return new WeaponEvaluationContext(thrustPolearmPreference: preference);
                case WeaponRole.SwingPolearm:
                    return new WeaponEvaluationContext(swingPolearmPreference: preference);
                default:
                    return new WeaponEvaluationContext(oneHandedSwordPreference: preference);
            }
        }
    }
}
