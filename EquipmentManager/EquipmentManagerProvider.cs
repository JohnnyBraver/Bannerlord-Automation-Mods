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
    public class EquipmentManagerProvider : IAutomationPreparationProvider, IPreSellProvider, IAutomationRequestProvider, IAutomationReportProvider, IAutomationReportStyleProvider
    {
        private struct PotentialBuyOrder
        {
            public InventoryItemView Candidate;
            public int Price;
            public int ExplicitGoldReserve;
            public RequestProfile Profile;
            public float ScoreIncrease;
            public bool PrioritizeStealth;
            public Hero Hero;
            public EquipmentIndex Slot;
        }

        public string ProviderName => "EquipmentManager";
        public uint? ReportHeaderColor => 0x8FA3ADFF;

        public IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context)
        {
            if (context == null) return new List<string>();

            var settings = Settings.Instance;
            return BuildAutomationReportLines(
                context.Stage,
                context.Settlement?.Name?.ToString() ?? "Settlement",
                context.BoughtItems,
                context.SoldItems,
                settings?.EquipmentSaleReportDetail ?? EquipmentSaleReportDetailMode.CategoryCounts,
                Math.Max(1, settings?.MaxReportItemsToPrint ?? 4),
                settings?.EquipmentReportSort ?? EquipmentReportSortMode.PaidPrice);
        }

        internal static IReadOnlyList<string> BuildAutomationReportLines(
            AutomationTransactionStage stage,
            string settlementName,
            IReadOnlyList<AutomationReportItem> boughtItems,
            IReadOnlyList<AutomationReportItem> soldItems,
            EquipmentSaleReportDetailMode saleReportDetail,
            int maxItems,
            EquipmentReportSortMode sortMode)
        {
            var lines = new List<string>();
            maxItems = Math.Max(1, maxItems);

            if (stage == AutomationTransactionStage.PreSell && soldItems.Count > 0)
            {
                lines.Add($"[Equipment] Sold spare gear @ {settlementName}: {FormatSaleReportItems(soldItems, saleReportDetail, maxItems, sortMode)}");
            }
            else if (stage == AutomationTransactionStage.PriorityRequest && boughtItems.Count > 0)
            {
                lines.Add($"[Equipment] Bought upgrades @ {settlementName}: {FormatDetailedReportItems(boughtItems, false, maxItems, sortMode)}");
            }
            else if (stage == AutomationTransactionStage.FreeTrade)
            {
                if (soldItems.Count > 0)
                {
                    lines.Add($"[Equipment] Free-trade gear sold @ {settlementName}: {FormatSaleReportItems(soldItems, saleReportDetail, maxItems, sortMode)}");
                }
                if (boughtItems.Count > 0)
                {
                    lines.Add($"[Equipment] Free-trade gear bought @ {settlementName}: {FormatDetailedReportItems(boughtItems, false, maxItems, sortMode)}");
                }
            }

            return lines;
        }

        public void PrepareForAutomation(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null) return;

            try
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"[Pre-Transaction] Securing existing equipment upgrades in {settlement.Name} before trade phase starts.");
                EquipmentEngine.AutoEquipHeadless(party, "Pre-Transaction");
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in PrepareForAutomation AutoEquipHeadless: {ex}");
            }
        }

        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<TradeOrder>();
            if (party == null || settlement == null) return orders;

            var settings = Settings.Instance;
            if (settings == null || !settings.SellUnlockedEquipment) return orders;
            if (settings.PreventEquipmentSaleInVillages && settlement.IsVillage) return orders;

            var currentLogic = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement, true);
            if (currentLogic == null) return orders;

            try
            {
                var locks = InventoryLockHelper.GetCurrentLockKeys();

                bool hasWeaponPerk = Hero.MainHero?.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.PaidInPromise) ?? false;
                bool hasArmorPerk = Hero.MainHero?.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.GivingHands) ?? false;

                var targets = new List<Hero>();
                if (Hero.MainHero != null)
                {
                    targets.Add(Hero.MainHero);
                }
                if (settings.AutoEquipCompanions && party != null)
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
                var protectionItems = new List<EquipmentProtectionItem>();
                for (int i = 0; i < playerElements.Count; i++)
                {
                    var rosterElement = playerElements[i];
                    if (rosterElement.IsEmpty || rosterElement.Amount <= 0) continue;

                    var eqEl = rosterElement.EquipmentElement;
                    var item = eqEl.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (!isEquipment) continue;

                    float sellPrice = currentLogic.GetItemPrice(eqEl, false);
                    protectionItems.Add(new EquipmentProtectionItem(eqEl, rosterElement.Amount, sellPrice));
                }

                var protectionPlan = EquipmentSaleProtector.BuildProtectionPlan(protectionItems, targets, settings, hasWeaponPerk, hasArmorPerk);

                for (int i = 0; i < playerElements.Count; i++)
                {
                    var rosterElement = playerElements[i];
                    if (rosterElement.IsEmpty || rosterElement.Amount <= 0) continue;

                    var eqEl = rosterElement.EquipmentElement;
                    var item = eqEl.Item;
                    if (item == null) continue;

                    bool isEquipment = item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null;
                    if (!isEquipment) continue;

                    if (InventoryLockHelper.IsLocked(eqEl, locks)) continue;

                    int sellQuantity = protectionPlan.GetSellableQuantity(eqEl, rosterElement.Amount);
                    if (sellQuantity > 0)
                    {
                        orders.Add(new TradeOrder(eqEl, sellQuantity, false));
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

                    if (prioritizeStealth && !canBuyStealth)
                    {
                        continue;
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
                                ExplicitGoldReserve = requiredReserve,
                                Profile = prioritizeStealth ? settings.StealthGearRequestProfile : settings.TopArmorRequestProfile,
                                ScoreIncrease = scoreIncrease,
                                PrioritizeStealth = prioritizeStealth,
                                Hero = hero,
                                Slot = slot
                            });
                        }
                    }
                }
            }

            // Submit candidates in preference order; Core buys the best affordable one.
            if (potentialOrders.Count > 0)
            {
                var requestGroups = EquipmentMarketRequestPlanner.BuildRequestGroups(
                    potentialOrders.Select(o => new EquipmentMarketCandidateOrder(
                        o.Candidate,
                        o.ExplicitGoldReserve,
                        o.Profile,
                        o.ScoreIncrease)),
                    "EquipmentManager",
                    1);

                foreach (var requestGroup in requestGroups)
                {
                    var bestOrder = potentialOrders
                        .Where(o => o.Candidate.SnapshotId == requestGroup.TopCandidate.SnapshotId)
                        .OrderByDescending(o => o.ScoreIncrease)
                        .First();

                    AutomationRegistry.RegisterRequest(requestGroup.Request);
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Requested Auto-Buy candidates (Limit 1 per profile/reserve group): top choice {bestOrder.Candidate.Item.Name} as upgrade for {bestOrder.Hero.Name} ({bestOrder.Slot} slot). Candidates: {requestGroup.CandidateCount}. Profile: {requestGroup.Profile}. Reserve: {requestGroup.ExplicitGoldReserve} denars. Price seen: {bestOrder.Price} denars. Score Increase: {requestGroup.TopScoreIncrease:F1}");
                }
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
            if (item == null) return false;

            var armorSlots = new EquipmentIndex[] { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
            var weaponSlots = new EquipmentIndex[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 };
            
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
                        if (!item.IsCivilian) continue;
                        equipment = hero.CivilianEquipment;
                        side = InventoryLogic.InventorySide.CivilianEquipment;
                    }
                    else
                    {
                        if (!item.IsStealthItem) continue;
                        equipment = hero.StealthEquipment;
                        side = InventoryLogic.InventorySide.StealthEquipment;
                        prioritizeStealth = true;
                    }

                    if (item.HasArmorComponent)
                    {
                        foreach (var slot in armorSlots)
                        {
                            if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                            var currentArmor = equipment[slot];
                            float currentScore = prioritizeStealth ? GetStealthScore(currentArmor) : GetArmorScore(currentArmor);

                            if (prioritizeStealth)
                            {
                                float candidateScore = GetStealthScore(eqEl);
                                if (EquipmentComparison.StrictlyBeatsArmor(eqEl, currentArmor, true) || candidateScore > currentScore) return true;
                            }
                            else
                            {
                                float candidateScore = GetArmorScore(eqEl);
                                if (EquipmentComparison.StrictlyBeatsArmor(eqEl, currentArmor, false) || candidateScore > currentScore) return true;
                            }
                        }
                    }

                    if (item.PrimaryWeapon != null)
                    {
                        foreach (var slot in weaponSlots)
                        {
                            if (!Equipment.IsItemFitsToSlot(slot, item)) continue;

                            var currentWeapon = equipment[slot];
                            if (!EquipmentComparison.ShouldEvaluateWeaponSlot(currentWeapon, side)) continue;

                            float candidateScore = EquipmentComparison.GetWeaponScore(eqEl);
                            float currentScore = EquipmentComparison.GetWeaponScore(currentWeapon);
                            if (EquipmentComparison.StrictlyBeatsWeapon(eqEl, currentWeapon) || candidateScore > currentScore) return true;
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

        private static string FormatSaleReportItems(
            IReadOnlyList<AutomationReportItem> items,
            EquipmentSaleReportDetailMode saleReportDetail,
            int maxItems,
            EquipmentReportSortMode sortMode)
        {
            if (saleReportDetail == EquipmentSaleReportDetailMode.FullItemList)
            {
                return FormatDetailedReportItems(items, true, maxItems, sortMode);
            }

            return FormatCategoryCounts(items, isSale: true);
        }

        private static string FormatCategoryCounts(IReadOnlyList<AutomationReportItem> items, bool isSale)
        {
            return string.Join(", ", items
                .GroupBy(item => item.CategoryName)
                .OrderBy(group => GetCategorySortOrder(group.First().Category))
                .ThenBy(group => group.Key)
                .Select(group => FormatCategoryTotal(group.Key, group.ToList(), isSale)));
        }

        private static string FormatCategoryTotal(string categoryName, IReadOnlyList<AutomationReportItem> items, bool isSale)
        {
            int quantity = items.Sum(item => item.Quantity);
            int gold = items.Sum(item => item.Gold);
            if (gold == 0)
            {
                return $"{categoryName} {quantity}x";
            }

            string sign = isSale ? "+" : "-";
            return $"{categoryName} {quantity}x ({sign}{Math.Abs(gold)}d)";
        }

        private static string FormatDetailedReportItems(IReadOnlyList<AutomationReportItem> items, bool isSale, int maxItems, EquipmentReportSortMode sortMode)
        {
            var orderedItems = SortReportItems(items, sortMode).ToList();
            var visible = orderedItems
                .Take(Math.Max(1, maxItems))
                .Select(item => FormatReportItem(item, isSale))
                .ToList();

            int hidden = orderedItems.Count - visible.Count;
            if (hidden > 0)
            {
                visible.Add($"{hidden} more");
            }

            return string.Join(", ", visible);
        }

        private static IEnumerable<AutomationReportItem> SortReportItems(IReadOnlyList<AutomationReportItem> items, EquipmentReportSortMode sortMode)
        {
            switch (sortMode)
            {
                case EquipmentReportSortMode.Amount:
                    return items.OrderByDescending(item => item.Quantity).ThenBy(item => item.ItemName);
                case EquipmentReportSortMode.MarketValue:
                    return items.OrderByDescending(item => item.MarketValue).ThenByDescending(item => item.Gold).ThenBy(item => item.ItemName);
                case EquipmentReportSortMode.PaidPrice:
                default:
                    return items.OrderByDescending(item => item.Gold).ThenByDescending(item => item.MarketValue).ThenBy(item => item.ItemName);
            }
        }

        private static int GetCategorySortOrder(InventoryItemCategory category)
        {
            if ((category & InventoryItemCategory.Armor) == InventoryItemCategory.Armor) return 0;
            if ((category & InventoryItemCategory.Weapon) == InventoryItemCategory.Weapon) return 1;
            return 2;
        }

        private static string FormatReportItem(AutomationReportItem item, bool isSale)
        {
            if (item.Gold == 0)
            {
                return $"{item.Quantity}x {item.ItemName}";
            }

            string sign = isSale ? "+" : "-";
            return $"{item.Quantity}x {item.ItemName} ({sign}{Math.Abs(item.Gold)}d)";
        }
    }
}
