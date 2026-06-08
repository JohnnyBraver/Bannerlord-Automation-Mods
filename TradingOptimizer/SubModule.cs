using System;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TradingOptimizer
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony? HarmonyInstance { get; private set; }

        private static float _reopenTimer = -1f;
        private static bool _autoOpenScheduled = false;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Settings.Load();

            try
            {
                HarmonyInstance = new Harmony("com.trading.optimizer");
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

        public static void TriggerAutoOpen()
        {
            _autoOpenScheduled = true;
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            // 1. Tick the patches for frame waiting logic
            TradingPatches.TickUpdate();

            // 2. Handle Auto Reopen (between Sell and Buy stages)
            if (TradingPatches.LoopState == AutoLoopState.WaitingForReopen && TradingPatches.ActiveInventoryVM == null)
            {
                if (_reopenTimer < 0)
                {
                    _reopenTimer = 0.5f; // Wait 0.5 seconds before reopening
                }
                else
                {
                    _reopenTimer -= dt;
                    if (_reopenTimer <= 0)
                    {
                        _reopenTimer = -1f;
                        if (Settlement.CurrentSettlement != null)
                        {
                            InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
                        }
                        else
                        {
                            // If player left the settlement, reset to Idle
                            TradingPatches.LoopState = AutoLoopState.Idle;
                        }
                    }
                }
            }

            // 3. Handle Auto Open on Entering Settlement
            if (_autoOpenScheduled)
            {
                _autoOpenScheduled = false;
                if (Settlement.CurrentSettlement != null)
                {
                    InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
                }
            }

            // 4. Handle Manual keybind trigger (Ctrl + Keybind)
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
                    SubModule.TriggerAutoOpen();
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
