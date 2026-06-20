using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace SettlementAutomationCore
{
    public sealed class PostBattleAutomationContext
    {
        public PostBattleAutomationContext(
            MobileParty party,
            MapEvent mapEvent,
            IEnumerable<ItemReservation>? itemReservations = null)
        {
            Party = party;
            MapEvent = mapEvent;
            ItemReservations = new List<ItemReservation>(itemReservations ?? new List<ItemReservation>());
        }

        public MobileParty Party { get; }
        public MapEvent MapEvent { get; }
        public IReadOnlyList<ItemReservation> ItemReservations { get; }
    }

    public sealed class PostBattleActivity
    {
        public PostBattleActivity(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }

    public sealed class PostBattleAutomationResult
    {
        private readonly List<PostBattleActivity> _activities = new List<PostBattleActivity>();

        public IReadOnlyList<PostBattleActivity> Activities => _activities;

        public bool HasActivity => _activities.Count > 0;

        public void AddActivity(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _activities.Add(new PostBattleActivity(message));
            }
        }
    }

    public interface IPostBattleAutomationProvider
    {
        string ProviderName { get; }
        PostBattleAutomationResult ProcessPostBattle(PostBattleAutomationContext context);
    }

}
