using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace EquipmentManager
{
    public static class EquipmentEngine
    {
        private struct EquipTarget
        {
            public Hero Hero;
            public InventoryLogic.InventorySide Side;
            public bool PrioritizeStealth;
        }

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

            var settings = Settings.Instance;
            if (settings == null) return;

            var inventoryLogic = GetInventoryLogic(vm);
            if (inventoryLogic == null) return;

            int equippedCount = 0;
            int totalSold = 0;

            // 1. Gather all heroes to process
            var heroesToProcess = new List<Hero>();
            if (settings.AutoEquipCompanions)
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

            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"=== Equipment Optimization Run started for: {string.Join(", ", heroesToProcess.Select(h => h.Name.ToString()))} ===");

            var notifications = new List<string>();

            // 2. Auto-Equip Phase
            equippedCount = EvaluateAndEquip(inventoryLogic, heroesToProcess, settings, notifications);

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

                if (!Enum.TryParse<ItemQuality>(settings.MinQualityToKeep, true, out var minQuality))
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
                    if ((int)item.Tier >= settings.MinTierToKeep)
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
                        else if (settings.KeepPositiveModifiers && modifier.PriceMultiplier > 1.0f)
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
                    // D. Prevent selling unequipped upgrades / side-grades
                    if (!shouldLock && item.HasArmorComponent)
                    {
                        if (EquipmentManagerProvider.IsUpgradeForAnyTarget(eqEl, heroesToProcess, settings))
                        {
                            shouldLock = true;
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
            bool skipSell = settlement != null && settings.PreventEquipmentSaleInVillages && settlement.IsVillage;

            if (settings.SellUnlockedEquipment && vm.RightItemListVM != null && !skipSell)
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

                // Prioritize heavy items if enabled (using weight/price ratio descending)
                if (settings.PrioritizeHeavyTrash)
                {
                    itemsToSell = itemsToSell.OrderByDescending(itemVM =>
                    {
                        var item = itemVM.ItemRosterElement.EquipmentElement.Item;
                        int price = inventoryLogic.GetItemPrice(itemVM.ItemRosterElement.EquipmentElement, false);
                        return (float)item.Weight / (price > 0 ? price : 1);
                    }).ToList();
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
                        totalSold += count;
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
            vm.ExecuteRemoveZeroCounts();
            vm.RefreshValues();
            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"=== Equipment Optimization Run completed (Total equipped: {equippedCount}, Total sold: {totalSold}) ===");
        }

        private static bool StrictlyBeatsArmor(EquipmentElement candidate, EquipmentElement current, bool prioritizeStealth)
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

            if (prioritizeStealth)
            {
                int stealthC = (candidate.Item != null && candidate.Item.ArmorComponent != null) ? candidate.Item.ArmorComponent.StealthFactor : 0;
                int stealthE = (current.Item != null && current.Item.ArmorComponent != null) ? current.Item.ArmorComponent.StealthFactor : 0;

                if (stealthC > stealthE) return true;
                if (stealthC < stealthE) return false;

                return protectionEqualOrBetter && protectionStrictlyBetter;
            }
            else
            {
                return protectionEqualOrBetter && protectionStrictlyBetter;
            }
        }

        private static float GetArmorScore(EquipmentElement eqEl, bool prioritizeStealth)
        {
            if (eqEl.IsEmpty) return -9999f;
            float armorSum = eqEl.GetModifiedHeadArmor() + eqEl.GetModifiedBodyArmor() + eqEl.GetModifiedLegArmor() + eqEl.GetModifiedArmArmor();
            if (prioritizeStealth)
            {
                int stealthFactor = (eqEl.Item != null && eqEl.Item.ArmorComponent != null) ? eqEl.Item.ArmorComponent.StealthFactor : 0;
                return stealthFactor * 1000f + armorSum;
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

        public static InventoryLogic? CreateHeadlessInventoryLogic(MobileParty party)
        {
            if (party == null || Hero.MainHero == null) return null;
            try
            {
                var logic = new InventoryLogic(party, Hero.MainHero.CharacterObject, null);

                var initMethod = typeof(InventoryLogic).GetMethods()
                    .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 13);

                if (initMethod == null) return null;

                var categoryTypeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryCategoryType");
                var modeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryMode");
                if (categoryTypeEnum == null || modeEnum == null) return null;

                var categoryTypeAll = Enum.Parse(categoryTypeEnum, "All");
                var modeDefault = Enum.Parse(modeEnum, "Default");

                initMethod.Invoke(logic, new object[] {
                    null!, // leftItemRoster
                    party.ItemRoster,
                    party.MemberRoster,
                    false, // isTrading
                    false, // isSpecialActionsPermitted
                    Hero.MainHero.CharacterObject,
                    categoryTypeAll,
                    null!, // marketData
                    true, // useBasePrices
                    modeDefault,
                    new TaleWorlds.Localization.TextObject("Inventory"), // title
                    null!, // leftMemberRoster
                    null! // otherSideCapacityData
                });

                return logic;
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error creating headless InventoryLogic: {ex}");
                return null;
            }
        }

        public static void AutoEquipHeadless(MobileParty party, string context = "Post-Battle")
        {
            if (party == null || Hero.MainHero == null) return;
            try
            {
                var settings = Settings.Instance;
                if (settings == null) return;

                var inventoryLogic = CreateHeadlessInventoryLogic(party);
                if (inventoryLogic == null) return;

                var heroesToProcess = new List<Hero>();
                heroesToProcess.Add(Hero.MainHero);
                if (settings.AutoEquipCompanions)
                {
                    foreach (var member in party.MemberRoster.GetTroopRoster())
                    {
                        if (member.Character.IsHero && member.Character.HeroObject != null && member.Character.HeroObject != Hero.MainHero)
                        {
                            heroesToProcess.Add(member.Character.HeroObject);
                        }
                    }
                }

                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"=== Headless {context} Equipment Optimization started for: {string.Join(", ", heroesToProcess.Select(h => h.Name.ToString()))} ===");

                int equippedCount = EvaluateAndEquip(inventoryLogic, heroesToProcess, settings, null);

                if (equippedCount > 0 && inventoryLogic.IsThereAnyChanges())
                {
                    inventoryLogic.DoneLogic();
                }

                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"=== Headless {context} Equipment Optimization completed (Total equipped: {equippedCount}) ===");
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in AutoEquipHeadless: {ex}");
            }
        }


        private static int EvaluateAndEquip(InventoryLogic inventoryLogic, List<Hero> heroesToProcess, Settings settings, List<string>? notifications)
        {
            int equippedCount = 0;
            var armorSlots = new EquipmentIndex[] { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
            var weaponSlots = new EquipmentIndex[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 };

            var sneakingTargets = new List<EquipTarget>();
            var civilianTargets = new List<EquipTarget>();
            var combatTargets = new List<EquipTarget>();

            foreach (var hero in heroesToProcess)
            {
                if (hero.CharacterObject == null) continue;

                // 1. Sneaking (Stealth Equipment)
                sneakingTargets.Add(new EquipTarget { Hero = hero, Side = InventoryLogic.InventorySide.StealthEquipment, PrioritizeStealth = true });

                // 2. Civilian (Civilian Equipment)
                civilianTargets.Add(new EquipTarget { Hero = hero, Side = InventoryLogic.InventorySide.CivilianEquipment, PrioritizeStealth = false });

                // 3. Combat (Battle Equipment)
                combatTargets.Add(new EquipTarget { Hero = hero, Side = InventoryLogic.InventorySide.BattleEquipment, PrioritizeStealth = false });
            }

            var combinedTargets = new List<EquipTarget>();
            var priority = settings.LoadoutPrioritySetting;
            if (priority == LoadoutPriority.Sneaking_Civilian_Combat)
            {
                combinedTargets.AddRange(sneakingTargets);
                combinedTargets.AddRange(civilianTargets);
                combinedTargets.AddRange(combatTargets);
            }
            else if (priority == LoadoutPriority.Sneaking_Combat_Civilian)
            {
                combinedTargets.AddRange(sneakingTargets);
                combinedTargets.AddRange(combatTargets);
                combinedTargets.AddRange(civilianTargets);
            }
            else if (priority == LoadoutPriority.Combat_Sneaking_Civilian)
            {
                combinedTargets.AddRange(combatTargets);
                combinedTargets.AddRange(sneakingTargets);
                combinedTargets.AddRange(civilianTargets);
            }
            else if (priority == LoadoutPriority.Combat_Civilian_Sneaking)
            {
                combinedTargets.AddRange(combatTargets);
                combinedTargets.AddRange(civilianTargets);
                combinedTargets.AddRange(sneakingTargets);
            }
            else if (priority == LoadoutPriority.Civilian_Sneaking_Combat)
            {
                combinedTargets.AddRange(civilianTargets);
                combinedTargets.AddRange(sneakingTargets);
                combinedTargets.AddRange(combatTargets);
            }
            else // Civilian_Combat_Sneaking
            {
                combinedTargets.AddRange(civilianTargets);
                combinedTargets.AddRange(combatTargets);
                combinedTargets.AddRange(sneakingTargets);
            }

            foreach (var target in combinedTargets)
            {
                var hero = target.Hero;
                var targetSide = target.Side;
                bool prioritizeStealth = target.PrioritizeStealth;

                Equipment equipment;
                if (targetSide == InventoryLogic.InventorySide.StealthEquipment)
                {
                    equipment = hero.StealthEquipment;
                }
                else if (targetSide == InventoryLogic.InventorySide.CivilianEquipment)
                {
                    equipment = hero.CivilianEquipment;
                }
                else
                {
                    equipment = hero.BattleEquipment;
                }

                // A. Armor Slots
                if (settings.AutoEquipCategorySetting == AutoEquipCategory.ArmorOnly || settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsAndArmor)
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

                            if (targetSide == InventoryLogic.InventorySide.CivilianEquipment && !item.IsCivilian) continue;
                            if (targetSide == InventoryLogic.InventorySide.StealthEquipment && !item.IsStealthItem) continue;
                            if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                            bool strictlyBeats = StrictlyBeatsArmor(candidate, currentArmor, prioritizeStealth);
                            float score = GetArmorScore(candidate, prioritizeStealth);

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
                                float currentScore = GetArmorScore(currentArmor, prioritizeStealth);
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
                            equippedCount++;

                            string slotName = GetSlotName(slot);
                            string setName = targetSide == InventoryLogic.InventorySide.StealthEquipment ? "Sneaking" : (targetSide == InventoryLogic.InventorySide.CivilianEquipment ? "Civilian" : "Combat");
                            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Equipped {upgradeElement.EquipmentElement.Item.Name} on {hero.Name} in {slotName} slot ({setName} set).");

                            // If we successfully upgraded, update currentArmor for drawback comparison
                            currentArmor = upgradeElement.EquipmentElement;
                        }

                        // Check if a drawback candidate is still better than the newly equipped armor
                        if (notifications != null && bestDrawback.HasValue)
                        {
                            float currentScore = GetArmorScore(currentArmor, prioritizeStealth);
                            if (bestDrawbackScore > currentScore)
                            {
                                string slotName = GetSlotName(slot);
                                string setName = targetSide == InventoryLogic.InventorySide.StealthEquipment ? "Sneaking" : (targetSide == InventoryLogic.InventorySide.CivilianEquipment ? "Civilian" : "Combat");
                                notifications.Add($"{hero.Name} ({setName}): {bestDrawback.Value.EquipmentElement.Item.Name} in {slotName} slot is better but has drawbacks compared to {(currentArmor.IsEmpty ? "None" : currentArmor.Item.Name)}.");
                            }
                        }
                    }
                }

                // B. Weapon Slots (Only evaluate if slot is not empty)
                if (settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsOnly || settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsAndArmor)
                {
                    foreach (var slot in weaponSlots)
                    {
                        var currentWeapon = equipment[slot];
                        if (currentWeapon.IsEmpty || currentWeapon.Item == null || currentWeapon.Item.PrimaryWeapon == null) continue;

                        // Ignore special stealth stone throwing weapons in stealth loadout
                        if (targetSide == InventoryLogic.InventorySide.StealthEquipment && 
                            currentWeapon.Item.ItemType == ItemObject.ItemTypeEnum.Thrown && 
                            currentWeapon.Item.StringId == "stealth_throwing_stone")
                        {
                            continue;
                        }

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

                            if (targetSide == InventoryLogic.InventorySide.CivilianEquipment && !item.IsCivilian) continue;
                            if (targetSide == InventoryLogic.InventorySide.StealthEquipment && !item.IsStealthItem) continue;
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
                            equippedCount++;

                            string setName = targetSide == InventoryLogic.InventorySide.StealthEquipment ? "Sneaking" : (targetSide == InventoryLogic.InventorySide.CivilianEquipment ? "Civilian" : "Combat");
                            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Equipped {upgradeElement.EquipmentElement.Item.Name} on {hero.Name} in Weapon slot {slot} ({setName} set).");

                            currentWeapon = upgradeElement.EquipmentElement;
                        }

                        // Check if drawback candidate is still better
                        if (notifications != null && bestDrawback.HasValue)
                        {
                            float currentScore = GetWeaponScore(currentWeapon);
                            if (bestDrawbackScore > currentScore)
                            {
                                string setName = targetSide == InventoryLogic.InventorySide.StealthEquipment ? "Sneaking" : (targetSide == InventoryLogic.InventorySide.CivilianEquipment ? "Civilian" : "Combat");
                                notifications.Add($"{hero.Name} ({setName}): {bestDrawback.Value.EquipmentElement.Item.Name} in {slot} slot is better but has drawbacks compared to {currentWeapon.Item.Name}.");
                            }
                        }
                    }
                }
            }

            return equippedCount;
        }
    }
}
