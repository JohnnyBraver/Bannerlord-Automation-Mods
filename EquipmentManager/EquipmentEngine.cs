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
using SettlementAutomationCore.Transactions;

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

        private struct EquipSlotTarget
        {
            public EquipTarget Target;
            public EquipmentIndex Slot;
            public bool IsWeapon;
        }

        private sealed class AvailableEquipment
        {
            public EquipmentElement EquipmentElement { get; }
            public int Quantity { get; set; }

            public AvailableEquipment(EquipmentElement equipmentElement, int quantity)
            {
                EquipmentElement = equipmentElement;
                Quantity = quantity;
            }
        }

        private sealed class SellCandidate
        {
            public SPItemVM ItemVM { get; }
            public int Quantity { get; }

            public SellCandidate(SPItemVM itemVM, int quantity)
            {
                ItemVM = itemVM;
                Quantity = quantity;
            }
        }

        private abstract class EquipmentTransferContext
        {
            public abstract List<AvailableEquipment> BuildAvailableEquipmentPool();
            public abstract void EquipItem(AvailableEquipment available, EquipTarget target, EquipmentIndex slot);
        }

        private sealed class CoreEquipmentTransferContext : EquipmentTransferContext
        {
            private readonly IPartyEquipmentTransaction _transaction;

            public CoreEquipmentTransferContext(IPartyEquipmentTransaction transaction)
            {
                _transaction = transaction;
            }

            public override List<AvailableEquipment> BuildAvailableEquipmentPool()
            {
                return _transaction.BuildAvailableEquipmentPool()
                    .Select(candidate => new AvailableEquipment(candidate.EquipmentElement, candidate.Quantity))
                    .ToList();
            }

            public override void EquipItem(AvailableEquipment available, EquipTarget target, EquipmentIndex slot)
            {
                var result = _transaction.EquipItem(available.EquipmentElement, target.Hero, target.Side, slot);
                if (!result.Success)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Skipped equipment transfer: {result.FailureReason}");
                }
            }
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
            var equipmentTransaction = PartyEquipmentTransaction.ForInventoryLogic(inventoryLogic, vm);
            equippedCount = EvaluateAndEquip(new CoreEquipmentTransferContext(equipmentTransaction), heroesToProcess, settings, notifications);

            // 3. Display drawback notifications
            foreach (var note in notifications)
            {
                InformationManager.DisplayMessage(new InformationMessage(note, new Color(0.9f, 0.6f, 0.2f)));
            }

            // 4. Build sale-protection plan without mutating player item locks.
            EquipmentProtectionPlan? protectionPlan = null;
            if (vm.RightItemListVM != null)
            {
                bool hasWeaponPerk = Hero.MainHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise);
                bool hasArmorPerk = Hero.MainHero.GetPerkValue(DefaultPerks.Steward.GivingHands);

                var protectionItems = new List<EquipmentProtectionItem>();

                foreach (var itemVM in vm.RightItemListVM)
                {
                    if (itemVM == null || itemVM.ItemRosterElement.IsEmpty) continue;

                    var eqEl = itemVM.ItemRosterElement.EquipmentElement;
                    var item = eqEl.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (!isEquipment) continue;

                    protectionItems.Add(new EquipmentProtectionItem(eqEl, itemVM.ItemCount, itemVM.ItemCost));
                }

                protectionPlan = EquipmentSaleProtector.BuildProtectionPlan(protectionItems, heroesToProcess, settings, hasWeaponPerk, hasArmorPerk);
            }

            // 5. Sell Phase
            var settlement = inventoryLogic.CurrentSettlementComponent?.Settlement;
            bool skipSell = settlement != null && settings.PreventEquipmentSaleInVillages && settlement.IsVillage;

            if (settings.SellUnlockedEquipment && vm.RightItemListVM != null && !skipSell)
            {
                var itemsToSell = new List<SellCandidate>();
                foreach (var itemVM in vm.RightItemListVM)
                {
                    if (itemVM == null || itemVM.ItemRosterElement.IsEmpty) continue;
                    if (itemVM.IsLocked) continue;

                    var item = itemVM.ItemRosterElement.EquipmentElement.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (isEquipment)
                    {
                        int sellQuantity = protectionPlan?.GetSellableQuantity(itemVM.ItemRosterElement.EquipmentElement, itemVM.ItemCount) ?? itemVM.ItemCount;
                        if (sellQuantity > 0)
                        {
                            itemsToSell.Add(new SellCandidate(itemVM, sellQuantity));
                        }
                    }
                }

                // Prioritize heavy items if enabled (using weight/price ratio descending)
                if (settings.PrioritizeHeavyTrash)
                {
                    itemsToSell = itemsToSell.OrderByDescending(candidate =>
                    {
                        var item = candidate.ItemVM.ItemRosterElement.EquipmentElement.Item;
                        int price = inventoryLogic.GetItemPrice(candidate.ItemVM.ItemRosterElement.EquipmentElement, false);
                        return (float)item.Weight / (price > 0 ? price : 1);
                    }).ToList();
                }

                var soldLogs = new List<string>();
                foreach (var candidate in itemsToSell)
                {
                    try
                    {
                        var itemVM = candidate.ItemVM;
                        int count = candidate.Quantity;
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
                        SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error selling {candidate.ItemVM.ItemRosterElement.EquipmentElement.Item?.Name}: {ex.Message}");
                    }
                }
                if (soldLogs.Count > 0)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Sold unprotected equipment: {string.Join(", ", soldLogs)}");
                }
            }

            // 6. Refresh UI
            equipmentTransaction.RefreshAfterTransaction();

            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"=== Equipment Optimization Run completed (Total equipped: {equippedCount}, Total sold: {totalSold}) ===");
        }

        private static bool StrictlyBeatsArmor(EquipmentElement candidate, EquipmentElement current, bool prioritizeStealth)
        {
            return EquipmentComparison.StrictlyBeatsArmor(candidate, current, prioritizeStealth);
        }

        private static float GetArmorScore(EquipmentElement eqEl, bool prioritizeStealth)
        {
            return EquipmentComparison.GetArmorScore(eqEl, prioritizeStealth);
        }

        private static bool StrictlyBeatsWeapon(EquipmentElement candidate, EquipmentElement current)
        {
            return EquipmentComparison.StrictlyBeatsWeapon(candidate, current);
        }

        private static float GetWeaponScore(EquipmentElement eqEl)
        {
            return EquipmentComparison.GetWeaponScore(eqEl);
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

        public static void AutoEquipHeadless(MobileParty party, string context = "Post-Battle")
        {
            if (party == null || Hero.MainHero == null) return;
            try
            {
                var settings = Settings.Instance;
                if (settings == null) return;

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

                var equipmentTransaction = PartyEquipmentTransaction.ForParty(party);
                int totalEquipped = EvaluateAndEquip(new CoreEquipmentTransferContext(equipmentTransaction), heroesToProcess, settings, null);
                equipmentTransaction.RefreshAfterTransaction();

                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"=== Headless {context} Equipment Optimization completed (Total equipped: {totalEquipped}) ===");
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in AutoEquipHeadless: {ex}");
            }
        }


        private static int EvaluateAndEquip(EquipmentTransferContext transferContext, List<Hero> heroesToProcess, Settings settings, List<string>? notifications)
        {
            int equippedCount = 0;
            var armorSlots = new EquipmentIndex[] { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
            var weaponSlots = new EquipmentIndex[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 };
            var availableItems = transferContext.BuildAvailableEquipmentPool();
            var cascadeQueue = new Queue<AvailableEquipment>();
            var virtualEquipment = new Dictionary<(Hero Hero, InventoryLogic.InventorySide Side, EquipmentIndex Slot), EquipmentElement>();

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

            var slotTargets = BuildSlotTargets(combinedTargets, settings, armorSlots, weaponSlots);

            foreach (var target in combinedTargets)
            {
                var hero = target.Hero;
                var targetSide = target.Side;
                bool prioritizeStealth = target.PrioritizeStealth;

                // A. Armor Slots
                if (settings.AutoEquipCategorySetting == AutoEquipCategory.ArmorOnly || settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsAndArmor)
                {
                    foreach (var slot in armorSlots)
                    {
                        var currentArmor = GetCurrentEquipment(virtualEquipment, target, slot);

                        // Find best strict upgrade and best drawback candidate
                        AvailableEquipment? bestStrictUpgrade = null;
                        float bestStrictScore = -9999f;

                        AvailableEquipment? bestDrawback = null;
                        float bestDrawbackScore = -9999f;

                        foreach (var available in availableItems)
                        {
                            if (available.Quantity <= 0) continue;

                            var candidate = available.EquipmentElement;
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
                                    bestStrictUpgrade = available;
                                }
                            }
                            else
                            {
                                float currentScore = GetArmorScore(currentArmor, prioritizeStealth);
                                if (score > currentScore && score > bestDrawbackScore)
                                {
                                    bestDrawbackScore = score;
                                    bestDrawback = available;
                                }
                            }
                        }

                        // Equip strict upgrade if found
                        if (bestStrictUpgrade != null)
                        {
                            var upgradeElement = bestStrictUpgrade.EquipmentElement;
                            EquipAvailableItem(transferContext, bestStrictUpgrade, target, slot, currentArmor, false, availableItems, cascadeQueue, virtualEquipment);
                            equippedCount++;

                            string slotName = GetSlotName(slot);
                            string setName = GetSetName(targetSide);
                            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Equipped {upgradeElement.Item.Name} on {hero.Name} in {slotName} slot ({setName} set).");

                            // If we successfully upgraded, update currentArmor for drawback comparison
                            currentArmor = upgradeElement;
                        }

                        // Check if a drawback candidate is still better than the newly equipped armor
                        if (notifications != null && bestDrawback != null)
                        {
                            float currentScore = GetArmorScore(currentArmor, prioritizeStealth);
                            if (bestDrawbackScore > currentScore)
                            {
                                string slotName = GetSlotName(slot);
                                string setName = GetSetName(targetSide);
                                notifications.Add($"{hero.Name} ({setName}): {bestDrawback.EquipmentElement.Item.Name} in {slotName} slot is better but has drawbacks compared to {(currentArmor.IsEmpty ? "None" : currentArmor.Item.Name)}.");
                            }
                        }
                    }
                }

                // B. Weapon Slots (Only evaluate if slot is not empty)
                if (settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsOnly || settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsAndArmor)
                {
                    foreach (var slot in weaponSlots)
                    {
                        var currentWeapon = GetCurrentEquipment(virtualEquipment, target, slot);
                        if (!EquipmentComparison.ShouldEvaluateWeaponSlot(currentWeapon, targetSide)) continue;

                        AvailableEquipment? bestStrictUpgrade = null;
                        float bestStrictScore = -9999f;

                        AvailableEquipment? bestDrawback = null;
                        float bestDrawbackScore = -9999f;

                        foreach (var available in availableItems)
                        {
                            if (available.Quantity <= 0) continue;

                            var candidate = available.EquipmentElement;
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
                                    bestStrictUpgrade = available;
                                }
                            }
                            else
                            {
                                float currentScore = GetWeaponScore(currentWeapon);
                                if (score > currentScore && score > bestDrawbackScore)
                                {
                                    bestDrawbackScore = score;
                                    bestDrawback = available;
                                }
                            }
                        }

                        // Equip strict upgrade if found
                        if (bestStrictUpgrade != null)
                        {
                            var upgradeElement = bestStrictUpgrade.EquipmentElement;
                            EquipAvailableItem(transferContext, bestStrictUpgrade, target, slot, currentWeapon, true, availableItems, cascadeQueue, virtualEquipment);
                            equippedCount++;

                            string setName = GetSetName(targetSide);
                            SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Equipped {upgradeElement.Item.Name} on {hero.Name} in Weapon slot {slot} ({setName} set).");

                            currentWeapon = upgradeElement;
                        }

                        // Check if drawback candidate is still better
                        if (notifications != null && bestDrawback != null)
                        {
                            float currentScore = GetWeaponScore(currentWeapon);
                            if (bestDrawbackScore > currentScore)
                            {
                                string setName = GetSetName(targetSide);
                                notifications.Add($"{hero.Name} ({setName}): {bestDrawback.EquipmentElement.Item.Name} in {slot} slot is better but has drawbacks compared to {currentWeapon.Item.Name}.");
                            }
                        }
                    }
                }
            }

            int maxCascadeIterations = EquipmentDecisionMath.GetCascadeIterationLimit(slotTargets.Count, cascadeQueue.Count);
            int cascadeIterations = 0;
            while (cascadeQueue.Count > 0)
            {
                if (++cascadeIterations > maxCascadeIterations)
                {
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Aborted equipment cascade after {cascadeIterations - 1} attempts to prevent an auto-equip loop. Pending displaced items: {cascadeQueue.Count}.");
                    break;
                }

                var freedItem = cascadeQueue.Dequeue();
                if (freedItem.Quantity <= 0) continue;

                if (TryFitFreedItem(transferContext, freedItem, slotTargets, availableItems, cascadeQueue, virtualEquipment))
                {
                    equippedCount++;
                }
            }

            return equippedCount;
        }

        private static List<EquipSlotTarget> BuildSlotTargets(
            List<EquipTarget> combinedTargets,
            Settings settings,
            EquipmentIndex[] armorSlots,
            EquipmentIndex[] weaponSlots)
        {
            var slotTargets = new List<EquipSlotTarget>();

            if (settings.AutoEquipCategorySetting == AutoEquipCategory.ArmorOnly || settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsAndArmor)
            {
                foreach (var target in combinedTargets)
                {
                    foreach (var slot in armorSlots)
                    {
                        slotTargets.Add(new EquipSlotTarget { Target = target, Slot = slot, IsWeapon = false });
                    }
                }
            }

            if (settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsOnly || settings.AutoEquipCategorySetting == AutoEquipCategory.WeaponsAndArmor)
            {
                foreach (var target in combinedTargets)
                {
                    foreach (var slot in weaponSlots)
                    {
                        slotTargets.Add(new EquipSlotTarget { Target = target, Slot = slot, IsWeapon = true });
                    }
                }
            }

            return slotTargets;
        }

        private static EquipmentElement GetCurrentEquipment(
            Dictionary<(Hero Hero, InventoryLogic.InventorySide Side, EquipmentIndex Slot), EquipmentElement> virtualEquipment,
            EquipTarget target,
            EquipmentIndex slot)
        {
            var key = (target.Hero, target.Side, slot);
            if (virtualEquipment.TryGetValue(key, out var equipmentElement))
            {
                return equipmentElement;
            }

            return GetEquipmentForSide(target.Hero, target.Side)[slot];
        }

        private static void SetCurrentEquipment(
            Dictionary<(Hero Hero, InventoryLogic.InventorySide Side, EquipmentIndex Slot), EquipmentElement> virtualEquipment,
            EquipTarget target,
            EquipmentIndex slot,
            EquipmentElement equipmentElement)
        {
            virtualEquipment[(target.Hero, target.Side, slot)] = equipmentElement;
        }

        private static Equipment GetEquipmentForSide(Hero hero, InventoryLogic.InventorySide side)
        {
            if (side == InventoryLogic.InventorySide.StealthEquipment)
            {
                return hero.StealthEquipment;
            }

            if (side == InventoryLogic.InventorySide.CivilianEquipment)
            {
                return hero.CivilianEquipment;
            }

            return hero.BattleEquipment;
        }

        private static void EquipAvailableItem(
            EquipmentTransferContext transferContext,
            AvailableEquipment available,
            EquipTarget target,
            EquipmentIndex slot,
            EquipmentElement currentEquipment,
            bool isWeapon,
            List<AvailableEquipment> availableItems,
            Queue<AvailableEquipment> cascadeQueue,
            Dictionary<(Hero Hero, InventoryLogic.InventorySide Side, EquipmentIndex Slot), EquipmentElement> virtualEquipment)
        {
            transferContext.EquipItem(available, target, slot);

            available.Quantity--;
            SetCurrentEquipment(virtualEquipment, target, slot, available.EquipmentElement);

            if (!currentEquipment.IsEmpty && currentEquipment.Item != null)
            {
                var freedItem = new AvailableEquipment(currentEquipment, 1);
                availableItems.Add(freedItem);
                cascadeQueue.Enqueue(freedItem);
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Freed {currentEquipment.Item.Name} from {target.Hero.Name}'s {GetSetName(target.Side)} {GetSlotLabel(slot, isWeapon)} slot for cascade evaluation.");
            }
        }

        private static bool TryFitFreedItem(
            EquipmentTransferContext transferContext,
            AvailableEquipment freedItem,
            List<EquipSlotTarget> slotTargets,
            List<AvailableEquipment> availableItems,
            Queue<AvailableEquipment> cascadeQueue,
            Dictionary<(Hero Hero, InventoryLogic.InventorySide Side, EquipmentIndex Slot), EquipmentElement> virtualEquipment)
        {
            var candidate = freedItem.EquipmentElement;
            var item = candidate.Item;
            if (item == null) return false;

            foreach (var slotTarget in slotTargets)
            {
                var target = slotTarget.Target;
                var currentEquipment = GetCurrentEquipment(virtualEquipment, target, slotTarget.Slot);

                if (slotTarget.IsWeapon)
                {
                    if (!CanEquipWeaponCandidate(candidate, currentEquipment, target, slotTarget.Slot)) continue;
                    if (!StrictlyBeatsWeapon(candidate, currentEquipment)) continue;

                    EquipAvailableItem(transferContext, freedItem, target, slotTarget.Slot, currentEquipment, true, availableItems, cascadeQueue, virtualEquipment);
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Cascade equipped {item.Name} on {target.Hero.Name} in Weapon slot {slotTarget.Slot} ({GetSetName(target.Side)} set).");
                    return true;
                }

                if (!CanEquipArmorCandidate(candidate, target, slotTarget.Slot)) continue;
                if (!StrictlyBeatsArmor(candidate, currentEquipment, target.PrioritizeStealth)) continue;

                EquipAvailableItem(transferContext, freedItem, target, slotTarget.Slot, currentEquipment, false, availableItems, cascadeQueue, virtualEquipment);
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Cascade equipped {item.Name} on {target.Hero.Name} in {GetSlotName(slotTarget.Slot)} slot ({GetSetName(target.Side)} set).");
                return true;
            }

            return false;
        }

        private static bool CanEquipArmorCandidate(EquipmentElement candidate, EquipTarget target, EquipmentIndex slot)
        {
            var item = candidate.Item;
            if (item == null || !item.HasArmorComponent) return false;
            if (target.Side == InventoryLogic.InventorySide.CivilianEquipment && !item.IsCivilian) return false;
            if (target.Side == InventoryLogic.InventorySide.StealthEquipment && !item.IsStealthItem) return false;
            return Equipment.IsItemFitsToSlot(slot, item);
        }

        private static bool CanEquipWeaponCandidate(EquipmentElement candidate, EquipmentElement currentWeapon, EquipTarget target, EquipmentIndex slot)
        {
            var item = candidate.Item;
            if (item == null || item.PrimaryWeapon == null) return false;
            if (!EquipmentComparison.ShouldEvaluateWeaponSlot(currentWeapon, target.Side)) return false;
            if (target.Side == InventoryLogic.InventorySide.CivilianEquipment && !item.IsCivilian) return false;
            if (target.Side == InventoryLogic.InventorySide.StealthEquipment && !item.IsStealthItem) return false;

            return Equipment.IsItemFitsToSlot(slot, item);
        }

        private static string GetSetName(InventoryLogic.InventorySide side)
        {
            return side == InventoryLogic.InventorySide.StealthEquipment
                ? "Sneaking"
                : (side == InventoryLogic.InventorySide.CivilianEquipment ? "Civilian" : "Combat");
        }

        private static string GetSlotLabel(EquipmentIndex slot, bool isWeapon)
        {
            return isWeapon ? $"Weapon {slot}" : GetSlotName(slot);
        }
    }
}
