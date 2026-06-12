using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TradingOptimizer
{
    public static class TradingPatches
    {
        public static SPInventoryVM? ActiveInventoryVM { get; private set; }

        public static void SPInventoryVMConstructorPostfix(SPInventoryVM __instance)
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
                if (settings != null && CampaignTime.Now.ToDays < settings.InitialSettlementDaysDelay)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[TradingOptimizer] Manual trade disabled during economy settling period (Day {CampaignTime.Now.ToDays:F1}/{settings.InitialSettlementDaysDelay})"
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
                InformationManager.DisplayMessage(new InformationMessage($"Trading Optimizer Error: {ex.Message}"));
            }
        }

        public static void PrintTradeReport(int finalGold, int initialGold, TradeTransactionReport report, string traderName)
        {
            bool isSim = Settings.Instance?.SimulationMode ?? false;
            string simPrefix = isSim ? "[Simulation] " : "";
            int netProfit = finalGold - initialGold;

            if (report.SoldItems.Count == 0 && report.BoughtItems.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{simPrefix}Trading Optimizer: Visited {traderName} - No profitable trades found."
                ));
                return;
            }

            string profitText = netProfit >= 0 ? $"+{netProfit}d" : $"{netProfit}d";
            uint msgColor = netProfit >= 0 ? 0x40FF40FF : 0xFF4040FF; // Green or Red
            string tradeVerb = isSim ? "WOULD trade with" : "Auto-traded with";

            InformationManager.DisplayMessage(new InformationMessage(
                $"{simPrefix}Trading Optimizer: {tradeVerb} {traderName}! Profit: {profitText}",
                Color.FromUint(msgColor)
            ));

            if (report.SoldItems.Count > 0)
            {
                string wouldSell = isSim ? "Would sell: " : "  Sold: ";
                var sb = new StringBuilder(wouldSell);
                sb.Append(string.Join(", ", report.SoldItems.Select(s => $"{s.Count} {s.Name} (+{s.Gold}d)")));
                InformationManager.DisplayMessage(new InformationMessage(sb.ToString()));
            }

            if (report.BoughtItems.Count > 0)
            {
                string wouldBuy = isSim ? "Would buy:  " : "  Bought: ";
                var sb = new StringBuilder(wouldBuy);
                sb.Append(string.Join(", ", report.BoughtItems.Select(b => $"{b.Count} {b.Name} (-{b.Gold}d)")));
                InformationManager.DisplayMessage(new InformationMessage(sb.ToString()));
            }

            if (TaleWorlds.CampaignSystem.Party.MobileParty.MainParty != null)
            {
                float curWeight = GetRosterWeight(TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster);
                float capacity = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.InventoryCapacity;
                int pct = (int)Math.Round((curWeight / capacity) * 100);
                string cargoLabel = isSim ? "  Current cargo" : "  Cargo";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{cargoLabel}: {(int)curWeight} / {(int)capacity} capacity ({pct}%)"
                ));
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
