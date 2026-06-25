using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TradeOptimizer
{
    public static class PricingService
    {
        private static PerkObject? _appraiserPerk;
        private static PerkObject? _wholeSellerPerk;
        private static PerkObject? _caravanMasterPerk;
        private static PerkObject? _marketDealerPerk;
        private static bool _perksInitialized;

        private static void InitializePerks()
        {
            if (_perksInitialized) return;
            try
            {
                _appraiserPerk = DefaultPerks.Trade.Appraiser;
                _wholeSellerPerk = DefaultPerks.Trade.WholeSeller;
                _caravanMasterPerk = DefaultPerks.Trade.CaravanMaster;
                _marketDealerPerk = DefaultPerks.Trade.MarketDealer;
            }
            catch
            {
                try
                {
                    var defaultPerksType = typeof(PerkObject).Assembly.GetType("TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks");
                    var tradeType = defaultPerksType?.GetNestedType("Trade", BindingFlags.Public | BindingFlags.NonPublic);
                    if (tradeType != null)
                    {
                        _appraiserPerk = tradeType.GetProperty("Appraiser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as PerkObject;
                        _wholeSellerPerk = tradeType.GetProperty("WholeSeller", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as PerkObject;
                        _caravanMasterPerk = tradeType.GetProperty("CaravanMaster", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as PerkObject;
                        _marketDealerPerk = tradeType.GetProperty("MarketDealer", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as PerkObject;
                    }
                }
                catch (Exception ex)
                {
                    TradingEngine.WriteLog($"[Perk Initialization Error] {ex.Message}");
                }
            }
            _perksInitialized = true;
        }

        public static bool HasTier1Perks()
        {
            if (Hero.MainHero == null) return false;
            InitializePerks();
            return (_appraiserPerk != null && Hero.MainHero.GetPerkValue(_appraiserPerk)) ||
                   (_wholeSellerPerk != null && Hero.MainHero.GetPerkValue(_wholeSellerPerk));
        }

        public static bool HasTier2Perks()
        {
            if (Hero.MainHero == null) return false;
            InitializePerks();
            return (_caravanMasterPerk != null && Hero.MainHero.GetPerkValue(_caravanMasterPerk)) ||
                   (_marketDealerPerk != null && Hero.MainHero.GetPerkValue(_marketDealerPerk));
        }

        public static float GetReferencePrice(
            InventoryLogic? logic,
            EquipmentElement eqElement,
            ItemRosterElement? rosterElement,
            float costBasis,
            bool isSelling)
        {
            var settings = Settings.Instance;
            if (settings == null) return eqElement.Item.Value;

            bool useCostBasis = false;
            if (isSelling)
            {
                var costBasisMode = settings.CostBasis;
                if (costBasisMode == CostBasisMode.Always)
                {
                    useCostBasis = true;
                }
                else if (costBasisMode == CostBasisMode.PerkBased)
                {
                    useCostBasis = HasTier1Perks();
                }
            }

            if (useCostBasis)
            {
                if (costBasis > 0f)
                {
                    return costBasis;
                }
                if (rosterElement.HasValue)
                {
                    float trackedPrice = GetTrackedAveragePrice(rosterElement.Value);
                    if (trackedPrice > 0f)
                    {
                        return trackedPrice;
                    }
                }
            }

            var refMode = settings.PricingReference;
            if (refMode == PricingReferenceMode.AlwaysGlobal)
            {
                return GetWorldAveragePrice(eqElement);
            }
            if (refMode == PricingReferenceMode.AlwaysLocal)
            {
                return GetLocalCategoryAveragePrice(logic, eqElement);
            }

            // PerkBased Pricing Reference Fallback
            bool hasTier1 = HasTier1Perks();
            bool hasTier2 = HasTier2Perks();

            if (hasTier1 && hasTier2)
            {
                return GetWorldAveragePrice(eqElement);
            }
            return GetLocalCategoryAveragePrice(logic, eqElement);
        }

        public static float GetWorldAveragePrice(EquipmentElement equipmentElement)
        {
            var towns = Town.AllTowns;
            if (towns == null || towns.Count == 0)
            {
                return equipmentElement.Item.Value;
            }

            float sumPrices = 0f;
            int count = 0;
            foreach (var town in towns)
            {
                if (town != null)
                {
                    sumPrices += town.GetItemPrice(equipmentElement, null, false);
                    count++;
                }
            }
            return count > 0 ? (sumPrices / count) : equipmentElement.Item.Value;
        }

        public static float GetLocalCategoryAveragePrice(InventoryLogic? logic, EquipmentElement eqElement)
        {
            var town = logic?.CurrentSettlementComponent as Town;
            if (town == null)
            {
                var village = logic?.CurrentSettlementComponent as Village;
                if (village != null && village.TradeBound != null)
                {
                    town = village.TradeBound.Town;
                }
            }
            if (town == null && MobileParty.MainParty != null)
            {
                var mainParty = MobileParty.MainParty;
                var nearestTownSettlement = Settlement.All
                    .Where(s => s.IsTown)
                    .OrderBy(s => s.GetPosition2D.DistanceSquared(mainParty.GetPosition2D))
                    .FirstOrDefault();
                town = nearestTownSettlement?.Town;
            }

            if (town != null)
            {
                float deviationRatio = Helpers.TownHelpers.CalculatePriceDeviationRatio(town, eqElement);
                int currentItemPrice = logic != null ? logic.GetItemPrice(eqElement, false) : 0;
                if (currentItemPrice > 0 && Math.Abs(1f + deviationRatio) > 0.01f)
                {
                    return currentItemPrice / (1f + deviationRatio);
                }
            }
            return eqElement.Item.Value;
        }

        public static float GetTrackedAveragePrice(ItemRosterElement rosterElement)
        {
            try
            {
                var behavior = Campaign.Current?.CampaignBehaviorManager?.GetBehaviors<CampaignBehaviorBase>()
                    ?.FirstOrDefault(b => b.GetType().FullName == "TaleWorlds.CampaignSystem.CampaignBehaviors.TradeSkillCampaignBehavior");
                if (behavior != null)
                {
                    var method = behavior.GetType().GetMethod("GetAveragePriceForItem", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        return (int)method.Invoke(behavior, new object[] { rosterElement });
                    }
                }
            }
            catch (Exception ex)
            {
                TradingEngine.WriteLog($"[Average Price Check Error] {ex.Message}");
            }
            return 0f;
        }
    }
}
