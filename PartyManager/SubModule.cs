using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
                var campaignStarter = gameStarter as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    campaignStarter.AddBehavior(new PartyManagerCampaignBehavior());
                }

                _provider = new PartyManagerProvider();
                AutomationRegistry.RegisterTradeProvider(_provider);
                AutomationRegistry.RegisterRecruitProvider(_provider);
                AutomationRegistry.RegisterGarrisonProvider(_provider);
                AutomationRegistry.RegisterRansomProvider(_provider);
                AutomationRegistry.RegisterDungeonProvider(_provider);
                AutomationRegistry.RegisterGoalProvider(_provider);

                SettlementAutomationCore.SubModule.OnAutomationCycleCompleted += OnAutomationCycleCompleted;
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
                AutomationRegistry.UnregisterDungeonProvider(_provider);
                AutomationRegistry.UnregisterGoalProvider(_provider);
                _provider = null;
            }

            SettlementAutomationCore.SubModule.OnAutomationCycleCompleted -= OnAutomationCycleCompleted;
        }

        private static void OnAutomationCycleCompleted(Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null) return;

            var party = MobileParty.MainParty;
            if (party == null || party.PrisonRoster == null) return;

            int prisonerCount = party.PrisonRoster.TotalManCount;
            int prisonerSizeLimit = party.Party.PrisonerSizeLimit;

            // 1. Capacity Alert
            if (settings.PrisonerCapacityAlertPercent > 0 && prisonerSizeLimit > 0)
            {
                int percentFill = (prisonerCount * 100) / prisonerSizeLimit;
                if (percentFill >= settings.PrisonerCapacityAlertPercent)
                {
                    string msg = $"Prisoner capacity alert: {prisonerCount}/{prisonerSizeLimit} prisoners ({percentFill}% fill).";
                    InformationManager.DisplayMessage(new InformationMessage($"[PartyManager] WARNING: {msg}", new Color(0.9f, 0.6f, 0.2f)));
                }
            }

            // 2. Stack Size Alert
            int threshold = int.MaxValue;
            if (settings.PrisonerStackAlertFlatLimit > 0)
            {
                threshold = Math.Min(threshold, settings.PrisonerStackAlertFlatLimit);
            }
            if (settings.PrisonerStackAlertPercentLimit > 0 && prisonerSizeLimit > 0)
            {
                int percentThreshold = Math.Max(1, (settings.PrisonerStackAlertPercentLimit * prisonerSizeLimit) / 100);
                threshold = Math.Min(threshold, percentThreshold);
            }

            if (threshold != int.MaxValue)
            {
                var highStacks = new List<string>();
                var prisonRoster = party.PrisonRoster;
                for (int i = 0; i < prisonRoster.Count; i++)
                {
                    var el = prisonRoster.GetElementCopyAtIndex(i);
                    if (el.Character != null && el.Number >= threshold)
                    {
                        highStacks.Add($"{el.Character.Name} (x{el.Number})");
                    }
                }

                if (highStacks.Count > 0)
                {
                    string msg = $"High count prisoner stack(s) detected: {string.Join(", ", highStacks)}";
                    InformationManager.DisplayMessage(new InformationMessage($"[PartyManager] WARNING: {msg}", new Color(0.9f, 0.6f, 0.2f)));
                }
            }
        }
    }

    public class PartyManagerCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            var settings = Settings.Instance;
            if (settings == null) return;

            // Only run if player was involved in the map event and their side won
            if (mapEvent.PlayerSide == BattleSideEnum.None) return;
            if (mapEvent.WinningSide != mapEvent.PlayerSide) return;

            var party = MobileParty.MainParty;
            if (party == null) return;

            // 1. Animal Auto-Slaughter
            if (settings.PostBattleSlaughterSetting != PostBattleSlaughterMode.None)
            {
                int maxAllowed = HerdingCalculator.GetMaxAnimalsAllowed(party);
                int currentAnimals = HerdingCalculator.GetCurrentAnimalsCount(party);

                if (currentAnimals > maxAllowed)
                {
                    int excess = currentAnimals - maxAllowed;
                    InformationManager.DisplayMessage(new InformationMessage($"[PartyManager] Herding detected post-battle. Current: {currentAnimals}, Max Allowed: {maxAllowed}. Starting auto-slaughter..."));
                    SlaughterAnimalsField(party, excess, settings.PostBattleSlaughterSetting);
                }
            }

            // 2. Prisoner Auto-Discard
            if (settings.AutoDiscardPrisonersPostBattle)
            {
                int currentCount = party.PrisonRoster.TotalManCount;
                int limit = party.Party.PrisonerSizeLimit;

                if (currentCount > limit)
                {
                    int excess = currentCount - limit;
                    int discardedTotal = 0;
                    var prisonRoster = party.PrisonRoster;

                    // Collect all non-hero candidates matching settings.DiscardPrisonersUpToTier
                    var candidates = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>();
                    for (int i = 0; i < prisonRoster.Count; i++)
                    {
                        var el = prisonRoster.GetElementCopyAtIndex(i);
                        if (el.Character != null && !el.Character.IsHero && el.Character.Tier <= settings.DiscardPrisonersUpToTier && el.Number > 0)
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
                        InformationManager.DisplayMessage(new InformationMessage($"[PartyManager] {msg}"));
                        SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", msg);
                    }
                }
            }
        }

        private void SlaughterAnimalsField(MobileParty party, int excess, PostBattleSlaughterMode mode)
        {
            var itemRoster = party.ItemRoster;
            var candidates = new List<AnimalSlaughterCandidate>();

            for (int i = 0; i < itemRoster.Count; i++)
            {
                var el = itemRoster.GetElementCopyAtIndex(i);
                var item = el.EquipmentElement.Item;
                if (item == null || el.Amount <= 0) continue;

                bool isLivestock = item.IsAnimal;
                bool isPack = item.IsMountable && item.HorseComponent != null && item.HorseComponent.IsPackAnimal;
                bool isRiding = item.IsMountable && item.HorseComponent != null && !item.HorseComponent.IsPackAnimal;

                bool allowed = false;
                int categoryPriority = 99; // Lower is slaughtered first

                if (isLivestock && (mode == PostBattleSlaughterMode.Livestock || mode == PostBattleSlaughterMode.LivestockAndPack || mode == PostBattleSlaughterMode.All))
                {
                    allowed = true;
                    categoryPriority = 1;
                }
                else if (isPack && (mode == PostBattleSlaughterMode.LivestockAndPack || mode == PostBattleSlaughterMode.All))
                {
                    allowed = true;
                    categoryPriority = 2;
                }
                else if (isRiding && mode == PostBattleSlaughterMode.All)
                {
                    allowed = true;
                    categoryPriority = 3;
                }

                if (allowed)
                {
                    candidates.Add(new AnimalSlaughterCandidate(item, el.Amount, item.Value, categoryPriority));
                }
            }

            // Sort: 1. Category Priority (Livestock -> Pack -> Riding), 2. Value (cheapest first)
            var sortedCandidates = candidates
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.Value)
                .ToList();

            foreach (var cand in sortedCandidates)
            {
                if (excess <= 0) break;
                int toSlaughter = Math.Min(excess, cand.Amount);
                if (toSlaughter > 0)
                {
                    // Remove animals
                    itemRoster.AddToCounts(cand.Item, -toSlaughter);

                    // Add yield
                    int meatCount = cand.Item.HorseComponent.MeatCount;
                    int hideCount = cand.Item.HorseComponent.HideCount;
                    if (meatCount > 0)
                    {
                        itemRoster.AddToCounts(DefaultItems.Meat, meatCount * toSlaughter);
                    }
                    if (hideCount > 0)
                    {
                        itemRoster.AddToCounts(DefaultItems.Hides, hideCount * toSlaughter);
                    }

                    excess -= toSlaughter;
                    InformationManager.DisplayMessage(new InformationMessage($"[PartyManager] Field slaughtered {toSlaughter}x {cand.Item.Name} (Yielded {meatCount * toSlaughter}x Meat, {hideCount * toSlaughter}x Hides)"));
                }
            }
        }

        public override void SyncData(IDataStore dataStore) { }

        private class AnimalSlaughterCandidate
        {
            public ItemObject Item { get; }
            public int Amount { get; }
            public int Value { get; }
            public int Priority { get; }
            public AnimalSlaughterCandidate(ItemObject item, int amount, int value, int priority)
            {
                Item = item;
                Amount = amount;
                Value = value;
                Priority = priority;
            }
        }
    }
}
