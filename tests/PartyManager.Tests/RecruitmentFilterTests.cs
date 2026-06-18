using PartyManager.Filters;
using Xunit;

namespace PartyManager.Tests
{
    public class RecruitmentFilterTests
    {
        [Fact]
        public void IsAnyRoleEnabled_AllowsShieldInfantryWithThrowingWhenSkirmishersDisabled()
        {
            var settings = DefaultSettings();
            settings.RecruitShieldInfantry = true;
            settings.RecruitSkirmishers = false;

            bool allowed = RecruitmentFilter.IsAnyRoleEnabled(
                RecruitmentFilter.RecruitmentRole.FrontlineInfantry | RecruitmentFilter.RecruitmentRole.Skirmisher,
                settings);

            Assert.True(allowed);
        }

        [Fact]
        public void IsAnyRoleEnabled_RejectsSkirmisherOnlyWhenSkirmishersDisabled()
        {
            var settings = DefaultSettings();
            settings.RecruitShieldInfantry = true;
            settings.RecruitSkirmishers = false;

            bool allowed = RecruitmentFilter.IsAnyRoleEnabled(
                RecruitmentFilter.RecruitmentRole.Skirmisher,
                settings);

            Assert.False(allowed);
        }

        [Fact]
        public void ClassifyRole_TreatsShieldThrowersAsFrontline()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: false,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: true,
                hasShield: true,
                hasPike: false,
                hasLargeSwingable: false);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.FrontlineInfantry, role);
        }

        [Fact]
        public void ClassifyRole_TreatsStableLowTierTwoHandersAsShockInfantry()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: false,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: false,
                hasShield: false,
                hasPike: false,
                hasLargeSwingable: true);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.ShockInfantry, role);
        }

        [Fact]
        public void ClassifyRole_TreatsSkilledTwoHandersAsShockInfantry()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: false,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: true,
                hasShield: false,
                hasPike: false,
                hasLargeSwingable: true);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.ShockInfantry, role);
        }

        [Fact]
        public void ClassifyRole_TreatsJavelinInfantryWithoutStrongerRoleAsSkirmisher()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: false,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: true,
                hasShield: false,
                hasPike: false,
                hasLargeSwingable: false);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.Skirmisher, role);
        }

        [Fact]
        public void ClassifyRole_PrefersShockInfantryOverJavelin()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: false,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: true,
                hasShield: false,
                hasPike: false,
                hasLargeSwingable: true);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.ShockInfantry, role);
        }

        [Fact]
        public void ClassifyRole_TreatsPikesAsPikeInfantry()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: false,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: false,
                hasShield: false,
                hasPike: true,
                hasLargeSwingable: false);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.PikeInfantry, role);
        }

        [Fact]
        public void IsAnyRoleEnabled_AllowsPikeInfantryWhenFrontlineDisabled()
        {
            var settings = DefaultSettings();
            settings.RecruitShieldInfantry = false;
            settings.RecruitPikeInfantry = true;

            bool allowed = RecruitmentFilter.IsAnyRoleEnabled(
                RecruitmentFilter.RecruitmentRole.PikeInfantry,
                settings);

            Assert.True(allowed);
        }

        [Fact]
        public void ClassifyRole_DoesNotTreatMountedThrowingAsHorseArcher()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: true,
                hasBow: false,
                hasCrossbow: false,
                hasJavelin: true,
                hasShield: false,
                hasPike: false,
                hasLargeSwingable: false);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.MeleeCavalry, role);
        }

        [Fact]
        public void ClassifyRole_PrefersHorseArcherForMountedBows()
        {
            var role = RecruitmentFilter.ClassifyRole(
                isMounted: true,
                hasBow: true,
                hasCrossbow: false,
                hasJavelin: false,
                hasShield: false,
                hasPike: false,
                hasLargeSwingable: false);

            Assert.Equal(RecruitmentFilter.RecruitmentRole.HorseArcher, role);
        }

        private static Settings DefaultSettings()
        {
            return new Settings
            {
                RecruitShieldInfantry = false,
                RecruitShockInfantry = false,
                RecruitSkirmishers = false,
                RecruitFootArchers = false,
                RecruitCrossbowmen = false,
                RecruitMeleeCavalry = false,
                RecruitHorseArchers = false,
                RecruitPikeInfantry = false
            };
        }
    }
}
