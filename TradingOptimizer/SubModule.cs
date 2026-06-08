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

namespace TradingOptimizer
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony? HarmonyInstance { get; private set; }

        private static Settlement? _pendingBackgroundTradeSettlement = null;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Settings.Load();

            try
            {
                HarmonyInstance = new Harmony("com.trading.optimizer");

                // Manually patch the single constructor of SPInventoryVM
                var targetConstructor = typeof(SPInventoryVM).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(TradingPatches).GetMethod(nameof(TradingPatches.SPInventoryVMConstructorPostfix), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (postfixMethod != null)
                    {
                        HarmonyInstance.Patch(targetConstructor, postfix: new HarmonyMethod(postfixMethod));
                    }
                }

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

            // 2. Handle Manual keybind trigger (Ctrl + T) when inventory is active
            if (TradingPatches.ActiveInventoryVM != null)
            {
                if (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
                {
                    if (Enum.TryParse<InputKey>(Settings.Instance.Keybind, true, out var targetKey))
                    {
                        if (Input.IsKeyReleased(targetKey))
                        {
                            TradingPatches.ManualTrigger();
                        }
                    }
                }
            }
        }

        private static IMarketData? GetMarketData(Settlement settlement)
        {
            if (settlement.IsTown) return settlement.Town?.MarketData;
            if (settlement.IsVillage) return settlement.Village?.Bound?.Town?.MarketData;
            return null;
        }

        private static void ExecuteBackgroundTrade(Settlement settlement)
        {
            if (settlement == null || MobileParty.MainParty == null || Hero.MainHero == null) return;

            try
            {
                // Create InventoryLogic
                var logic = new InventoryLogic(MobileParty.MainParty, Hero.MainHero.CharacterObject, settlement.Party);

                // Find the Initialize method with 13 arguments
                var initMethod = typeof(InventoryLogic).GetMethods()
                    .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 13);

                if (initMethod == null) return;

                var categoryTypeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryCategoryType");
                var modeEnum = typeof(InventoryLogic).Assembly.GetType("Helpers.InventoryScreenHelper+InventoryMode");
                if (categoryTypeEnum == null || modeEnum == null) return;

                var categoryTypeAll = Enum.Parse(categoryTypeEnum, "All");
                var modeTrade = Enum.Parse(modeEnum, "Trade");

                // Call Initialize
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

                // Create SPInventoryVM in memory
                Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
                var vm = new SPInventoryVM(logic, false, dummyFunc);

                int initialGold = Hero.MainHero.Gold;

                // Run optimization
                var report = TradingEngine.RunOptimization(vm, isSellPhase: true, isBuyPhase: true);

                // Commit changes if any exist
                if (logic.IsThereAnyChanges())
                {
                    bool success = logic.DoneLogic();
                    if (success)
                    {
                        int finalGold = Hero.MainHero.Gold;
                        TradingPatches.PrintTradeReport(finalGold, initialGold, report, settlement.Name.ToString());
                    }
                }
                else
                {
                    // Report no trades found
                    TradingPatches.PrintTradeReport(initialGold, initialGold, report, settlement.Name.ToString());
                }
            }
            catch (Exception)
            {
                // Ignore background trade errors
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
                if (Settings.Instance.AutoTradeOnEnterSettlement && settlement.StringId != _lastVisitedSettlementId)
                {
                    _lastVisitedSettlementId = settlement.StringId;
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
