using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace EquipmentManager
{
    public static class EquipmentEngine
    {
        private static readonly FieldInfo? InventoryLogicField = typeof(SPInventoryVM)
            .GetField("_inventoryLogic", BindingFlags.Instance | BindingFlags.NonPublic);

        public static InventoryLogic? GetInventoryLogic(SPInventoryVM vm)
        {
            if (vm == null) return null;
            return InventoryLogicField?.GetValue(vm) as InventoryLogic;
        }

        public static void OptimizeEquipment(SPInventoryVM vm)
        {
            if (vm == null) return;

            var inventoryLogic = GetInventoryLogic(vm);
            if (inventoryLogic == null) return;

            // 1. Gather all heroes to process
            var heroesToProcess = new List<Hero>();
            if (Settings.Instance.AutoEquipCompanions)
            {
                try
                {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    dynamic characterList = vm.CharacterList;
                    if (characterList != null && characterList.ItemList != null)
                    {
                        foreach (dynamic item in characterList.ItemList)
                        {
                            Hero h = item.Hero;
                            if (h != null && !heroesToProcess.Contains(h))
                            {
                                heroesToProcess.Add(h);
                            }
                        }
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                catch (Exception)
                {
                    // Fallback
                }
            }

            if (heroesToProcess.Count == 0)
            {
                var mainHero = Hero.MainHero;
                if (mainHero != null)
                {
                    heroesToProcess.Add(mainHero);
                }
            }

            var notifications = new List<string>();

            // 2. Auto-Equip Phase
            var armorSlots = new EquipmentIndex[] { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
            var weaponSlots = new EquipmentIndex[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 };

            foreach (var hero in heroesToProcess)
            {
                if (hero.CharacterObject == null) continue;

                // Process both Combat and Civilian sets
                for (int setIndex = 0; setIndex < 2; setIndex++)
                {
                    bool isCivilian = setIndex == 1;
                    var equipment = isCivilian ? hero.CivilianEquipment : hero.BattleEquipment;
                    var targetSide = isCivilian ? InventoryLogic.InventorySide.CivilianEquipment : InventoryLogic.InventorySide.BattleEquipment;

                    // A. Armor Slots
                    if (Settings.Instance.AutoEquipArmor)
                    {
                        foreach (var slot in armorSlots)
                        {
                            var currentArmor = equipment[slot];

                            // Find best strict upgrade and best drawback candidate
                            ItemRosterElement? bestStrictUpgrade = null;
                            float bestStrictScore = -9999f;

                            ItemRosterElement? bestDrawback = null;
                            float bestDrawbackScore = -9999f;

                            var playerItems = inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                            foreach (var element in playerItems)
                            {
                                if (element.IsEmpty || element.Amount <= 0) continue;

                                var candidate = element.EquipmentElement;
                                var item = candidate.Item;
                                if (item == null || !item.HasArmorComponent) continue;

                                if (isCivilian && !item.IsCivilian) continue;
                                if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                                bool checkWeight = isCivilian && Settings.Instance.OptimizeCivilianForSneaking;
                                float penalty = Settings.Instance.SneakingWeightPenaltyFactor;

                                bool strictlyBeats = StrictlyBeatsArmor(candidate, currentArmor, checkWeight, penalty);
                                float score = GetArmorScore(candidate, checkWeight, penalty);

                                if (strictlyBeats)
                                {
                                    if (score > bestStrictScore)
                                    {
                                        bestStrictScore = score;
                                        bestStrictUpgrade = element;
                                    }
                                }
                                else
                                {
                                    float currentScore = GetArmorScore(currentArmor, checkWeight, penalty);
                                    if (score > currentScore && score > bestDrawbackScore)
                                    {
                                        bestDrawbackScore = score;
                                        bestDrawback = element;
                                    }
                                }
                            }

                            // Equip strict upgrade if found
                            if (bestStrictUpgrade.HasValue)
                            {
                                var upgradeElement = bestStrictUpgrade.Value;
                                var equipElement = new ItemRosterElement(upgradeElement.EquipmentElement, 1);
                                var command = TransferCommand.Transfer(1, InventoryLogic.InventorySide.PlayerInventory, targetSide, equipElement, EquipmentIndex.None, slot, hero.CharacterObject);
                                inventoryLogic.AddTransferCommand(command);

                                string slotName = GetSlotName(slot);
                                string setName = isCivilian ? "Civilian" : "Combat";
                                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Equipped {upgradeElement.EquipmentElement.Item.Name} on {hero.Name} in {slotName} slot ({setName} set).");

                                // If we successfully upgraded, update currentArmor for drawback comparison
                                currentArmor = upgradeElement.EquipmentElement;
                            }

                            // Check if a drawback candidate is still better than the newly equipped armor
                            if (bestDrawback.HasValue)
                            {
                                float currentScore = GetArmorScore(currentArmor, isCivilian && Settings.Instance.OptimizeCivilianForSneaking, Settings.Instance.SneakingWeightPenaltyFactor);
                                if (bestDrawbackScore > currentScore)
                                {
                                    string slotName = GetSlotName(slot);
                                    string setName = isCivilian ? "Civilian" : "Combat";
                                    notifications.Add($"{hero.Name} ({setName}): {bestDrawback.Value.EquipmentElement.Item.Name} in {slotName} slot is better but has drawbacks compared to {(currentArmor.IsEmpty ? "None" : currentArmor.Item.Name)}.");
                                }
                            }
                        }
                    }

                    // B. Weapon Slots (Only evaluate if slot is not empty)
                    if (Settings.Instance.AutoEquipWeapons)
                    {
                        foreach (var slot in weaponSlots)
                        {
                            var currentWeapon = equipment[slot];
                            if (currentWeapon.IsEmpty || currentWeapon.Item == null || currentWeapon.Item.PrimaryWeapon == null) continue;

                            ItemRosterElement? bestStrictUpgrade = null;
                            float bestStrictScore = -9999f;

                            ItemRosterElement? bestDrawback = null;
                            float bestDrawbackScore = -9999f;

                            var playerItems = inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                            foreach (var element in playerItems)
                            {
                                if (element.IsEmpty || element.Amount <= 0) continue;

                                var candidate = element.EquipmentElement;
                                var item = candidate.Item;
                                if (item == null || item.PrimaryWeapon == null) continue;

                                if (isCivilian && !item.IsCivilian) continue;
                                if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                                bool strictlyBeats = StrictlyBeatsWeapon(candidate, currentWeapon);
                                float score = GetWeaponScore(candidate);

                                if (strictlyBeats)
                                {
                                    if (score > bestStrictScore)
                                    {
                                        bestStrictScore = score;
                                        bestStrictUpgrade = element;
                                    }
                                }
                                else
                                {
                                    float currentScore = GetWeaponScore(currentWeapon);
                                    if (score > currentScore && score > bestDrawbackScore)
                                    {
                                        bestDrawbackScore = score;
                                        bestDrawback = element;
                                    }
                                }
                            }

                            // Equip strict upgrade if found
                            if (bestStrictUpgrade.HasValue)
                            {
                                var upgradeElement = bestStrictUpgrade.Value;
                                var equipElement = new ItemRosterElement(upgradeElement.EquipmentElement, 1);
                                var command = TransferCommand.Transfer(1, InventoryLogic.InventorySide.PlayerInventory, targetSide, equipElement, EquipmentIndex.None, slot, hero.CharacterObject);
                                inventoryLogic.AddTransferCommand(command);

                                string setName = isCivilian ? "Civilian" : "Combat";
                                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Equipped {upgradeElement.EquipmentElement.Item.Name} on {hero.Name} in Weapon slot {slot} ({setName} set).");

                                currentWeapon = upgradeElement.EquipmentElement;
                            }

                            // Check if drawback candidate is still better
                            if (bestDrawback.HasValue)
                            {
                                float currentScore = GetWeaponScore(currentWeapon);
                                if (bestDrawbackScore > currentScore)
                                {
                                    string setName = isCivilian ? "Civilian" : "Combat";
                                    notifications.Add($"{hero.Name} ({setName}): {bestDrawback.Value.EquipmentElement.Item.Name} in {slot} slot is better but has drawbacks compared to {currentWeapon.Item.Name}.");
                                }
                            }
                        }
                    }
                }
            }

            // 3. Display drawback notifications
            foreach (var note in notifications)
            {
                InformationManager.DisplayMessage(new InformationMessage(note, new Color(0.9f, 0.6f, 0.2f)));
            }

            // 4. Auto-Locking Phase
            if (vm.RightItemListVM != null)
            {
                bool hasWeaponPerk = Hero.MainHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise);
                bool hasArmorPerk = Hero.MainHero.GetPerkValue(DefaultPerks.Steward.GivingHands);

                if (!Enum.TryParse<ItemQuality>(Settings.Instance.MinQualityToKeep, true, out var minQuality))
                {
                    minQuality = ItemQuality.Fine;
                }

                foreach (var itemVM in vm.RightItemListVM)
                {
                    if (itemVM == null || itemVM.ItemRosterElement.IsEmpty) continue;

                    var eqEl = itemVM.ItemRosterElement.EquipmentElement;
                    var item = eqEl.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (!isEquipment) continue;

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
                        float sellPrice = itemVM.ItemCost;
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

                    if (shouldLock)
                    {
                        itemVM.IsLocked = true;
                        SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Locked item to keep: {item.Name} (Tier {item.Tier}, Quality: {modifier?.ItemQuality.ToString() ?? "Standard"})");
                    }
                }
            }

            // 5. Sell Phase
            var settlement = inventoryLogic.CurrentSettlementComponent?.Settlement;
            bool skipSell = settlement != null && Settings.Instance.PreventEquipmentSaleInVillages && settlement.IsVillage;

            if (Settings.Instance.SellUnlockedEquipment && vm.RightItemListVM != null && !skipSell)
            {
                var itemsToSell = new List<SPItemVM>();
                foreach (var itemVM in vm.RightItemListVM)
                {
                    if (itemVM == null || itemVM.ItemRosterElement.IsEmpty) continue;
                    if (itemVM.IsLocked) continue;

                    var item = itemVM.ItemRosterElement.EquipmentElement.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (isEquipment)
                    {
                        itemsToSell.Add(itemVM);
                    }
                }

                // Prioritize heavy items if enabled
                if (Settings.Instance.PrioritizeHeavyTrash)
                {
                    itemsToSell = itemsToSell.OrderByDescending(itemVM => itemVM.ItemRosterElement.EquipmentElement.Item.Weight).ToList();
                }

                var soldLogs = new List<string>();
                foreach (var itemVM in itemsToSell)
                {
                    try
                    {
                        int count = itemVM.ItemCount;
                        var item = itemVM.ItemRosterElement.EquipmentElement.Item;
                        if (item == null || count <= 0) continue;

                        // Check merchant gold limits
                        if (vm.IsOtherInventoryGoldRelevant)
                        {
                            int merchantGold = vm.LeftInventoryOwnerGold;
                            int currentDebt = inventoryLogic.TotalAmount; // positive means merchant owes us (gold we gained)
                            int remainingGold = merchantGold - currentDebt;

                            if (remainingGold <= 0)
                            {
                                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Stopped selling equipment: Merchant has no gold left ({remainingGold} denars).");
                                break;
                            }

                            int price = inventoryLogic.GetItemPrice(itemVM.ItemRosterElement.EquipmentElement, false);
                            if (price > 0)
                            {
                                int maxAffordable = remainingGold / price;
                                if (maxAffordable <= 0)
                                {
                                    // Sell at most 1 to take the last few coins, or skip
                                    if (remainingGold > 0 && remainingGold >= price / 2) // if they have at least half the price
                                    {
                                        count = 1;
                                    }
                                    else
                                    {
                                        continue; // skip this item, try to find a cheaper one or stop
                                    }
                                }
                                else
                                {
                                    count = Math.Min(count, maxAffordable);
                                }
                            }
                        }

                        var command = TransferCommand.Transfer(
                            count,
                            InventoryLogic.InventorySide.PlayerInventory,
                            InventoryLogic.InventorySide.OtherInventory,
                            new ItemRosterElement(itemVM.ItemRosterElement.EquipmentElement, count),
                            EquipmentIndex.None,
                            EquipmentIndex.None,
                            Hero.MainHero.CharacterObject
                        );
                        inventoryLogic.AddTransferCommand(command);
                        soldLogs.Add($"{count}x {item.Name}");
                    }
                    catch (Exception ex)
                    {
                        SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error selling {itemVM.ItemRosterElement.EquipmentElement.Item?.Name}: {ex.Message}");
                    }
                }
                if (soldLogs.Count > 0)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Sold unlocked equipment: {string.Join(", ", soldLogs)}");
                }
            }

            // 6. Refresh UI
            vm.RefreshValues();
        }

        private static bool StrictlyBeatsArmor(EquipmentElement candidate, EquipmentElement current, bool checkWeight, float penaltyFactor)
        {
            if (current.IsEmpty) return true;

            int headC = candidate.GetModifiedHeadArmor();
            int bodyC = candidate.GetModifiedBodyArmor();
            int legC = candidate.GetModifiedLegArmor();
            int armC = candidate.GetModifiedArmArmor();

            int headE = current.GetModifiedHeadArmor();
            int bodyE = current.GetModifiedBodyArmor();
            int legE = current.GetModifiedLegArmor();
            int armE = current.GetModifiedArmArmor();

            bool protectionEqualOrBetter = headC >= headE && bodyC >= bodyE && legC >= legE && armC >= armE;
            bool protectionStrictlyBetter = headC > headE || bodyC > bodyE || legC > legE || armC > armE;

            if (checkWeight)
            {
                float weightC = candidate.GetEquipmentElementWeight();
                float weightE = current.GetEquipmentElementWeight();

                bool weightEqualOrBetter = weightC <= weightE;
                bool weightStrictlyBetter = weightC < weightE;

                return protectionEqualOrBetter && weightEqualOrBetter && (protectionStrictlyBetter || weightStrictlyBetter);
            }
            else
            {
                return protectionEqualOrBetter && protectionStrictlyBetter;
            }
        }

        private static float GetArmorScore(EquipmentElement eqEl, bool checkWeight, float penaltyFactor)
        {
            if (eqEl.IsEmpty) return -9999f;
            float armorSum = eqEl.GetModifiedHeadArmor() + eqEl.GetModifiedBodyArmor() + eqEl.GetModifiedLegArmor() + eqEl.GetModifiedArmArmor();
            if (checkWeight)
            {
                return armorSum - eqEl.GetEquipmentElementWeight() * penaltyFactor;
            }
            return armorSum;
        }

        private static bool StrictlyBeatsWeapon(EquipmentElement candidate, EquipmentElement current)
        {
            if (current.IsEmpty || candidate.IsEmpty) return false;

            var wC = candidate.Item?.PrimaryWeapon;
            var wE = current.Item?.PrimaryWeapon;
            if (wC == null || wE == null) return false;

            if (wC.WeaponClass != wE.WeaponClass) return false;

            bool speedC = wC.ThrustSpeed >= wE.ThrustSpeed && wC.SwingSpeed >= wE.SwingSpeed && wC.MissileSpeed >= wE.MissileSpeed;
            bool damageC = wC.ThrustDamage >= wE.ThrustDamage && wC.SwingDamage >= wE.SwingDamage && wC.MissileDamage >= wE.MissileDamage;
            bool lengthC = wC.WeaponLength >= wE.WeaponLength;
            bool handlingC = wC.Handling >= wE.Handling;
            bool accuracyC = wC.Accuracy >= wE.Accuracy;
            bool durabilityC = wC.MaxDataValue >= wE.MaxDataValue;

            bool statsEqualOrBetter = speedC && damageC && lengthC && handlingC && accuracyC && durabilityC;

            bool speedS = wC.ThrustSpeed > wE.ThrustSpeed || wC.SwingSpeed > wE.SwingSpeed || wC.MissileSpeed > wE.MissileSpeed;
            bool damageS = wC.ThrustDamage > wE.ThrustDamage || wC.SwingDamage > wE.SwingDamage || wC.MissileDamage > wE.MissileDamage;
            bool lengthS = wC.WeaponLength > wE.WeaponLength;
            bool handlingS = wC.Handling > wE.Handling;
            bool accuracyS = wC.Accuracy > wE.Accuracy;
            bool durabilityS = wC.MaxDataValue > wE.MaxDataValue;

            bool statsStrictlyBetter = speedS || damageS || lengthS || handlingS || accuracyS || durabilityS;

            if (!statsEqualOrBetter || !statsStrictlyBetter) return false;

            var flagsC = wC.WeaponFlags;
            var flagsE = wE.WeaponFlags;
            if ((flagsC & flagsE) != flagsE) return false;

            return true;
        }

        private static float GetWeaponScore(EquipmentElement eqEl)
        {
            if (eqEl.IsEmpty) return -9999f;
            var w = eqEl.Item?.PrimaryWeapon;
            if (w == null) return -9999f;

            if (w.IsMeleeWeapon)
            {
                return w.ThrustDamage * w.ThrustSpeed * 0.01f + w.SwingDamage * w.SwingSpeed * 0.01f + w.Handling * 10f + w.WeaponLength;
            }
            else if (w.IsRangedWeapon)
            {
                return w.MissileDamage * w.MissileSpeed * 0.01f + w.Accuracy * 10f;
            }
            else if (w.IsShield || w.IsAmmo)
            {
                return w.MaxDataValue;
            }
            return 0f;
        }

        private static string GetSlotName(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Head: return "Head";
                case EquipmentIndex.Body: return "Torso";
                case EquipmentIndex.Leg: return "Legs";
                case EquipmentIndex.Gloves: return "Gloves";
                case EquipmentIndex.Cape: return "Cape/Shoulders";
                default: return slot.ToString();
            }
        }
    }
}
