using EquipmentManager;
using TaleWorlds.Core;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentSaleProtectorTests
    {
        [Fact]
        public void ProtectionPlan_ProtectsPartialStackWithoutLockingWholeStack()
        {
            var equipment = Equipment("sword");
            var plan = new EquipmentProtectionPlan(new[]
            {
                new EquipmentProtectionItem(equipment, 3, 50)
            });

            plan.Protect(equipment, 1);

            Assert.Equal(2, plan.GetSellableQuantity(equipment, 3));
        }

        [Fact]
        public void GetEquipmentKey_IncludesModifierIdentity()
        {
            var item = Item("helmet");
            var fine = new ItemModifier { StringId = "fine" };
            var cracked = new ItemModifier { StringId = "cracked" };

            string fineKey = EquipmentSaleProtector.GetEquipmentKey(new EquipmentElement(item, fine, null!, false));
            string crackedKey = EquipmentSaleProtector.GetEquipmentKey(new EquipmentElement(item, cracked, null!, false));

            Assert.NotEqual(fineKey, crackedKey);
            Assert.Contains("helmet", fineKey);
            Assert.Contains("fine", fineKey);
        }

        [Fact]
        public void Settings_DefaultQualityReserveProtectionIsOff()
        {
            var settings = new Settings();

            Assert.False(settings.KeepPositiveModifiers);
            Assert.Equal(0, settings.AdditionalArmorSetsToKeep);
            Assert.False(settings.KeepSpareCombatArmorSets);
            Assert.False(settings.KeepSpareCivilianArmorSets);
            Assert.False(settings.KeepSpareSneakingArmorSets);
        }

        private static EquipmentElement Equipment(string id)
        {
            return new EquipmentElement(Item(id), null, null!, false);
        }

        private static ItemObject Item(string id)
        {
            return new ItemObject(id);
        }
    }
}
