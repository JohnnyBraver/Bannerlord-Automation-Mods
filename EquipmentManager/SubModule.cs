using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using Bannerlord.UIExtenderEx;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace EquipmentManager
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
                HarmonyInstance = new Harmony("com.equipment.manager");
                
                // Do manual patching for SPInventoryVM constructor to avoid Harmony annotation issues in v1.4.5
                var targetConstructor = typeof(SPInventoryVM).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(EquipmentPatches).GetMethod("OnSPInventoryVMConstructed", BindingFlags.Public | BindingFlags.Static);
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
                        "EquipmentManager_Error.txt"
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
                _uiExtender = UIExtender.Create("EquipmentManager");
                _uiExtender.Register(typeof(SubModule).Assembly);
                _uiExtender.Enable();
            }
        }

        private static EquipmentManagerProvider? _provider;

        protected override void OnGameStart(TaleWorlds.Core.Game game, TaleWorlds.Core.IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                _provider = new EquipmentManagerProvider();
                AutomationRegistry.RegisterPreSellProvider(_provider);
                AutomationRegistry.RegisterEquipmentUpgradeProvider(_provider);

                var campaignStarter = gameStarter as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    campaignStarter.AddBehavior(new EquipmentManagerCampaignBehavior());
                }
            }
        }

        public override void OnGameEnd(TaleWorlds.Core.Game game)
        {
            base.OnGameEnd(game);
            if (_provider != null)
            {
                AutomationRegistry.UnregisterPreSellProvider(_provider);
                AutomationRegistry.UnregisterEquipmentUpgradeProvider(_provider);
                _provider = null;
            }
        }
    }

    public class EquipmentManagerCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            try
            {
                if (mapEvent.PlayerSide == BattleSideEnum.None) return;
                if (mapEvent.WinningSide != mapEvent.PlayerSide) return;

                if (TaleWorlds.CampaignSystem.Party.MobileParty.MainParty != null)
                {
                    EquipmentEngine.AutoEquipHeadless(TaleWorlds.CampaignSystem.Party.MobileParty.MainParty);
                }
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in EquipmentManagerCampaignBehavior.OnMapEventEnded: {ex}");
            }
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
