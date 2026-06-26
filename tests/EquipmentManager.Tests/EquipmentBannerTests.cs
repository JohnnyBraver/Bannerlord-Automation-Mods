using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Reflection;
using EquipmentManager;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Localization;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentBannerTests
    {
        private class MockEquipmentTransferContext : EquipmentEngine.EquipmentTransferContext
        {
            public List<EquipmentEngine.AvailableEquipment> Pool { get; set; } = new List<EquipmentEngine.AvailableEquipment>();
            public List<(EquipmentEngine.AvailableEquipment Item, EquipmentEngine.EquipTarget Target, EquipmentIndex Slot)> Equipped { get; } = new List<(EquipmentEngine.AvailableEquipment, EquipmentEngine.EquipTarget, EquipmentIndex)>();

            public override List<EquipmentEngine.AvailableEquipment> BuildAvailableEquipmentPool()
            {
                return Pool;
            }

            public override void EquipItem(EquipmentEngine.AvailableEquipment available, EquipmentEngine.EquipTarget target, EquipmentIndex slot)
            {
                Equipped.Add((available, target, slot));
                var equipment = target.Side == InventoryLogic.InventorySide.StealthEquipment
                    ? target.Hero.StealthEquipment
                    : (target.Side == InventoryLogic.InventorySide.CivilianEquipment ? target.Hero.CivilianEquipment : target.Hero.BattleEquipment);
                equipment[slot] = available.EquipmentElement;
            }
        }


        [Fact]
        public void BannerAutoEquip_EquipsBestBanner_WhenSlotIsEmpty()
        {
            var hero = CreateMockHero("Hero1");
            var settings = new Settings();
            settings.AutoEquipCategoryDropdown.SelectedIndex = 3; // Weapons & Armor

            var bannerL1 = CreateMockBannerItem("banner_l1", 1, "melee_damage");
            var bannerL3 = CreateMockBannerItem("banner_l3", 3, "melee_damage");
            var bannerL2 = CreateMockBannerItem("banner_l2", 2, "melee_damage");

            var context = new MockEquipmentTransferContext
            {
                Pool = new List<EquipmentEngine.AvailableEquipment>
                {
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(bannerL1, null, null!, false), 1),
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(bannerL3, null, null!, false), 1),
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(bannerL2, null, null!, false), 1)
                }
            };

            int count = EquipmentEngine.EvaluateAndEquip(context, new List<Hero> { hero }, settings, null);

            Assert.Equal(1, count);
            Assert.Single(context.Equipped);
            Assert.Equal("banner_l3", context.Equipped[0].Item.EquipmentElement.Item.StringId);
            Assert.Equal(EquipmentIndex.ExtraWeaponSlot, context.Equipped[0].Slot);
            Assert.Equal("banner_l3", hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item.StringId);
        }

        [Fact]
        public void BannerAutoEquip_OnlyUpgrades_WhenSameEffectAndHigherLevel()
        {
            var hero = CreateMockHero("Hero1");
            var settings = new Settings();
            settings.AutoEquipCategoryDropdown.SelectedIndex = 3; // Weapons & Armor

            var currentBanner = CreateMockBannerItem("banner_melee_l1", 1, "melee_damage");
            hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot] = new EquipmentElement(currentBanner, null, null!, false);

            var bannerMeleeL2 = CreateMockBannerItem("banner_melee_l2", 2, "melee_damage");
            var bannerSpeedL3 = CreateMockBannerItem("banner_speed_l3", 3, "speed");

            var context = new MockEquipmentTransferContext
            {
                Pool = new List<EquipmentEngine.AvailableEquipment>
                {
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(bannerSpeedL3, null, null!, false), 1),
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(bannerMeleeL2, null, null!, false), 1)
                }
            };

            int count = EquipmentEngine.EvaluateAndEquip(context, new List<Hero> { hero }, settings, null);

            // Should upgrade to banner_melee_l2 (same effect, higher level)
            // Should NOT upgrade to banner_speed_l3 (different effect, even though level is higher)
            Assert.Equal(1, count);
            Assert.Single(context.Equipped);
            Assert.Equal("banner_melee_l2", context.Equipped[0].Item.EquipmentElement.Item.StringId);
            Assert.Equal("banner_melee_l2", hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item.StringId);
        }

        [Fact]
        public void BannerAutoEquip_DoesNotTouch_QuestBannerWithNoEffect()
        {
            var hero = CreateMockHero("Hero1");
            var settings = new Settings();
            settings.AutoEquipCategoryDropdown.SelectedIndex = 3; // Weapons & Armor

            var questBanner = CreateMockBannerItem("dragon_banner", 1, null); // No effect
            hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot] = new EquipmentElement(questBanner, null, null!, false);

            var betterBanner = CreateMockBannerItem("banner_melee_l3", 3, "melee_damage");

            var context = new MockEquipmentTransferContext
            {
                Pool = new List<EquipmentEngine.AvailableEquipment>
                {
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(betterBanner, null, null!, false), 1)
                }
            };

            int count = EquipmentEngine.EvaluateAndEquip(context, new List<Hero> { hero }, settings, null);

            // Should not replace quest banner with no effect
            Assert.Equal(0, count);
            Assert.Empty(context.Equipped);
            Assert.Equal("dragon_banner", hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item.StringId);
        }

        [Fact]
        public void BannerAutoEquip_DoesNotEquip_BannersWithNoEffectFromInventory()
        {
            var hero = CreateMockHero("Hero1");
            var settings = new Settings();
            settings.AutoEquipCategoryDropdown.SelectedIndex = 3; // Weapons & Armor

            var questBanner = CreateMockBannerItem("dragon_banner_piece_1", 1, null); // No effect

            var context = new MockEquipmentTransferContext
            {
                Pool = new List<EquipmentEngine.AvailableEquipment>
                {
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(questBanner, null, null!, false), 1)
                }
            };

            int count = EquipmentEngine.EvaluateAndEquip(context, new List<Hero> { hero }, settings, null);

            // Should not equip banner with no effect
            Assert.Equal(0, count);
            Assert.Empty(context.Equipped);
            Assert.True(hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        }

        [Fact]
        public void BannerAutoEquip_CascadesDisplacedBanners()
        {
            var hero1 = CreateMockHero("Hero1");
            var hero2 = CreateMockHero("Hero2");
            var settings = new Settings();
            settings.AutoEquipCategoryDropdown.SelectedIndex = 3; // Weapons & Armor

            var bannerMeleeL1 = CreateMockBannerItem("banner_melee_l1", 1, "melee_damage");
            var bannerMeleeL2 = CreateMockBannerItem("banner_melee_l2", 2, "melee_damage");

            // Hero1 has L1 banner
            hero1.BattleEquipment[EquipmentIndex.ExtraWeaponSlot] = new EquipmentElement(bannerMeleeL1, null, null!, false);
            // Hero2 has empty slot

            var context = new MockEquipmentTransferContext
            {
                Pool = new List<EquipmentEngine.AvailableEquipment>
                {
                    new EquipmentEngine.AvailableEquipment(new EquipmentElement(bannerMeleeL2, null, null!, false), 1)
                }
            };

            int count = EquipmentEngine.EvaluateAndEquip(context, new List<Hero> { hero1, hero2 }, settings, null);

            // Hero1 should upgrade to L2.
            // The displaced L1 banner from Hero1 should cascade and be equipped by Hero2 (since Hero2's slot is empty).
            Assert.Equal(2, count);
            Assert.Equal(2, context.Equipped.Count);
            Assert.Equal("banner_melee_l2", hero1.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item.StringId);
            Assert.Equal("banner_melee_l1", hero2.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item.StringId);
        }


        private static Hero CreateMockHero(string name)
        {
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            var charObj = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            
            SetName(charObj, name);
            SetName(hero, name);
            
            SetPrivateField(hero, "_characterObject", charObj);
            SetPrivateField(hero, "<CharacterObject>k__BackingField", charObj);
            
            var battleEquip = new Equipment();
            var civilianEquip = new Equipment();
            var stealthEquip = new Equipment();
            
            SetPrivateField(hero, "<_battleEquipment>k__BackingField", battleEquip);
            SetPrivateField(hero, "<_civilianEquipment>k__BackingField", civilianEquip);
            SetPrivateField(hero, "<_stealthEquipment>k__BackingField", stealthEquip);
            
            return hero;
        }

        private static void SetName(object obj, string name)
        {
            var textObj = new TextObject(name);
            SetPrivateField(obj, "_name", textObj);
            SetPrivateField(obj, "<Name>k__BackingField", textObj);
        }

        private static BannerEffect CreateMockBannerEffect(string stringId)
        {
            var effect = (BannerEffect)FormatterServices.GetUninitializedObject(typeof(BannerEffect));
            SetPrivateField(effect, "<StringId>k__BackingField", stringId);
            return effect;
        }

        private static ItemObject CreateMockBannerItem(string id, int level = 1, string effectId = null)
        {
            var item = new ItemObject(id);
            SetPrivateField(item, "Type", ItemObject.ItemTypeEnum.Banner);

            var bannerComponent = (BannerComponent)FormatterServices.GetUninitializedObject(typeof(BannerComponent));
            SetPrivateField(bannerComponent, "<Item>k__BackingField", item);
            SetPrivateField(bannerComponent, "<BannerLevel>k__BackingField", level);

            if (effectId != null)
            {
                var effect = CreateMockBannerEffect(effectId);
                SetPrivateField(bannerComponent, "<BannerEffect>k__BackingField", effect);
            }

            SetPrivateField(item, "<ItemComponent>k__BackingField", bannerComponent);
            return item;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }

            var baseType = obj.GetType().BaseType;
            while (baseType != null)
            {
                field = baseType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    return;
                }
                baseType = baseType.BaseType;
            }
        }
    }
}
