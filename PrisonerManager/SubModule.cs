using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using SettlementAutomationCore;

namespace PrisonerManager
{
    public class SubModule : MBSubModuleBase
    {
        private static PrisonerManagerProvider? _provider;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (game.GameType is Campaign)
            {
                _provider = new PrisonerManagerProvider();
                AutomationRegistry.RegisterRansomProvider(_provider);
                AutomationRegistry.RegisterDungeonProvider(_provider);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            if (_provider != null)
            {
                AutomationRegistry.UnregisterRansomProvider(_provider);
                AutomationRegistry.UnregisterDungeonProvider(_provider);
                _provider = null;
            }
        }
    }
}
