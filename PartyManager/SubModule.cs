using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using SettlementAutomationCore;
using PartyManager.Helpers;

namespace PartyManager
{
    public class SubModule : MBSubModuleBase
    {
        private static PartyManagerProvider? _provider;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                _provider = new PartyManagerProvider();
                AutomationRegistry.RegisterSettlementRecruitmentProvider(_provider);
                AutomationRegistry.RegisterGarrisonProvider(_provider);
                AutomationRegistry.RegisterPrisonerDispositionProvider(_provider);
                AutomationRegistry.RegisterRequestProvider(_provider);
                AutomationRegistry.RegisterSettlementCleanupProvider(_provider);
                AutomationRegistry.RegisterReservationProvider(_provider);
                AutomationRegistry.RegisterPostBattleProvider(_provider);

                SettlementAutomationCore.SubModule.OnAutomationCycleCompleted += OnAutomationCycleCompleted;
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            if (_provider != null)
            {
                AutomationRegistry.UnregisterSettlementRecruitmentProvider(_provider);
                AutomationRegistry.UnregisterGarrisonProvider(_provider);
                AutomationRegistry.UnregisterPrisonerDispositionProvider(_provider);
                AutomationRegistry.UnregisterRequestProvider(_provider);
                AutomationRegistry.UnregisterSettlementCleanupProvider(_provider);
                AutomationRegistry.UnregisterReservationProvider(_provider);
                AutomationRegistry.UnregisterPostBattleProvider(_provider);
                _provider = null;
            }

            SettlementAutomationCore.SubModule.OnAutomationCycleCompleted -= OnAutomationCycleCompleted;
        }

        private static void OnAutomationCycleCompleted(Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled) return;
            PrisonerHelper.ProcessPostAutomationAlerts(settlement, settings);
        }
    }
}
