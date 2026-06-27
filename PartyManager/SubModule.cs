using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
            BoatHelper.AutoBuyBoats(settlement, settings);
            ProcessPostAutomationSpeedWarnings(MobileParty.MainParty, settings);
        }

        private static void ProcessPostAutomationSpeedWarnings(MobileParty party, Settings settings)
        {
            if (party == null || settings == null) return;

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                out _, out _, out _);
            int partySize = infantry + cavalry;
            int herdSize = pack + livestock + Math.Max(0, riding - infantry);
            int herdingPenalty = PartyLogisticsPlanner.CalculateHerdingPenaltyPercent(partySize, herdSize);
            if (settings.HerdingWarningThresholdPercent > 0 && herdingPenalty >= settings.HerdingWarningThresholdPercent)
            {
                string msg = $"herding still slows the party by about {herdingPenalty}% after cleanup; settings or item locks may be protecting the remaining animals";
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage($"[PartyManager] WARNING: {msg}", new TaleWorlds.Library.Color(0.9f, 0.6f, 0.2f)));
            }

            int cargoPenalty = PartyLogisticsPlanner.CalculateOverburdenPenaltyPercent(
                party.TotalWeightCarried,
                party.InventoryCapacity);
            if (settings.CargoWarningThresholdPercent > 0 && cargoPenalty >= settings.CargoWarningThresholdPercent)
            {
                string msg = $"cargo overburden still slows the party by about {cargoPenalty}% after cleanup";
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage($"[PartyManager] WARNING: {msg}", new TaleWorlds.Library.Color(0.9f, 0.6f, 0.2f)));
            }
        }
    }
}
