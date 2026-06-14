using PartyManager;
using Xunit;

namespace PartyManager.Tests
{
    public class RecruitmentCapacityPlannerTests
    {
        [Fact]
        public void GetMaxRecruitablePartySize_RespectsPercentBelowFullPartyLimit()
        {
            int limit = RecruitmentCapacityPlanner.GetMaxRecruitablePartySize(
                partySizeLimit: 120,
                recruitUpToPartySizePercent: 75,
                enableGarrisonDonation: true,
                currentGarrisonSize: 10,
                maxGarrisonSize: 100);

            Assert.Equal(90, limit);
        }

        [Fact]
        public void GetMaxRecruitablePartySize_AllowsGarrisonOverRecruitingOnlyAtFullPercent()
        {
            int limit = RecruitmentCapacityPlanner.GetMaxRecruitablePartySize(
                partySizeLimit: 120,
                recruitUpToPartySizePercent: 100,
                enableGarrisonDonation: true,
                currentGarrisonSize: 80,
                maxGarrisonSize: 100);

            Assert.Equal(140, limit);
        }

        [Fact]
        public void GetAvailableGarrisonDonationSpace_ReturnsZeroWhenDisabled()
        {
            Assert.Equal(0, RecruitmentCapacityPlanner.GetAvailableGarrisonDonationSpace(false, 10, 100));
        }
    }
}

