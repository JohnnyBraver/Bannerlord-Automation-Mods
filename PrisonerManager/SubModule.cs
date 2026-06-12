using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
                var campaignStarter = gameStarter as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    campaignStarter.AddBehavior(new PrisonerManagerCampaignBehavior());
                }

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

    public class PrisonerManagerCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.AutoDiscardPostBattle) return;

            // Only run if player was involved in the map event and won
            if (mapEvent.PlayerSide == BattleSideEnum.None) return;
            if (mapEvent.WinningSide != mapEvent.PlayerSide) return;

            var party = MobileParty.MainParty;
            if (party == null) return;

            int currentCount = party.PrisonRoster.TotalManCount;
            int limit = party.Party.PrisonerSizeLimit;

            if (currentCount > limit)
            {
                int excess = currentCount - limit;
                int discardedTotal = 0;
                var prisonRoster = party.PrisonRoster;

                // Collect all non-hero candidates matching settings.DiscardUpToTier
                var candidates = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>();
                for (int i = 0; i < prisonRoster.Count; i++)
                {
                    var el = prisonRoster.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero && el.Character.Tier <= settings.DiscardUpToTier && el.Number > 0)
                    {
                        candidates.Add(el);
                    }
                }

                // Sort candidates by value approximation: mounted units and higher tier units are sorted last (retaining them)
                var sortedCandidates = candidates.OrderBy(c => {
                    // Lower tiers are discarded first
                    int baseScore = c.Character.Tier * 100;
                    
                    // Mounted units have higher value, so we add score to protect them
                    if (c.Character.IsMounted)
                    {
                        baseScore += 50;
                    }
                    
                    // Nobles are extremely valuable
                    var leafTroops = new List<CharacterObject>();
                    SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(c.Character, leafTroops);
                    int maxLeafTier = leafTroops.Count > 0 ? leafTroops.Max(l => l.Tier) : c.Character.Tier;
                    if (maxLeafTier >= 6)
                    {
                        baseScore += 500;
                    }
                    
                    return baseScore;
                }).ToList();

                foreach (var cand in sortedCandidates)
                {
                    if (excess <= 0) break;
                    int toDiscard = Math.Min(excess, cand.Number);
                    if (toDiscard > 0)
                    {
                        prisonRoster.AddToCounts(cand.Character, -toDiscard);
                        excess -= toDiscard;
                        discardedTotal += toDiscard;
                    }
                }

                if (discardedTotal > 0)
                {
                    string msg = $"Auto-discarded {discardedTotal} low-value prisoners post-battle due to party capacity limits.";
                    InformationManager.DisplayMessage(new InformationMessage($"[PrisonerManager] {msg}"));
                    SettlementAutomationCore.Helpers.Logger.WriteLog("PrisonerManager", msg);
                }
            }
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
