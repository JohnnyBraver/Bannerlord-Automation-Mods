using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using SettlementAutomationCore;
using PartyManager.Filters;

namespace PartyManager.Helpers
{
    public static class PrisonerHelper
    {
        public static List<RansomOrder> GetRansomOrders(MobileParty party, Settlement settlement, Settings settings)
        {
            var orders = new List<RansomOrder>();
            if (settings == null || !settings.AutoRansomPrisoners) return orders;

            var prisonRoster = party.PrisonRoster;
            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var el = prisonRoster.GetElementCopyAtIndex(i);
                var prisoner = el.Character;
                if (prisoner == null || el.Number <= 0) continue;

                // 1. Keep Heroes check
                if (prisoner.IsHero && settings.KeepHeroPrisoners) continue;

                // 2. Keep Filter check: If we keep it, we do NOT ransom it.
                if (PrisonerFilter.MatchKeepFilter(prisoner, settings)) continue;

                orders.Add(new RansomOrder(prisoner, el.Number));
            }

            if (orders.Count > 0)
            {
                var summary = string.Join(", ", orders.Select(o => $"{o.Amount}x {o.Prisoner.Name}"));
                SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"Prisoner Ransom Orders compiled for {settlement.Name}: {summary}");
            }

            return orders;
        }

        public static List<DungeonOrder> GetDungeonOrders(MobileParty party, Settlement settlement, Settings settings)
        {
            var orders = new List<DungeonOrder>();
            if (settings == null || !settings.AutoDonatePrisoners) return orders;

            var prisonRoster = party.PrisonRoster;
            var candidates = new List<PrisonerInfo>();

            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var el = prisonRoster.GetElementCopyAtIndex(i);
                var prisoner = el.Character;
                if (prisoner == null || el.Number <= 0 || prisoner.IsHero) continue; // Never auto-donate heroes

                // Match keep filter first: if we want to KEEP them, do NOT donate them
                if (PrisonerFilter.MatchKeepFilter(prisoner, settings)) continue;

                if (prisoner.Tier >= settings.MinDonateTier)
                {
                    candidates.Add(new PrisonerInfo(prisoner, el.Number));
                }
            }

            // Sort candidates: high-tier first or standard
            if (settings.PrioritizeHighTierDonation)
            {
                candidates = candidates.OrderByDescending(c => c.Prisoner.Tier).ToList();
            }
            else
            {
                candidates = candidates.OrderBy(c => c.Prisoner.Tier).ToList();
            }

            foreach (var candidate in candidates)
            {
                orders.Add(new DungeonOrder(candidate.Prisoner, candidate.Amount));
            }

            if (orders.Count > 0)
            {
                var summary = string.Join(", ", orders.Select(o => $"{o.Amount}x {o.Prisoner.Name}"));
                SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"Prisoner Dungeon Donation Orders compiled for {settlement.Name}: {summary}");
            }

            return orders;
        }

        public static void ProcessPostBattleDiscard(MobileParty party, Settings settings)
        {
            if (settings == null || !settings.AutoDiscardPrisonersPostBattle) return;

            int currentCount = party.PrisonRoster.TotalManCount;
            int limit = party.Party.PrisonerSizeLimit;

            if (currentCount > limit)
            {
                int excess = currentCount - limit;
                int discardedTotal = 0;
                var prisonRoster = party.PrisonRoster;

                // Collect all non-hero candidates matching discard policy
                var candidates = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>();
                for (int i = 0; i < prisonRoster.Count; i++)
                {
                    var el = prisonRoster.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero && el.Number > 0)
                    {
                        bool isCandidate = false;

                        if (settings.UsePerkBasedPrisonerDiscard && Hero.MainHero != null)
                        {
                            bool stoutDefender = Hero.MainHero.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Leadership.StoutDefender);
                            bool ferventAttacker = Hero.MainHero.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Leadership.FerventAttacker);

                            if (stoutDefender && ferventAttacker)
                            {
                                isCandidate = el.Character.Tier <= settings.DiscardPrisonersUpToTier;
                            }
                            else if (stoutDefender)
                            {
                                // Protect T4-6, discard T1-3 (subject to standard discard limits)
                                isCandidate = el.Character.Tier <= settings.DiscardPrisonersUpToTier && el.Character.Tier < 4;
                            }
                            else if (ferventAttacker)
                            {
                                // Protect T1-3, discard T4-6 (noble bypass protects noble prisoners)
                                bool isNoble = el.Character.Tier >= 6;
                                bool protectNoble = isNoble && settings.BypassNoblePrisonerTierLimit;
                                if (!protectNoble)
                                {
                                    isCandidate = el.Character.Tier >= 4;
                                }
                            }
                            else
                            {
                                isCandidate = el.Character.Tier <= settings.DiscardPrisonersUpToTier;
                            }
                        }
                        else
                        {
                            isCandidate = el.Character.Tier <= settings.DiscardPrisonersUpToTier;
                        }

                        if (isCandidate)
                        {
                            candidates.Add(el);
                        }
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

        public static void ProcessPostAutomationAlerts(Settlement settlement, Settings settings)
        {
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
            if (settings.PrisonerStackAlertPercentLimit > 0 && prisonerSizeLimit > 0)
            {
                int percentThreshold = Math.Max(1, (settings.PrisonerStackAlertPercentLimit * prisonerSizeLimit) / 100);
                var highStacks = new List<string>();
                var prisonRoster = party.PrisonRoster;
                for (int i = 0; i < prisonRoster.Count; i++)
                {
                    var el = prisonRoster.GetElementCopyAtIndex(i);
                    if (el.Character != null && el.Number >= percentThreshold)
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

        private class PrisonerInfo
        {
            public CharacterObject Prisoner { get; }
            public int Amount { get; }
            public PrisonerInfo(CharacterObject prisoner, int amount)
            {
                Prisoner = prisoner;
                Amount = amount;
            }
        }
    }
}
