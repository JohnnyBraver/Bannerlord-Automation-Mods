using EquipmentManager;
using TaleWorlds.Core;
using Xunit;
using System.Linq;

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

        [Fact]
        public void BuildProtectionPlan_ProtectsStealthThrowingStone()
        {
            var item = Item("stealth_throwing_stone");
            var element = new EquipmentElement(item, null, null!, false);
            var protectionItems = new[]
            {
                new EquipmentProtectionItem(element, 5, 10f)
            };

            var plan = EquipmentSaleProtector.BuildProtectionPlan(
                protectionItems,
                new System.Collections.Generic.List<TaleWorlds.CampaignSystem.Hero>(),
                new Settings(),
                hasWeaponDonationPerk: false,
                hasArmorDonationPerk: false);

            Assert.Equal(0, plan.GetSellableQuantity(element, 5));
        }

        [Fact]
        public void BuildProtectionPlan_ProtectsQuestItems()
        {
            var item = Item("quest_item");
            var field = typeof(ItemObject).GetField("<IsUniqueItem>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(item, true);
            }

            var armorComponent = (ArmorComponent)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ArmorComponent));
            var componentField = typeof(ItemObject).GetField("<ItemComponent>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (componentField != null)
            {
                componentField.SetValue(item, armorComponent);
            }

            var element = new EquipmentElement(item, null, null!, false);
            var protectionItems = new[]
            {
                new EquipmentProtectionItem(element, 1, 10f)
            };

            var plan = EquipmentSaleProtector.BuildProtectionPlan(
                protectionItems,
                new System.Collections.Generic.List<TaleWorlds.CampaignSystem.Hero>(),
                new Settings(),
                hasWeaponDonationPerk: false,
                hasArmorDonationPerk: false);

            Assert.Equal(0, plan.GetSellableQuantity(element, 1));
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
