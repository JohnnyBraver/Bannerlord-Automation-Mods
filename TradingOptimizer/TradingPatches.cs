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

namespace TradingOptimizer
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
            int netProfit = finalGold - initialGold;

            if (report.SoldItems.Count == 0 && report.BoughtItems.Count == 0)
            {
                string noTradeMsg = $"{simPrefix}Trade Optimizer: Visited {traderName} - No profitable trades found.";
                InformationManager.DisplayMessage(new InformationMessage(noTradeMsg));
                TradingEngine.WriteLog(noTradeMsg);
                return;
            }

            // Calculate premium and discount
            double totalPremium = 0;
            double totalDiscount = 0;
            foreach (var s in report.SoldItems)
            {
                if (s.MarketPrice > 0)
                {
                    totalPremium += s.Gold - (s.MarketPrice * s.Count);
                }
            }
            foreach (var b in report.BoughtItems)
            {
                if (b.MarketPrice > 0)
                {
                    totalDiscount += (b.MarketPrice * b.Count) - b.Gold;
                }
            }
            double totalAdvantage = totalPremium + totalDiscount;

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
            bool alwaysLocal = settings?.PricingReference == PricingReferenceMode.AlwaysLocal;

            bool exactMode = alwaysGlobal || (hasTier1 && hasTier2);
            bool estimateMode = alwaysLocal || hasTier1;

            double totalTradedValue = report.SoldItems.Sum(s => s.Gold) + report.BoughtItems.Sum(b => b.Gold);
            double pctAdvantage = totalTradedValue > 0 ? (totalAdvantage / totalTradedValue) * 100 : 0;

            string profitText = netProfit >= 0 ? $"+{netProfit}d" : $"{netProfit}d";
            string headlineMsg;
            if (totalAdvantage > 0)
            {
                string advantageStr = exactMode ? $"+{(int)Math.Round(pctAdvantage)}% (+{(int)Math.Round(totalAdvantage)}d)" : $"~{(int)Math.Round(pctAdvantage)}%";
                headlineMsg = $"{simPrefix}Trade Optimizer: {traderName}! Net Gold: {profitText} | Market Advantage: {advantageStr}";
            }
            else
            {
                headlineMsg = $"{simPrefix}Trade Optimizer: {traderName}! Net Gold: {profitText}";
            }

            uint msgColor = netProfit >= 0 ? 0x40FF40FF : 0xFF4040FF; // Green or Red
            InformationManager.DisplayMessage(new InformationMessage(headlineMsg, Color.FromUint(msgColor)));
            TradingEngine.WriteLog(headlineMsg);

            if (report.SoldItems.Count > 0)
            {
                string label = isSim ? "Would sell: " : "  Sold: ";
                var itemsList = report.SoldItems.Select(s => $"{s.Count}x {s.Name} (+{s.Gold}d)");
                string msg = label + string.Join(", ", itemsList);
                InformationManager.DisplayMessage(new InformationMessage(msg));
                TradingEngine.WriteLog("  " + msg.Trim());
            }

            if (report.BoughtItems.Count > 0)
            {
                string label = isSim ? "Would buy:  " : "  Bought: ";
                var itemsList = report.BoughtItems.Select(b => $"{b.Count}x {b.Name} (-{b.Gold}d)");
                string msg = label + string.Join(", ", itemsList);
                InformationManager.DisplayMessage(new InformationMessage(msg));
                TradingEngine.WriteLog("  " + msg.Trim());
            }

            if (TaleWorlds.CampaignSystem.Party.MobileParty.MainParty != null)
            {
                float curWeight = GetRosterWeight(TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster);
                float capacity = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.InventoryCapacity;
                int pct = (int)Math.Round((curWeight / capacity) * 100);
                string cargoLabel = isSim ? "  Current cargo" : "  Cargo";
                string cargoMsg = $"{cargoLabel}: {(int)curWeight} / {(int)capacity} capacity ({pct}%)";
                InformationManager.DisplayMessage(new InformationMessage(cargoMsg));
                TradingEngine.WriteLog("  " + cargoMsg.Trim());
            }
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
