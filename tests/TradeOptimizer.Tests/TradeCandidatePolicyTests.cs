using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TradeOptimizer;
using Xunit;

namespace TradeOptimizer.Tests
{
    public class TradeCandidatePolicyTests
    {
        [Fact]
        public void CanTradeByMode_RejectsNonCommodities()
        {
            var item = new ItemObject("sword");

            Assert.False(TradeCandidatePolicy.IsCommodityCandidate(item));
            Assert.False(TradeCandidatePolicy.CanTradeByMode(
                item,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                isBuy: true));
        }

        [Theory]
        [InlineData(TradingMode.None, true, false)]
        [InlineData(TradingMode.None, false, false)]
        [InlineData(TradingMode.BuyOnly, true, true)]
        [InlineData(TradingMode.BuyOnly, false, false)]
        [InlineData(TradingMode.SellOnly, true, false)]
        [InlineData(TradingMode.SellOnly, false, true)]
        [InlineData(TradingMode.BuyAndSell, true, true)]
        [InlineData(TradingMode.BuyAndSell, false, true)]
        public void CanTradeByMode_RespectsPerCategoryTradingMode(TradingMode foodMode, bool isBuy, bool expected)
        {
            bool allowed = TradeCandidatePolicy.CanTradeByMode(
                isFood: true,
                isLivestock: false,
                isMount: false,
                isCraftingMaterial: false,
                foodMode,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                isBuy);

            Assert.Equal(expected, allowed);
        }

        [Theory]
        [InlineData(TradingMode.None, true, false)]
        [InlineData(TradingMode.None, false, false)]
        [InlineData(TradingMode.BuyOnly, true, true)]
        [InlineData(TradingMode.BuyOnly, false, false)]
        [InlineData(TradingMode.SellOnly, true, false)]
        [InlineData(TradingMode.SellOnly, false, true)]
        [InlineData(TradingMode.BuyAndSell, true, true)]
        [InlineData(TradingMode.BuyAndSell, false, true)]
        public void CanTradeByMode_RespectsCraftingMaterialTradingMode(TradingMode craftingMaterialMode, bool isBuy, bool expected)
        {
            bool allowed = TradeCandidatePolicy.CanTradeByMode(
                isFood: false,
                isLivestock: false,
                isMount: false,
                isCraftingMaterial: true,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                craftingMaterialMode,
                isBuy);

            Assert.Equal(expected, allowed);
        }

        [Fact]
        public void CanTradeByMode_CraftingMaterialPolicyOverridesFoodPolicy()
        {
            bool allowed = TradeCandidatePolicy.CanTradeByMode(
                isFood: true,
                isLivestock: false,
                isMount: false,
                isCraftingMaterial: true,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.None,
                isBuy: true);

            Assert.False(allowed);
        }

        [Theory]
        [InlineData(80, 100, 0.80f, true)]
        [InlineData(81, 100, 0.80f, false)]
        [InlineData(80, 0, 0.80f, false)]
        public void PassesBuyPriceThreshold_UsesAveragePriceAndThreshold(int price, float average, float threshold, bool expected)
        {
            Assert.Equal(expected, TradeCandidatePolicy.PassesBuyPriceThreshold(price, average, threshold));
        }

        [Fact]
        public void GetTradeIdentityKey_IncludesModifierIdentity()
        {
            var item = new ItemObject("flax");
            var fine = new ItemModifier { StringId = "fine" };
            var cracked = new ItemModifier { StringId = "cracked" };

            string fineKey = TradingEngine.GetTradeIdentityKey(new EquipmentElement(item, fine, null!, false));
            string crackedKey = TradingEngine.GetTradeIdentityKey(new EquipmentElement(item, cracked, null!, false));

            Assert.NotEqual(fineKey, crackedKey);
            Assert.Equal("flax::fine", fineKey);
        }

        [Fact]
        public void IsCommodityCandidate_AcceptsTradeGoodsAnimalsAndMounts()
        {
            var good = new ItemObject("wool");
            var animal = new ItemObject("hog");
            var mount = new ItemObject("horse");
            var weapon = new ItemObject("sword");

            SetPrivateField(good, "Type", ItemObject.ItemTypeEnum.Goods);

            var animalCategory = (ItemCategory)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ItemCategory));
            SetPrivateField(animalCategory, "<IsAnimal>k__BackingField", true);
            SetPrivateField(animal, "<ItemCategory>k__BackingField", animalCategory);

            var animalHorseComponent = (HorseComponent)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HorseComponent));
            SetPrivateField(animal, "<ItemComponent>k__BackingField", animalHorseComponent);

            var horseComponent = (HorseComponent)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HorseComponent));
            var monster = (Monster)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Monster));
            SetPrivateField(horseComponent, "<Monster>k__BackingField", monster);
            SetPrivateField(horseComponent, "<IsRideable>k__BackingField", true);
            SetPrivateField(mount, "<ItemComponent>k__BackingField", horseComponent);

            SetPrivateField(weapon, "Type", ItemObject.ItemTypeEnum.OneHandedWeapon);

            Assert.True(good.IsTradeGood, "good should be trade good");
            Assert.True(animal.IsAnimal, "animal should be animal");
            Assert.True(mount.IsMountable, "mount should be mountable");

            Assert.True(TradeCandidatePolicy.IsCommodityCandidate(good));
            Assert.True(TradeCandidatePolicy.IsCommodityCandidate(animal));
            Assert.True(TradeCandidatePolicy.IsCommodityCandidate(mount));
            Assert.False(TradeCandidatePolicy.IsCommodityCandidate(weapon));
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }
            var baseType = obj.GetType().BaseType;
            while (baseType != null)
            {
                field = baseType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
