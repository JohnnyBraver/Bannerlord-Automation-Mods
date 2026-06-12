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
    public class EquipmentManagerProvider : ITradeOrderProvider
    {
        public string ProviderName => "EquipmentManager";

        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            return new List<TradeOrder>();
        }

        public List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic)
        {
            var orders = new List<TradeOrder>();
            if (!Settings.Instance.SellUnlockedEquipment) return orders;
            if (Settings.Instance.PreventEquipmentSaleInVillages && settlement.IsVillage) return orders;

            var tracker = Campaign.Current?.GetCampaignBehavior<IViewDataTracker>();
            var locks = new HashSet<string>(tracker?.GetInventoryLocks() ?? Enumerable.Empty<string>());

            bool hasWeaponPerk = Hero.MainHero?.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.PaidInPromise) ?? false;
            bool hasArmorPerk = Hero.MainHero?.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.GivingHands) ?? false;

            if (!Enum.TryParse<ItemQuality>(Settings.Instance.MinQualityToKeep, true, out var minQuality))
            {
                minQuality = ItemQuality.Fine;
            }

            // Loop through player's inventory to find equipment to sell
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

                // Build lock key just like the VM does
                string key = item.StringId + (eqEl.ItemModifier != null ? eqEl.ItemModifier.StringId : "");
                if (locks.Contains(key)) continue;

                // Check optimization retention rules
                bool shouldLock = false;

                // A. Min Tier
                if ((int)item.Tier >= Settings.Instance.MinTierToKeep)
                {
                    shouldLock = true;
                }

                // B. Quality Modifiers
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
                    else if (Settings.Instance.KeepPositiveModifiers && modifier.PriceMultiplier > 1.0f)
                    {
                        shouldLock = true;
                    }
                }

                // C. Donation Efficiency
                if (!shouldLock)
                {
                    float sellPrice = currentLogic.GetItemPrice(eqEl, false);
                    float baseValue = item.Value;
                    float costPerXp = baseValue > 0 ? (sellPrice / baseValue) : 9999f;

                    if (costPerXp <= Settings.Instance.MaxCostPerXp)
                    {
                        if (item.HasArmorComponent && hasArmorPerk && Settings.Instance.LockDonationArmor)
                        {
                            shouldLock = true;
                        }
                        else if ((item.WeaponComponent != null || item.PrimaryWeapon != null) && hasWeaponPerk && Settings.Instance.LockDonationWeapons)
                        {
                            shouldLock = true;
                        }
                    }
                }

                if (!shouldLock)
                {
                    // Sell this equipment!
                    orders.Add(new TradeOrder(eqEl, rosterElement.Amount, false));
                }
            }

            if (Settings.Instance.PrioritizeHeavyTrash)
            {
                orders = orders.OrderByDescending(o => o.EquipmentElement.Item?.Weight ?? 0f).ToList();
            }

            return orders;
        }
    }
}
