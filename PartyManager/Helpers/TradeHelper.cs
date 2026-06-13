using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using System.Reflection;
using SettlementAutomationCore;

namespace PartyManager.Helpers
{
    public static class TradeHelper
    {
        private class RosterElementInfo
        {
            public EquipmentElement EqElement { get; }
            public int Amount { get; }
            public int Price { get; }
            public RosterElementInfo(EquipmentElement eqElement, int amount, int price)
            {
                EqElement = eqElement;
                Amount = amount;
                Price = price;
            }
        }

        public static List<TradeOrder> GetPreSellOrders(MobileParty party, Settings settings)
        {
            var orders = new List<TradeOrder>();
            if (settings == null || !settings.PreventHerdingPenalty) return orders;

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                out var ridingItems, out var packItems, out var livestockItems);

            int totalAnimals = riding + pack + livestock;
            int maxAllowed = (infantry * 2) + (cavalry * 1);

            if (totalAnimals > maxAllowed)
            {
                int excess = totalAnimals - maxAllowed;
                bool slaughter = settings.SlaughterAnimalsForHerding;

                // 1. Process Livestock first
                foreach (var el in livestockItems)
                {
                    if (excess <= 0) break;
                    int toProcess = Math.Min(excess, el.Amount);
                    orders.Add(new TradeOrder(el.EquipmentElement, toProcess, false, slaughter));
                    excess -= toProcess;
                }

                // 2. Process Riding Mounts (respecting SellRidingMountsSetting)
                var ridingMode = settings.SellRidingMountsSetting;
                if (excess > 0 && ridingMode != SellRidingMountsMode.Never)
                {
                    int excessRiding = 0;
                    if (ridingMode == SellRidingMountsMode.ExcessOnly)
                    {
                        excessRiding = Math.Max(0, riding - infantry);
                    }
                    else if (ridingMode == SellRidingMountsMode.All)
                    {
                        excessRiding = riding;
                    }

                    if (excessRiding > 0)
                    {
                        foreach (var el in ridingItems)
                        {
                            if (excess <= 0 || excessRiding <= 0) break;
                            int available = el.Amount;
                            int toSell = Math.Min(Math.Min(excess, excessRiding), available);
                            orders.Add(new TradeOrder(el.EquipmentElement, toSell, false, false));
                            excess -= toSell;
                            excessRiding -= toSell;
                        }
                    }
                }

                // 3. Process Pack Animals if still over herding limit
                if (excess > 0)
                {
                    foreach (var el in packItems)
                    {
                        if (excess <= 0) break;
                        int toProcess = Math.Min(excess, el.Amount);
                        orders.Add(new TradeOrder(el.EquipmentElement, toProcess, false, slaughter));
                        excess -= toProcess;
                    }
                }
            }

            return orders;
        }

