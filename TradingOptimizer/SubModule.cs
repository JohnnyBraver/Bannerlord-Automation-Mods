using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Inventory;
using Bannerlord.UIExtenderEx;

namespace TradingOptimizer
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony? HarmonyInstance { get; private set; }
        private static UIExtender? _uiExtender;
        private static bool _uiExtenderInitialized = false;

        private static Settlement? _pendingBackgroundTradeSettlement = null;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                HarmonyInstance = new Harmony("com.trading.optimizer");
                
                // Do manual patching for SPInventoryVM constructor to avoid Harmony annotation issues in v1.4.5
                var targetConstructor = typeof(SPInventoryVM).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(TradingPatches).GetMethod("SPInventoryVMConstructorPostfix", BindingFlags.Public | BindingFlags.Static);
                    if (postfixMethod != null)
                    {
                        HarmonyInstance.Patch(targetConstructor, postfix: new HarmonyMethod(postfixMethod));
                    }
                }

                // Patch everything else (OnFinalizePostfix)
                HarmonyInstance.PatchAll();
            }
            catch (Exception ex)
            {
                try
                {
                    string path = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Mount and Blade II Bannerlord",
                        "Configs",
                        "TradingOptimizer_Error.txt"
                    );
                    System.IO.File.WriteAllText(path, ex.ToString());
                }
                catch
                {
                    // Ignore nested errors
                }
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (!_uiExtenderInitialized)
            {
                _uiExtenderInitialized = true;
                _uiExtender = UIExtender.Create("TradingOptimizer");
                _uiExtender.Register(typeof(SubModule).Assembly);
                _uiExtender.Enable();
            }
        }


        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                var campaignStarter = gameStarter as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    campaignStarter.AddBehavior(new TradingCampaignBehavior());
                }
            }
        }

        public static void QueueBackgroundTrade(Settlement settlement)
        {
            _pendingBackgroundTradeSettlement = settlement;
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            // 1. Process pending background trade
            if (_pendingBackgroundTradeSettlement != null)
            {
                var sett = _pendingBackgroundTradeSettlement;
                _pendingBackgroundTradeSettlement = null;
                ExecuteBackgroundTrade(sett);
            }

            // Manual trigger via in-screen button (ExecuteAutoTrade on SPInventoryVMMixin)
            // Keybind removed; button injection via UIExtenderEx is now the trigger.
        }

        private static IMarketData? GetMarketData(Settlement settlement)
        {
            if (settlement.IsTown) return settlement.Town?.MarketData;
            if (settlement.IsVillage) return settlement.Village?.Bound?.Town?.MarketData;
            return null;
        }

        private static InventoryLogic? CreateAndInitInventoryLogic(Settlement settlement)
        {
            if (settlement == null || MobileParty.MainParty == null || Hero.MainHero == null) return null;
            try
            {
                var logic = new InventoryLogic(MobileParty.MainParty, Hero.MainHero.CharacterObject, settlement.Party);

                var initMethod = typeof(InventoryLogic).GetMethods()
                    .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 13);

                if (initMethod == null) return null;

                var categoryTypeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryCategoryType");
                var modeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryMode");
                if (categoryTypeEnum == null || modeEnum == null) return null;

                var categoryTypeAll = Enum.Parse(categoryTypeEnum, "All");
                var modeTrade = Enum.Parse(modeEnum, "Trade");

                initMethod.Invoke(logic, new object[] {
                    settlement.ItemRoster,
                    MobileParty.MainParty.ItemRoster,
                    MobileParty.MainParty.MemberRoster,
                    true, // isTrading
                    false, // isSpecialActionsPermitted
                    Hero.MainHero.CharacterObject,
                    categoryTypeAll,
                    GetMarketData(settlement)!,
                    false, // useBasePrices
                    modeTrade,
                    settlement.Name,
                    null!, // leftMemberRoster
                    null! // otherSideCapacityData
                });

                return logic;
            }
            catch (Exception ex)
            {
                TradingEngine.WriteLog($"[InventoryLogic Init Error] {ex}");
                return null;
            }
        }
        private static void ExecuteBackgroundTrade(Settlement settlement)
        {
            if (settlement == null || MobileParty.MainParty == null || Hero.MainHero == null) return;

            try
            {
                var settings = Settings.Instance;
                bool shouldSplit = settings?.ShouldSplitTransactions ?? false;

                if (shouldSplit)
                {
                    var soldNormalItems = new HashSet<string>();

                    // Transaction 1: Sell Phase
                    int initialGold = Hero.MainHero.Gold;
                    var logic1 = CreateAndInitInventoryLogic(settlement);
                    if (logic1 != null)
                    {
                        Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
                        var vm1 = new SPInventoryVM(logic1, false, dummyFunc);
                        var report1 = TradingEngine.RunOptimization(vm1, isSellPhase: true, isBuyPhase: false) ?? new TradeTransactionReport();
                        
                        if (report1.SoldNormalItems != null)
                        {
                            foreach (var item in report1.SoldNormalItems)
                            {
                                soldNormalItems.Add(item);
                            }
                        }

                        if (logic1.IsThereAnyChanges())
                        {
                            if (settings != null && settings.SimulationMode)
                            {
                                int netGoldChange = report1.SoldItems.Sum(s => s.Gold);
                                TradingPatches.PrintTradeReport(initialGold + netGoldChange, initialGold, report1, settlement.Name.ToString() + " (Sell)");
                            }
                            else
                            {
                                if (logic1.DoneLogic())
                                {
                                    TradingPatches.PrintTradeReport(Hero.MainHero.Gold, initialGold, report1, settlement.Name.ToString() + " (Sell)");
                                }
                            }
                        }
                    }

                    // Transaction 2: Buy Phase
                    int goldBeforeBuy = Hero.MainHero.Gold;
                    var logic2 = CreateAndInitInventoryLogic(settlement);
                    if (logic2 != null)
                    {
                        Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
                        var vm2 = new SPInventoryVM(logic2, false, dummyFunc);
                        var report2 = TradingEngine.RunOptimization(vm2, isSellPhase: false, isBuyPhase: true, excludedItems: soldNormalItems) ?? new TradeTransactionReport();
                        if (logic2.IsThereAnyChanges())
                        {
                            if (settings != null && settings.SimulationMode)
                            {
                                int netGoldChange = -report2.BoughtItems.Sum(b => b.Gold);
                                TradingPatches.PrintTradeReport(goldBeforeBuy + netGoldChange, goldBeforeBuy, report2, settlement.Name.ToString() + " (Buy)");
                            }
                            else
                            {
                                if (logic2.DoneLogic())
                                {
                                    TradingPatches.PrintTradeReport(Hero.MainHero.Gold, goldBeforeBuy, report2, settlement.Name.ToString() + " (Buy)");
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Single transaction for both Sell and Buy
                    var logic = CreateAndInitInventoryLogic(settlement);
                    if (logic == null) return;
                    Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
                    var vm = new SPInventoryVM(logic, false, dummyFunc);
                    int initialGold = Hero.MainHero.Gold;
                    var report = TradingEngine.RunOptimization(vm, isSellPhase: true, isBuyPhase: true) ?? new TradeTransactionReport();
                    if (logic.IsThereAnyChanges())
                    {
                        if (settings != null && settings.SimulationMode)
                        {
                            int netGoldChange = report.SoldItems.Sum(s => s.Gold) - report.BoughtItems.Sum(b => b.Gold);
                            int hypotheticalFinalGold = initialGold + netGoldChange;
                            TradingPatches.PrintTradeReport(hypotheticalFinalGold, initialGold, report, settlement.Name.ToString());
                        }
                        else
                        {
                            if (logic.DoneLogic())
                            {
                                int finalGold = Hero.MainHero.Gold;
                                TradingPatches.PrintTradeReport(finalGold, initialGold, report, settlement.Name.ToString());
                            }
                        }
                    }
                    else
                    {
                        TradingPatches.PrintTradeReport(initialGold, initialGold, report, settlement.Name.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    TradingEngine.WriteLog($"[Background Error] Exception in ExecuteBackgroundTrade: {ex}");
                }
                catch {}
            }
        }
    }

    public class TradingCampaignBehavior : CampaignBehaviorBase
    {
        private string _lastVisitedSettlementId = "";

        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (party == MobileParty.MainParty && settlement != null && (settlement.IsTown || settlement.IsVillage))
            {
                var settings = Settings.Instance;
                if (settings != null && settings.AutoTradeOnEnterSettlement && settlement.StringId != _lastVisitedSettlementId)
                {
                    _lastVisitedSettlementId = settlement.StringId;
                    
                    // Check if enough campaign days have passed to let the initial economy stabilize
                    if (CampaignTime.Now.ToDays < settings.InitialSettlementDaysDelay)
                    {
                        TradingEngine.WriteLog($"[Settling Period] Skipped auto-trade for {settlement.Name} (Campaign Day {CampaignTime.Now.ToDays:F1} < Settling Limit {settings.InitialSettlementDaysDelay})");
                        return;
                    }

                    SubModule.QueueBackgroundTrade(settlement);
                }
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_lastVisitedSettlementId", ref _lastVisitedSettlementId);
            }
            catch (Exception)
            {
                // Ignore sync errors
            }
        }
    }
}
