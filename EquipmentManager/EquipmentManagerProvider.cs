using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;

namespace EquipmentManager
{
    public class EquipmentManagerProvider : IPreSellProvider, IAutomationRequestProvider
    {
        private struct PotentialBuyOrder
        {
            public InventoryItemView Candidate;
            public int Price;
            public int MinGoldReserve;
            public float ScoreIncrease;
            public bool PrioritizeStealth;
            public Hero Hero;
            public EquipmentIndex Slot;
        }

        public string ProviderName => "EquipmentManager";

        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<TradeOrder>();
            if (party == null || settlement == null) return orders;

            try
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"[Pre-Transaction] Securing existing equipment upgrades in {settlement.Name} before trade phase starts.");
                EquipmentEngine.AutoEquipHeadless(party, "Pre-Transaction");
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in GetPreSellOrders AutoEquipHeadless: {ex}");
            }

            var settings = Settings.Instance;
            if (settings == null || !settings.SellUnlockedEquipment) return orders;
            if (settings.PreventEquipmentSaleInVillages && settlement.IsVillage) return orders;

            var currentLogic = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement, true);
            if (currentLogic == null) return orders;

            try
            {
                var tracker = Campaign.Current?.GetCampaignBehavior<IViewDataTracker>();
                var locks = new HashSet<string>(tracker?.GetInventoryLocks() ?? Enumerable.Empty<string>());

                bool hasWeaponPerk = Hero.MainHero?.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.PaidInPromise) ?? false;
                bool hasArmorPerk = Hero.MainHero?.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.GivingHands) ?? false;

                if (!Enum.TryParse<ItemQuality>(settings.MinQualityToKeep, true, out var minQuality))
                {
                    minQuality = ItemQuality.Fine;
                }

                var targets = new List<Hero>();
                if (Hero.MainHero != null)
                {
                    targets.Add(Hero.MainHero);
                }
                if (settings.BuyEquipmentTargetSetting == BuyEquipmentTarget.PlayerAndCompanions && party != null)
                {
                    foreach (var member in party.MemberRoster.GetTroopRoster())
                    {
                        if (member.Character.IsHero && member.Character.HeroObject != null && member.Character.HeroObject != Hero.MainHero)
                        {
                            targets.Add(member.Character.HeroObject);
                        }
                    }
                }

                var playerElements = currentLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                for (int i = 0; i < playerElements.Count; i++)
                {
                    var rosterElement = playerElements[i];
                    if (rosterElement.IsEmpty || rosterElement.Amount <= 0) continue;

                    var eqEl = rosterElement.EquipmentElement;
                    var item = eqEl.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (!isEquipment) continue;

                    string key = item.StringId + (eqEl.ItemModifier != null ? eqEl.ItemModifier.StringId : "");
                    if (locks.Contains(key)) continue;

                    bool shouldLock = false;

                    if ((int)item.Tier >= settings.MinTierToKeep)
                    {
                        shouldLock = true;
                    }

                    var modifier = eqEl.ItemModifier;
                    if (modifier != null)
                    {
                        if (modifier.ItemQuality == ItemQuality.Legendary)
                        {
                            shouldLock = true;
                        }
                        else if (modifier.ItemQuality >= minQuality)
                        {
                            shouldLock = true;
                        }
                        else if (settings.KeepPositiveModifiers && modifier.PriceMultiplier > 1.0f)
                        {
                            shouldLock = true;
                        }
                    }

                    if (!shouldLock)
                    {
                        float sellPrice = currentLogic.GetItemPrice(eqEl, false);
                        float baseValue = item.Value;
                        float costPerXp = baseValue > 0 ? (sellPrice / baseValue) : 9999f;

                        if (costPerXp <= settings.MaxCostPerXp)
                        {
                            if (item.HasArmorComponent && hasArmorPerk && 
                                (settings.LockDonationCategorySetting == LockDonationCategory.ArmorOnly || 
                                 settings.LockDonationCategorySetting == LockDonationCategory.WeaponsAndArmor))
                            {
                                shouldLock = true;
                            }
                            else if ((item.WeaponComponent != null || item.PrimaryWeapon != null) && hasWeaponPerk && 
                                     (settings.LockDonationCategorySetting == LockDonationCategory.WeaponsOnly || 
                                      settings.LockDonationCategorySetting == LockDonationCategory.WeaponsAndArmor))
                            {
                                shouldLock = true;
                            }
                        }
                    }

                    if (!shouldLock && item.HasArmorComponent)
                    {
                        if (IsUpgradeForAnyTarget(eqEl, targets, settings))
                        {
                            shouldLock = true;
                            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"[Lock Rules] Preserving unequipped upgrade/side-grade in inventory: {item.Name} (Tier {item.Tier}, Armor Score: {GetArmorScore(eqEl):F1})");
                        }
                    }

                    if (!shouldLock)
                    {
                        orders.Add(new TradeOrder(eqEl, rosterElement.Amount, false));
                    }
                }

                if (settings.PrioritizeHeavyTrash)
                {
                    orders = orders.OrderByDescending(o =>
                    {
                        var item = o.EquipmentElement.Item;
                        float price = currentLogic.GetItemPrice(o.EquipmentElement, false);
                        return (float)(item?.Weight ?? 0f) / (price > 0f ? price : 1f);
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error constructing pre-sell orders: {ex}");
            }

            return orders;
        }

        // IAutomationRequestProvider: scan Core's merchant armor view and request the best exact upgrade.
        public void SubmitAutomationRequests(AutomationRequestContext context)
        {
            var settings = Settings.Instance;
            if (settings == null || context == null) return;

            var party = context.Party;
            var targets = new List<Hero>();
            if (Hero.MainHero != null)
            {
                targets.Add(Hero.MainHero);
            }
            if (settings.BuyEquipmentTargetSetting == BuyEquipmentTarget.PlayerAndCompanions && party != null)
            {
                foreach (var member in party.MemberRoster.GetTroopRoster())
                {
                    if (member.Character.IsHero && member.Character.HeroObject != null && member.Character.HeroObject != Hero.MainHero)
                    {
                        targets.Add(member.Character.HeroObject);
                    }
                }
            }

            if (!settings.BuyStealthGear && !settings.BuyTopArmor) return;

            int playerGold = Hero.MainHero?.Gold ?? 0;
            bool canBuyStealth = settings.BuyStealthGear;
            bool canBuyTopArmor = settings.BuyTopArmor && playerGold >= settings.BuyTopArmorGoldThreshold;
            if (!canBuyStealth && !canBuyTopArmor) return;

            var armorSlots = new EquipmentIndex[] { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
            var potentialOrders = new List<PotentialBuyOrder>();

            foreach (var hero in targets)
            {
                if (hero.CharacterObject == null) continue;

                for (int setIndex = 0; setIndex < 3; setIndex++)
                {
                    Equipment equipment;
                    bool prioritizeStealth = false;
                    InventoryLogic.InventorySide side;

                    if (setIndex == 0)
                    {
                        equipment = hero.BattleEquipment;
                        side = InventoryLogic.InventorySide.BattleEquipment;
                    }
                    else if (setIndex == 1)
                    {
                        equipment = hero.CivilianEquipment;
                        side = InventoryLogic.InventorySide.CivilianEquipment;
                    }
                    else
                    {
                        equipment = hero.StealthEquipment;
                        prioritizeStealth = true;
                        side = InventoryLogic.InventorySide.StealthEquipment;
                    }

                    foreach (var slot in armorSlots)
                    {
                        var currentArmor = equipment[slot];
                        InventoryItemView? bestCandidate = null;
                        float currentEquippedScore = prioritizeStealth ? GetStealthScore(currentArmor) : GetArmorScore(currentArmor);
                        float currentInventoryScore = GetBestScoreInInventory(context.PlayerInventory, slot, prioritizeStealth);
                        float bestScore = Math.Max(currentEquippedScore, currentInventoryScore);
                        bool foundUpgrade = false;

                        for (int i = 0; i < context.MerchantInventory.Armor.Count; i++)
                        {
                            var marketItem = context.MerchantInventory.Armor[i];
                            if (marketItem.Quantity <= 0) continue;

                            var candidate = marketItem.EquipmentElement;
                            var item = marketItem.Item;

                            if (side == InventoryLogic.InventorySide.CivilianEquipment && !item.IsCivilian) continue;
                            if (side == InventoryLogic.InventorySide.StealthEquipment && !item.IsStealthItem) continue;
                            if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                            if (prioritizeStealth)
                            {
                                if (!item.IsStealthItem) continue;

                                float score = GetStealthScore(candidate);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestCandidate = marketItem;
                                    foundUpgrade = true;
                                }
                            }
                            else
                            {
                                if (!canBuyTopArmor) continue;
                                if ((int)item.Tier < settings.MinTierToBuyTopArmor) continue;

                                float score = GetArmorScore(candidate);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestCandidate = marketItem;
                                    foundUpgrade = true;
                                }
                            }
                        }

                        if (foundUpgrade && bestCandidate != null)
                        {
                            int requiredReserve = prioritizeStealth ? settings.MinimumGoldReserve : settings.BuyTopArmorGoldThreshold;
                            float currentScore = prioritizeStealth ? GetStealthScore(currentArmor) : GetArmorScore(currentArmor);
                            float scoreIncrease = bestScore - currentScore;

                            potentialOrders.Add(new PotentialBuyOrder
                            {
                                Candidate = bestCandidate,
                                Price = bestCandidate.UnitPrice,
                                MinGoldReserve = requiredReserve,
                                ScoreIncrease = scoreIncrease,
                                PrioritizeStealth = prioritizeStealth,
                                Hero = hero,
                                Slot = slot
                            });
                        }
                    }
                }
            }

            // Select and request only the single best upgrade across all slots/heroes (Limit 1 per town)
            if (potentialOrders.Count > 0)
            {
                var bestOrder = potentialOrders.OrderByDescending(o => o.ScoreIncrease).First();
                AutomationRegistry.RegisterRequest(AutomationRequest.ForMarketItem(
                    "EquipmentManager",
                    bestOrder.Candidate,
                    1,
                    5,
                    bestOrder.MinGoldReserve));
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Requested Auto-Buy (Limit 1 per town): {bestOrder.Candidate.Item.Name} as upgrade for {bestOrder.Hero.Name} ({bestOrder.Slot} slot). Price seen: {bestOrder.Price} denars. Score Increase: {bestOrder.ScoreIncrease:F1}");
            }
        }

        private static float GetBestScoreInInventory(CategorizedInventoryView playerInventory, EquipmentIndex slot, bool prioritizeStealth)
        {
            float bestScore = -9999f;
            var playerElements = playerInventory.Armor;
            for (int i = 0; i < playerElements.Count; i++)
            {
                var rosterElement = playerElements[i];
                if (rosterElement.Quantity <= 0) continue;

                var eqEl = rosterElement.EquipmentElement;
                var item = rosterElement.Item;

                if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                if (prioritizeStealth)
                {
                    if (!item.IsStealthItem) continue;

                    float score = GetStealthScore(eqEl);
                    if (score > bestScore)
                    {
                        bestScore = score;
                    }
                }
                else
                {
                    float score = GetArmorScore(eqEl);
                    if (score > bestScore)
                    {
                        bestScore = score;
                    }
                }
            }
            return bestScore;
        }

        public static bool IsUpgradeForAnyTarget(EquipmentElement eqEl, List<Hero> targets, Settings settings)
        {
            var item = eqEl.Item;
            if (item == null || !item.HasArmorComponent) return false;

            var armorSlots = new EquipmentIndex[] { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
            
            foreach (var hero in targets)
            {
                if (hero.CharacterObject == null) continue;

                for (int setIndex = 0; setIndex < 3; setIndex++)
                {
                    Equipment equipment;
                    bool prioritizeStealth = false;

                    if (setIndex == 0)
                    {
                        equipment = hero.BattleEquipment;
                    }
                    else if (setIndex == 1)
                    {
                        if (!item.IsCivilian) continue;
                        equipment = hero.CivilianEquipment;
                    }
                    else
                    {
                        if (!item.IsStealthItem) continue;
                        equipment = hero.StealthEquipment;
                        prioritizeStealth = true;
                    }

                    foreach (var slot in armorSlots)
                    {
                        if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                        var currentArmor = equipment[slot];
                        float currentScore = prioritizeStealth ? GetStealthScore(currentArmor) : GetArmorScore(currentArmor);
                        
                        if (prioritizeStealth)
                        {
                            float candidateScore = GetStealthScore(eqEl);
                            if (candidateScore > currentScore) return true;
                        }
                        else
                        {
                            float candidateScore = GetArmorScore(eqEl);
                            if (candidateScore > currentScore) return true;
                        }
                    }
                }
            }

            return false;
        }

        private static float GetArmorScore(EquipmentElement eqEl)
        {
            if (eqEl.IsEmpty) return 0f;
            return eqEl.GetModifiedHeadArmor() + eqEl.GetModifiedBodyArmor() + eqEl.GetModifiedLegArmor() + eqEl.GetModifiedArmArmor();
        }

        private static float GetStealthScore(EquipmentElement eqEl)
        {
            if (eqEl.IsEmpty) return 0f;
            int stealthFactor = (eqEl.Item != null && eqEl.Item.ArmorComponent != null) ? eqEl.Item.ArmorComponent.StealthFactor : 0;
            return stealthFactor * 1000f + GetArmorScore(eqEl);
        }
    }
}
