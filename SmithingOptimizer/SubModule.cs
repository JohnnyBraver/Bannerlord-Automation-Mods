using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using Bannerlord.UIExtenderEx;
using SettlementAutomationCore;

namespace SmithingOptimizer
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony? HarmonyInstance { get; private set; }
        private static UIExtender? _uiExtender;
        private static bool _uiExtenderInitialized = false;
        private static SmithingOptimizerProvider? _provider;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                HarmonyInstance = new Harmony("com.smithing.optimizer");
                
                // Do manual patching for WeaponDesignVM constructor to avoid Harmony annotation issues in v1.4.5
                var targetConstructor = typeof(WeaponDesignVM).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(CraftingPatches).GetMethod("OnWeaponDesignVMConstructed", BindingFlags.Public | BindingFlags.Static);
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
                        "SmithingOptimizer_Error.txt"
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
                _uiExtender = UIExtender.Create("SmithingOptimizer");
                _uiExtender.Register(typeof(SubModule).Assembly);
                _uiExtender.Enable();
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                UnregisterAutomationHooks();

                _provider = new SmithingOptimizerProvider();
                AutomationRegistry.RegisterRequestProvider(_provider);
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
            if (_provider == null)
            {
                return;
            }

            AutomationRegistry.UnregisterRequestProvider(_provider);
            AutomationRegistry.UnregisterReportProvider(_provider);
            _provider = null;
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            // Keybind removed; button injection via UIExtenderEx is now the trigger.
        }

        private void TriggerManualOptimization()
        {
            // Delegate to the patch instance
            CraftingPatches.ManualTrigger();
        }
    }
}
