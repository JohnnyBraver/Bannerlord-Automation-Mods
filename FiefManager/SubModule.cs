using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using SettlementAutomationCore;

namespace FiefManager
{
    public class SubModule : MBSubModuleBase
    {
        private static FiefManagerProvider? _provider;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                _provider = new FiefManagerProvider();
                AutomationRegistry.RegisterFiefProvider(_provider);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            if (_provider != null)
            {
                AutomationRegistry.UnregisterFiefProvider(_provider);
                _provider = null;
            }
        }
    }
}
