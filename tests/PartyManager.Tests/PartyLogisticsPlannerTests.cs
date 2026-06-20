using PartyManager;
using Xunit;

namespace PartyManager.Tests
{
    public class PartyLogisticsPlannerTests
    {
        [Theory]
        [InlineData(10, 5, 15)]
        [InlineData(10, -2, 10)]
        [InlineData(-1, 5, 5)]
        public void CalculateRidingMountReserve_CoversFootTroopsAndFreeSlots(int footTroops, int freeSlots, int expected)
        {
            Assert.Equal(expected, PartyLogisticsPlanner.CalculateRidingMountReserve(footTroops, freeSlots));
        }

        [Theory]
        [InlineData(20, 10, 5, SellRidingMountsMode.ExcessOnly, true, 5)]
        [InlineData(12, 10, 5, SellRidingMountsMode.ExcessOnly, true, 0)]
        [InlineData(20, 10, 5, SellRidingMountsMode.All, true, 5)]
        [InlineData(20, 10, 5, SellRidingMountsMode.All, false, 20)]
        [InlineData(20, 10, 5, SellRidingMountsMode.Never, true, 0)]
        public void CalculateProcessableRidingMounts_PreservesFootReserve(
            int ridingMounts,
            int footTroops,
            int freeSlots,
            SellRidingMountsMode mode,
            bool preserveReserve,
            int expected)
        {
            Assert.Equal(
                expected,
                PartyLogisticsPlanner.CalculateProcessableRidingMounts(ridingMounts, footTroops, freeSlots, mode, preserveReserve));
        }

        [Theory]
        [InlineData(100, 100, 0)]
        [InlineData(100, 110, 3)]
        [InlineData(100, 200, 30)]
        [InlineData(10, 100, 80)]
        public void CalculateHerdingPenaltyPercent_MatchesGameFormula(int partySize, int herdSize, int expected)
        {
            Assert.Equal(expected, PartyLogisticsPlanner.CalculateHerdingPenaltyPercent(partySize, herdSize));
        }

        [Theory]
        [InlineData(1000f, 1000f, 0)]
        [InlineData(1250f, 1000f, 50)]
        [InlineData(1500f, 1000f, 60)]
        public void CalculateOverburdenPenaltyPercent_MatchesGameFormula(float weight, float capacity, int expected)
        {
            Assert.Equal(expected, PartyLogisticsPlanner.CalculateOverburdenPenaltyPercent(weight, capacity));
        }
    }
}
