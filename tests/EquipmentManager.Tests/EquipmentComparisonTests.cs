using EquipmentManager;
using TaleWorlds.Core;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentComparisonTests
    {
        [Theory]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        [InlineData(false, true, true)]
        public void ShouldEvaluateWeaponSlot_IgnoresEmptyOrNonWeaponCurrentSlots(
            bool currentIsEmpty,
            bool currentHasPrimaryWeapon,
            bool expected)
        {
            bool result = EquipmentComparison.ShouldEvaluateWeaponSlot(
                currentIsEmpty,
                currentHasPrimaryWeapon,
                isStealthLoadout: false,
                ItemObject.ItemTypeEnum.OneHandedWeapon,
                "sword");

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ShouldEvaluateWeaponSlot_IgnoresSpecialSneakingThrowingStone()
        {
            bool result = EquipmentComparison.ShouldEvaluateWeaponSlot(
                currentIsEmpty: false,
                currentHasPrimaryWeapon: true,
                isStealthLoadout: true,
                ItemObject.ItemTypeEnum.Thrown,
                "stealth_throwing_stone");

            Assert.False(result);
        }

        [Fact]
        public void ShouldEvaluateWeaponSlot_DoesNotIgnoreOtherThrownWeapons()
        {
            bool result = EquipmentComparison.ShouldEvaluateWeaponSlot(
                currentIsEmpty: false,
                currentHasPrimaryWeapon: true,
                isStealthLoadout: true,
                ItemObject.ItemTypeEnum.Thrown,
                "javelin");

            Assert.True(result);
        }
    }
}

