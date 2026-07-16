using EquipmentManager;
using TaleWorlds.Core;
using Xunit;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

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
        public void Settings_DefaultProtectionCategoriesAndArmorReservesAreOff()
        {
            var settings = new Settings();

            Assert.Equal(PositiveModifierProtectionCategory.Disabled, settings.PositiveModifierProtectionCategorySetting);
            Assert.Equal(0, settings.AdditionalArmorSetsToKeep);
            Assert.Equal(SpareArmorLoadouts.Disabled, settings.SpareArmorLoadoutsSetting);
        }

        [Fact]
        public void AutoSellCategory_DefaultsToWeaponsAndArmor()
        {
            var settings = new Settings();

            Assert.Equal(AutoSellCategory.WeaponsAndArmor, settings.AutoSellCategorySetting);
        }

        [Fact]
        public void PositiveModifierProtectionCategory_DefaultsToDisabled()
        {
            var settings = new Settings();

            Assert.Equal(PositiveModifierProtectionCategory.Disabled, settings.PositiveModifierProtectionCategorySetting);
        }

        [Fact]
        public void AutoSellCategory_SelectsOnlyTheRequestedEquipmentTypes()
        {
            var armor = ArmorItem("helmet");
            var weapon = WeaponItem("sword");
            var banner = BannerItem("banner");

            Assert.False(EquipmentSaleProtector.IsSelectedForAutoSale(armor, AutoSellCategory.Disabled));
            Assert.True(EquipmentSaleProtector.IsSelectedForAutoSale(armor, AutoSellCategory.ArmorOnly));
            Assert.False(EquipmentSaleProtector.IsSelectedForAutoSale(weapon, AutoSellCategory.ArmorOnly));
            Assert.False(EquipmentSaleProtector.IsSelectedForAutoSale(armor, AutoSellCategory.WeaponsOnly));
            Assert.True(EquipmentSaleProtector.IsSelectedForAutoSale(weapon, AutoSellCategory.WeaponsOnly));
            Assert.True(EquipmentSaleProtector.IsSelectedForAutoSale(armor, AutoSellCategory.WeaponsAndArmor));
            Assert.True(EquipmentSaleProtector.IsSelectedForAutoSale(weapon, AutoSellCategory.WeaponsAndArmor));
            Assert.False(EquipmentSaleProtector.IsSelectedForAutoSale(banner, AutoSellCategory.WeaponsAndArmor));

            Assert.True(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(armor, PositiveModifierProtectionCategory.ArmorOnly));
            Assert.False(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(weapon, PositiveModifierProtectionCategory.ArmorOnly));
            Assert.False(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(armor, PositiveModifierProtectionCategory.WeaponsOnly));
            Assert.True(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(weapon, PositiveModifierProtectionCategory.WeaponsOnly));
            Assert.True(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(armor, PositiveModifierProtectionCategory.WeaponsAndArmor));
            Assert.True(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(weapon, PositiveModifierProtectionCategory.WeaponsAndArmor));
            Assert.False(EquipmentSaleProtector.IsSelectedForPositiveModifierProtection(banner, PositiveModifierProtectionCategory.WeaponsAndArmor));
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

        [Fact]
        public void BuildProtectionPlan_ProtectsNotMerchandiseItems()
        {
            var item = Item("not_merch_item");
            var field = typeof(ItemObject).GetField("_notMerchandise", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(item, true);
            }
            var backingField = typeof(ItemObject).GetField("<NotMerchandise>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (backingField != null)
            {
                backingField.SetValue(item, true);
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

        private static ItemObject ArmorItem(string id)
        {
            var item = Item(id);
            SetPrivateField(item, "<ItemComponent>k__BackingField", FormatterServices.GetUninitializedObject(typeof(ArmorComponent)));
            return item;
        }

        private static ItemObject WeaponItem(string id)
        {
            var item = Item(id);
            SetPrivateField(item, "<ItemComponent>k__BackingField", FormatterServices.GetUninitializedObject(typeof(WeaponComponent)));
            return item;
        }

        private static ItemObject BannerItem(string id)
        {
            var item = Item(id);
            SetPrivateField(item, "<ItemComponent>k__BackingField", FormatterServices.GetUninitializedObject(typeof(BannerComponent)));
            return item;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(target, value);
        }
    }
}
