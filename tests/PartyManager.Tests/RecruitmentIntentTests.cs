using System.Linq;
using PartyManager;
using SettlementAutomationCore;
using Xunit;

namespace PartyManager.Tests
{
    public class RecruitmentIntentTests
    {
        [Fact]
        public void SortRecruitmentCandidates_VolunteersFirst_PutsNotablesBeforeMercenaries()
        {
            var mercenary = Candidate(SettlementRecruitmentSource.TavernMercenary, unitCost: 20, sequence: 0);
            var volunteer = Candidate(SettlementRecruitmentSource.NotableVolunteer, unitCost: 50, sequence: 1);

            var sorted = PartyManagerProvider.SortRecruitmentCandidates(
                new[] { mercenary, volunteer },
                RecruitHireOrder.VolunteersFirst);

            Assert.Same(volunteer, sorted[0]);
            Assert.Same(mercenary, sorted[1]);
        }

        [Fact]
        public void SortRecruitmentCandidates_MercenariesFirst_PutsMercenariesBeforeNotables()
        {
            var volunteer = Candidate(SettlementRecruitmentSource.NotableVolunteer, unitCost: 20, sequence: 0);
            var mercenary = Candidate(SettlementRecruitmentSource.TavernMercenary, unitCost: 50, sequence: 1);

            var sorted = PartyManagerProvider.SortRecruitmentCandidates(
                new[] { volunteer, mercenary },
                RecruitHireOrder.MercenariesFirst);

            Assert.Same(mercenary, sorted[0]);
            Assert.Same(volunteer, sorted[1]);
        }

        [Fact]
        public void SortRecruitmentCandidates_CheapestFirst_UsesUnitCostThenNativeOrder()
        {
            var expensive = Candidate(SettlementRecruitmentSource.NotableVolunteer, unitCost: 80, sequence: 0);
            var cheapLater = Candidate(SettlementRecruitmentSource.TavernMercenary, unitCost: 20, sequence: 2);
            var cheapEarlier = Candidate(SettlementRecruitmentSource.NotableVolunteer, unitCost: 20, sequence: 1);

            var sorted = PartyManagerProvider.SortRecruitmentCandidates(
                new[] { expensive, cheapLater, cheapEarlier },
                RecruitHireOrder.CheapestFirst);

            Assert.Equal(new[] { cheapEarlier, cheapLater, expensive }, sorted.ToArray());
        }

        [Fact]
        public void Settings_DefaultRecruitHireOrder_IsBestCandidatesFirst()
        {
            var settings = new Settings();

            Assert.Equal(RecruitHireOrder.BestCandidatesFirst, settings.RecruitHireOrderSetting);
        }

        private static SettlementRecruitmentCandidate Candidate(
            SettlementRecruitmentSource source,
            int unitCost,
            int sequence)
        {
            return new SettlementRecruitmentCandidate(
                source,
                null!,
                availableCount: 1,
                unitCost: unitCost,
                notable: null,
                slotIndex: -1,
                sequence: sequence);
        }
    }
}