        public static void SubmitAutomationRequests(MobileParty party, Settings settings)
        {
            if (settings == null || party == null || Hero.MainHero == null) return;

            // 1. Gather settings from TradeOptimizer using reflection to avoid direct DLL dependency
            bool optAutoBuyFood = true;
            int optFoodDays = 10;
            int optMinSizeVariety = 20;
            int optMinGoldVariety = 3000;
            bool optAutoBuyMounts = true;
            int optMinGoldMounts = 10000;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "TradeOptimizer")
                    {
                        var settingsType = assembly.GetType("TradeOptimizer.Settings");
                        if (settingsType != null)
                        {
                            var instanceProp = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            var settingsInstance = instanceProp?.GetValue(null);
                            if (settingsInstance != null)
                            {
                                optAutoBuyFood = (bool)(settingsType.GetProperty("AutoBuyFood")?.GetValue(settingsInstance) ?? true);
                                optFoodDays = (int)(settingsType.GetProperty("PartyFoodDaysToKeep")?.GetValue(settingsInstance) ?? 10);
                                optMinSizeVariety = (int)(settingsType.GetProperty("MinPartySizeForVariety")?.GetValue(settingsInstance) ?? 20);
                                optMinGoldVariety = (int)(settingsType.GetProperty("MinGoldForVariety")?.GetValue(settingsInstance) ?? 3000);
                                optAutoBuyMounts = (bool)(settingsType.GetProperty("AutoBuyMounts")?.GetValue(settingsInstance) ?? true);
                                optMinGoldMounts = (int)(settingsType.GetProperty("MinGoldForMounts")?.GetValue(settingsInstance) ?? 10000);
                            }
                        }
                        break;
                    }
                }
            }
            catch {}

            int partySize = party.MemberRoster.TotalManCount;
            if (partySize <= 0) return;

            // 2. Submit Tiered Food Requests
            if (optAutoBuyFood)
            {
                // Tier 1: Starvation/Survival (Keep 2 days of food at high priority)
                int survivalTarget = (int)Math.Ceiling(partySize * 2 / 20.0f); // 20 man-days per unit of food
                int dailyWage = party.TotalWage;
                int survivalMinGold = Math.Max(1000, dailyWage * 2);
                AutomationRegistry.RegisterRequest(new AutomationRequest(
                    "PartyManager",
                    RequestType.ItemCategory,
                    "Food",
                    survivalTarget,
                    95, // Priority: Emergency
                    survivalMinGold,
                    3.0f // Max Price: Pay any price
                ));

                // Tier 2: Stability (Keep 5 days of food at high/medium priority)
                int stabilityTarget = (int)Math.Ceiling(partySize * 5 / 20.0f);
                AutomationRegistry.RegisterRequest(new AutomationRequest(
                    "PartyManager",
                    RequestType.ItemCategory,
                    "Food",
                    stabilityTarget,
                    70, // Priority: High
                    2000,
                    1.5f
                ));

                // Tier 3: Full Buffer & Variety (Keep optFoodDays of food variety)
                if (partySize >= optMinSizeVariety)
                {
                    int varietyTarget = (int)Math.Ceiling(partySize * optFoodDays / 20.0f);
                    AutomationRegistry.RegisterRequest(new AutomationRequest(
                        "PartyManager",
                        RequestType.ItemCategory,
                        "Food",
                        varietyTarget,
                        35, // Priority: Low
                        optMinGoldVariety,
                        1.1f
                    ));
                }
            }

            // 3. Submit Mount Requests
            if (optAutoBuyMounts)
            {
                AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out int riding, out _, out _, out _, out _, out _);
                int missing = infantry - riding;
                if (missing > 0)
                {
                    // Scale priority by percentage of infantry that is currently unmounted
                    int scalePriority = (int)(30 + 50 * ((float)missing / infantry));
                    AutomationRegistry.RegisterRequest(new AutomationRequest(
                        "PartyManager",
                        RequestType.ItemCategory,
                        "Horse",
                        infantry, // Cumulative target is to reach fully mounted infantry
                        scalePriority,
                        optMinGoldMounts,
                        1.2f
                    ));
                }
            }

            // 4. Submit Prioritized Recruit Requests
            if (settings.AutoRecruitVolunteers)
            {
                int currentSize = party.MemberRoster.TotalManCount;
                int limit = party.Party.PartySizeLimit;
                if (currentSize < limit)
                {
                    bool wantsCavalry = settings.RecruitMeleeCavalry || settings.RecruitHorseArchers;
                    bool wantsRanged = settings.RecruitFootArchers || settings.RecruitCrossbowmen || settings.RecruitHorseArchers;
                    bool wantsMelee = settings.RecruitShieldInfantry || settings.RecruitShockInfantry || settings.RecruitSkirmishers;

                    string recruitFilter = "AnyRecruit";
                    if (wantsCavalry && !wantsRanged && !wantsMelee)
                        recruitFilter = "MeleeCavalry";
                    else if (wantsRanged && !wantsCavalry && !wantsMelee)
                        recruitFilter = "Ranged";
                    else if (wantsMelee && !wantsCavalry && !wantsRanged)
                        recruitFilter = "Melee";

                    // Tier 1: Emergency Hires (fill up to 30% capacity if extremely low)
                    int emergencyTarget = (int)(limit * 0.3f);
                    if (currentSize < emergencyTarget)
                    {
                        AutomationRegistry.RegisterRequest(new AutomationRequest(
                            "PartyManager",
                            RequestType.TroopFilter,
                            "AnyRecruit", // Hire anyone in an emergency
                            emergencyTarget,
                            90, // Priority: Emergency
                            500,
                            1.0f
                        ));
                    }

                    // Tier 2: Standard Recruit Hires (fill up to 80% capacity with filtered recruits)
                    int normalTarget = (int)(limit * 0.8f);
                    AutomationRegistry.RegisterRequest(new AutomationRequest(
                        "PartyManager",
                        RequestType.TroopFilter,
                        recruitFilter,
                        normalTarget,
                        60, // Priority: Medium-High
                        1000,
                        1.0f
                    ));

                    // Tier 3: Optimization Fill (fill up to 100% capacity with filtered recruits)
                    AutomationRegistry.RegisterRequest(new AutomationRequest(
                        "PartyManager",
                        RequestType.TroopFilter,
                        recruitFilter,
                        limit,
                        30, // Priority: Low
                        5000,
                        1.0f
                    ));
                }
            }
        }
    }
}
