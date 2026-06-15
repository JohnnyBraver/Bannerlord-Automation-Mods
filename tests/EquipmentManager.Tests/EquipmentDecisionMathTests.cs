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
        public void StrictlyBeatsWeapon_RejectsSideGradesEvenWhenScoreWouldIncrease()
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
            Assert.False(EquipmentDecisionMath.StrictlyBeatsWeapon(sideGrade, current));
            Assert.True(EquipmentDecisionMath.GetWeaponScore(sideGrade) > EquipmentDecisionMath.GetWeaponScore(current));
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
            bool isShieldOrAmmo = false)
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
                isShieldOrAmmo);
        }
    }
}
