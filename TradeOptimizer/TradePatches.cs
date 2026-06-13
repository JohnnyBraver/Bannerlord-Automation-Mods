using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TradeOptimizer
{
    public static class TradingPatches
    {
        public static SPInventoryVM? ActiveInventoryVM { get; private set; }

        public static void OnSPInventoryVMConstructed(SPInventoryVM __instance)
        {
            ActiveInventoryVM = __instance;
        }

        [HarmonyPatch(typeof(SPInventoryVM), "OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalizePostfix()
        {
            ActiveInventoryVM = null;
        }

        public static void ManualTrigger()
        {
            if (ActiveInventoryVM == null) return;
            try
            {
                var settings = Settings.Instance;
                float elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
                if (settings != null && elapsedDays < settings.InitialSettlementDaysDelay)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[TradeOptimizer] Manual trade disabled during economy settling period (Day {elapsedDays:F1}/{settings.InitialSettlementDaysDelay})"
                    ));
                    return;
                }

                int initialGold = Hero.MainHero?.Gold ?? 0;
                var report = TradingEngine.RunOptimization(ActiveInventoryVM, isSellPhase: true, isBuyPhase: true);
                
                int finalGold = Hero.MainHero?.Gold ?? 0;
                string traderName = ActiveInventoryVM.GetInventoryLogic()?.OtherParty?.Name?.ToString() ?? "Trader";
                
                // Show TLDR in-game
                PrintTradeReport(finalGold, initialGold, report, traderName);
                
                // Print cargo after the full run so weight is accurate
                bool isSim = Settings.Instance?.SimulationMode ?? false;
                if (report.SoldItems.Count > 0 || report.BoughtItems.Count > 0)
                    PrintCargoStatus(isSim);
                
                // Refresh values on UI
                ActiveInventoryVM.RefreshValues();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Trade Optimizer Error: {ex.Message}"));
            }
        }

        public static void PrintTradeReport(int finalGold, int initialGold, TradeTransactionReport report, string traderName)
        {
            bool isSim = Settings.Instance?.SimulationMode ?? false;
            string simPrefix = isSim ? "[Simulation] " : "";

            if (report.SoldItems.Count == 0 && report.BoughtItems.Count == 0)
            {
                string noTradeMsg = $"{simPrefix}Trade Optimizer: Visited {traderName} - No profitable trades found.";
                InformationManager.DisplayMessage(new InformationMessage(noTradeMsg));
                TradingEngine.WriteLog(noTradeMsg);
                return;
            }

            // Determine accuracy level from settings and trade perks
            bool hasTier1 = false;
            bool hasTier2 = false;
            if (Hero.MainHero != null)
            {
                try
                {
                    var defaultPerksType = typeof(PerkObject).Assembly.GetType("TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks");
                    var tradeType = defaultPerksType?.GetNestedType("Trade", BindingFlags.Public | BindingFlags.NonPublic);
                    if (tradeType != null)
                    {
                        var appraiserProp = tradeType.GetProperty("Appraiser", BindingFlags.Public | BindingFlags.Static);
                        var wholesellerProp = tradeType.GetProperty("WholeSeller", BindingFlags.Public | BindingFlags.Static);
                        var caravanProp = tradeType.GetProperty("CaravanMaster", BindingFlags.Public | BindingFlags.Static);
                        var marketProp = tradeType.GetProperty("MarketDealer", BindingFlags.Public | BindingFlags.Static);

                        var appraiserPerk = appraiserProp?.GetValue(null) as PerkObject;
                        var wholesellerPerk = wholesellerProp?.GetValue(null) as PerkObject;
                        var caravanPerk = caravanProp?.GetValue(null) as PerkObject;
                        var marketPerk = marketProp?.GetValue(null) as PerkObject;

                        if (appraiserPerk != null && Hero.MainHero.GetPerkValue(appraiserPerk)) hasTier1 = true;
                        if (wholesellerPerk != null && Hero.MainHero.GetPerkValue(wholesellerPerk)) hasTier1 = true;
                        if (caravanPerk != null && Hero.MainHero.GetPerkValue(caravanPerk)) hasTier2 = true;
                        if (marketPerk != null && Hero.MainHero.GetPerkValue(marketPerk)) hasTier2 = true;
                    }
                }
                catch { }
            }

            var settings = Settings.Instance;
            bool alwaysGlobal = settings?.PricingReference == PricingReferenceMode.AlwaysGlobal;
            bool exactMode = alwaysGlobal || (hasTier1 && hasTier2);
            bool estimateMode = hasTier1; // can show % even without exact numbers

            // ─── Sales Report ───────────────────────────────────────────────────
            if (report.SoldItems.Count > 0)
            {
                int totalSaleGold = report.SoldItems.Sum(s => s.Gold);
                double totalPremium = report.SoldItems
                    .Where(s => s.MarketPrice > 0)
                    .Sum(s => s.Gold - (s.MarketPrice * s.Count));
                double pctPremium = totalSaleGold > 0 ? (totalPremium / totalSaleGold) * 100 : 0;

                string saleNetText = $"+{totalSaleGold}d";
                string saleHeader;
                if (totalPremium > 0 && (exactMode || estimateMode))
                {
                    string premStr = exactMode
                        ? $"+{(int)Math.Round(pctPremium)}% (+{(int)Math.Round(totalPremium)}d)"
                        : $"~{(int)Math.Round(pctPremium)}% premium";
                    saleHeader = $"{simPrefix}Trade Optimizer @ {traderName} — Sales: {saleNetText} | Premium: {premStr}";
                }
                else
                {
                    saleHeader = $"{simPrefix}Trade Optimizer @ {traderName} — Sales: {saleNetText}";
                }

                InformationManager.DisplayMessage(new InformationMessage(saleHeader, Color.FromUint(0x40FF40FF))); // Green
                TradingEngine.WriteLog(saleHeader);

                string soldLabel = isSim ? "  Would sell: " : "  Sold: ";
                var soldList = report.SoldItems.Select(s => $"{s.Count}x {s.Name} (+{s.Gold}d)");
                string soldMsg = soldLabel + string.Join(", ", soldList);
                InformationManager.DisplayMessage(new InformationMessage(soldMsg));
                TradingEngine.WriteLog("  " + soldMsg.Trim());
            }

            // ─── Purchases Report ────────────────────────────────────────────────
            if (report.BoughtItems.Count > 0)
            {
                int totalBuyGold = report.BoughtItems.Sum(b => b.Gold);
                double totalDiscount = report.BoughtItems
                    .Where(b => b.MarketPrice > 0)
                    .Sum(b => (b.MarketPrice * b.Count) - b.Gold);
                double pctDiscount = totalBuyGold > 0 ? (totalDiscount / totalBuyGold) * 100 : 0;

                string buyNetText = $"-{totalBuyGold}d";
                string buyHeader;
                if (totalDiscount > 0 && (exactMode || estimateMode))
                {
                    string discStr = exactMode
                        ? $"+{(int)Math.Round(pctDiscount)}% (-{(int)Math.Round(totalDiscount)}d saved)"
                        : $"~{(int)Math.Round(pctDiscount)}% discount";
                    buyHeader = $"{simPrefix}Trade Optimizer @ {traderName} — Purchases: {buyNetText} | Discount: {discStr}";
                }
                else
                {
                    buyHeader = $"{simPrefix}Trade Optimizer @ {traderName} — Purchases: {buyNetText}";
                }

                InformationManager.DisplayMessage(new InformationMessage(buyHeader, Color.FromUint(0xFF9900FF))); // Amber
                TradingEngine.WriteLog(buyHeader);

                string buyLabel = isSim ? "  Would buy: " : "  Bought: ";
                var boughtList = report.BoughtItems.Select(b => $"{b.Count}x {b.Name} (-{b.Gold}d)");
                string buyMsg = buyLabel + string.Join(", ", boughtList);
                InformationManager.DisplayMessage(new InformationMessage(buyMsg));
                TradingEngine.WriteLog("  " + buyMsg.Trim());
            }
        }

        /// <summary>
        /// Prints final cargo capacity status. Call this only AFTER all automation phases have
        /// completed so the weight reading reflects the final inventory state.
        /// </summary>
        public static void PrintCargoStatus(bool isSim = false)
        {
            if (TaleWorlds.CampaignSystem.Party.MobileParty.MainParty == null) return;
            float curWeight = GetRosterWeight(TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster);
            float capacity = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.InventoryCapacity;
            int pct = (int)Math.Round((curWeight / capacity) * 100);
            string cargoLabel = isSim ? "  Current cargo" : "  Cargo";
            string cargoMsg = $"{cargoLabel}: {(int)curWeight} / {(int)capacity} capacity ({pct}%)";
            InformationManager.DisplayMessage(new InformationMessage(cargoMsg));
            TradingEngine.WriteLog("  " + cargoMsg.Trim());
        }

        private static float GetRosterWeight(TaleWorlds.CampaignSystem.Roster.ItemRoster roster)
        {
            if (roster == null) return 0f;
            float weight = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item != null)
                {
                    var item = element.EquipmentElement.Item;
                    if (item.IsAnimal || item.IsMountable)
                    {
                        continue;
                    }
                    weight += item.Weight * element.Amount;
                }
            }
            return weight;
        }
    }
}
