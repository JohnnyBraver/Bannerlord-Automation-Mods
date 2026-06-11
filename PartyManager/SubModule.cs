using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using SettlementAutomationCore;

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
                AutomationRegistry.RegisterTradeProvider(_provider);
                AutomationRegistry.RegisterRecruitProvider(_provider);
                AutomationRegistry.RegisterGarrisonProvider(_provider);
                AutomationRegistry.RegisterRansomProvider(_provider);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            if (_provider != null)
            {
                AutomationRegistry.UnregisterTradeProvider(_provider);
                AutomationRegistry.UnregisterRecruitProvider(_provider);
                AutomationRegistry.UnregisterGarrisonProvider(_provider);
                AutomationRegistry.UnregisterRansomProvider(_provider);
                _provider = null;
            }
        }
    }
}
