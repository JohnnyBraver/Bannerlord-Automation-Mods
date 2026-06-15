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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
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
                UnregisterAutomationHooks();

                _provider = new EquipmentManagerProvider();
                AutomationRegistry.RegisterPreparationProvider(_provider);
                AutomationRegistry.RegisterPreSellProvider(_provider);
                AutomationRegistry.RegisterRequestProvider(_provider);
                SettlementAutomationCore.SubModule.OnAutomationCycleCompleted -= OnAutomationCycleCompleted;
                SettlementAutomationCore.SubModule.OnAutomationCycleCompleted += OnAutomationCycleCompleted;

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
            UnregisterAutomationHooks();
        }

        private static void UnregisterAutomationHooks()
        {
            if (_provider != null)
            {
                AutomationRegistry.UnregisterPreparationProvider(_provider);
                AutomationRegistry.UnregisterPreSellProvider(_provider);
                AutomationRegistry.UnregisterRequestProvider(_provider);
                _provider = null;
            }

            SettlementAutomationCore.SubModule.OnAutomationCycleCompleted -= OnAutomationCycleCompleted;
        }

        private static void OnAutomationCycleCompleted(Settlement settlement)
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party == null) return;

                string settlementName = settlement != null ? settlement.Name.ToString() : "Unknown";
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"[Post-Transaction] Distributing newly bought gear to player & companions in {settlementName}.");
                EquipmentEngine.AutoEquipHeadless(party, "Post-Transaction");
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in OnAutomationCycleCompleted AutoEquipHeadless: {ex}");
            }
        }
    }

    public class EquipmentManagerCampaignBehavior : CampaignBehaviorBase
    {
        private readonly PostLootAutoEquipTrigger _postLootTrigger = new PostLootAutoEquipTrigger();

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.ItemsLooted.AddNonSerializedListener(this, OnItemsLooted);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            try
            {
                _postLootTrigger.OnBattleEnded(
                    mapEvent.PlayerSide != BattleSideEnum.None &&
                    mapEvent.WinningSide == mapEvent.PlayerSide);
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in EquipmentManagerCampaignBehavior.OnMapEventEnded: {ex}");
            }
        }

        private void OnItemsLooted(MobileParty party, ItemRoster items)
        {
            try
            {
                if (party == null || party != MobileParty.MainParty) return;
                if (!_postLootTrigger.OnItemsLooted(true, ContainsEquipment(items))) return;

                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", "Post-battle equipment loot detected. Auto-equip queued for the next campaign tick.");
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in EquipmentManagerCampaignBehavior.OnItemsLooted: {ex}");
            }
        }

        private void OnTick(float dt)
        {
            try
            {
                if (!_postLootTrigger.ShouldRunOnTick()) return;
                var party = MobileParty.MainParty;
                if (party == null) return;

                EquipmentEngine.AutoEquipHeadless(party, "Post-Loot");
            }
            catch (Exception ex)
            {
                _postLootTrigger.ClearPending();
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in EquipmentManagerCampaignBehavior.OnTick: {ex}");
            }
        }

        private static bool ContainsEquipment(ItemRoster items)
        {
            if (items == null) return false;

            for (int i = 0; i < items.Count; i++)
            {
                var element = items.GetElementCopyAtIndex(i);
                var item = element.EquipmentElement.Item;
                if (item == null || element.Amount <= 0) continue;

                if (item.HasArmorComponent || item.WeaponComponent != null || item.PrimaryWeapon != null)
                {
                    return true;
                }
            }

            return false;
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
