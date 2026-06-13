using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TradingOptimizer
{
    public static class PricingService
    {
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
