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
using SettlementAutomationCore;

namespace TradeOptimizer
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony? HarmonyInstance { get; private set; }
        private static UIExtender? _uiExtender;
        private static bool _uiExtenderInitialized = false;
        public static string Version { get; private set; } = "vUnknown";


        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            var assemblyVersion = typeof(SubModule).Assembly.GetName().Version;
            Version = $"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
            try
            {
                TradingEngine.WriteLog($"[SubModule] Loaded Trade Optimizer {Version}");
            }
            catch { }

            try
            {
                HarmonyInstance = new Harmony("com.trading.optimizer");
                
                // Do manual patching for SPInventoryVM constructor to avoid Harmony annotation issues in v1.4.5
                var targetConstructor = typeof(SPInventoryVM).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(TradingPatches).GetMethod("OnSPInventoryVMConstructed", BindingFlags.Public | BindingFlags.Static);
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
                        "TradeOptimizer_Error.txt"
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
                _uiExtender = UIExtender.Create("TradeOptimizer");
                _uiExtender.Register(typeof(SubModule).Assembly);
                _uiExtender.Enable();
            }
        }
        private static TradeOptimizerProvider? _provider;

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                UnregisterAutomationHooks();

                _provider = new TradeOptimizerProvider();
                AutomationRegistry.RegisterPreSellProvider(_provider);
                AutomationRegistry.RegisterFreeTradeAnalyzer(_provider);
                AutomationRegistry.RegisterReportProvider(_provider);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            UnregisterAutomationHooks();
        }

        private static void UnregisterAutomationHooks()
        {
            if (_provider != null)
            {
                AutomationRegistry.UnregisterPreSellProvider(_provider);
                AutomationRegistry.UnregisterFreeTradeAnalyzer(_provider);
                AutomationRegistry.UnregisterReportProvider(_provider);
                _provider = null;
            }
        }
    }
}
