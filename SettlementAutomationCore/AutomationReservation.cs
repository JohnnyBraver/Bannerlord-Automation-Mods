using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace SettlementAutomationCore
{
    public enum AutomationReservationFlow
    {
        Settlement,
        PostBattle
    }

    public sealed class AutomationReservationContext
    {
        public AutomationReservationContext(
            AutomationReservationFlow flow,
            MobileParty party,
            Settlement? settlement = null,
            MapEvent? mapEvent = null)
        {
            Flow = flow;
            Party = party;
            Settlement = settlement;
            MapEvent = mapEvent;
        }

        public AutomationReservationFlow Flow { get; }
        public MobileParty Party { get; }
        public Settlement? Settlement { get; }
        public MapEvent? MapEvent { get; }
    }

    public interface IAutomationReservationProvider
    {
        string ProviderName { get; }
        System.Collections.Generic.IReadOnlyList<ItemReservation> GetReservations(AutomationReservationContext context);
    }
}
