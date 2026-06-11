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
        private static TradingOptimizerProvider? _provider;

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                _provider = new TradingOptimizerProvider();
                TradingCore.TradeCoordinator.RegisterProvider(_provider);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            if (_provider != null)
            {
                TradingCore.TradeCoordinator.UnregisterProvider(_provider);
                _provider = null;
            }
        }
    }
}
