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

            // Cast enum values to check IsAnyRoleEnabled using int cast or helper
            bool allowed = RecruitmentFilter.IsAnyRoleEnabled(
                (RecruitmentFilter.RecruitmentRole)((int)RecruitmentFilter.RecruitmentRole.ShieldInfantry | (int)RecruitmentFilter.RecruitmentRole.Skirmisher),
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

        [Theory]
        [InlineData(TroopClassifier.TroopRole.LightInfantry, 1 << 0)]
        [InlineData(TroopClassifier.TroopRole.ShieldInfantry, 1 << 1)]
        [InlineData(TroopClassifier.TroopRole.ShockInfantry, 1 << 2)]
        [InlineData(TroopClassifier.TroopRole.Skirmisher, 1 << 3)]
        [InlineData(TroopClassifier.TroopRole.FootArcher, 1 << 4)]
        [InlineData(TroopClassifier.TroopRole.Crossbowman, 1 << 5)]
        [InlineData(TroopClassifier.TroopRole.MeleeCavalry, 1 << 6)]
        [InlineData(TroopClassifier.TroopRole.HorseArcher, 1 << 7)]
        [InlineData(TroopClassifier.TroopRole.PikeInfantry, 1 << 8)]
        [InlineData(TroopClassifier.TroopRole.SpearInfantry, 1 << 9)]
        [InlineData(TroopClassifier.TroopRole.MountedSkirmisher, 1 << 10)]
        public void MapTroopRole_MapsToCorrectRecruitmentRole(TroopClassifier.TroopRole input, int expected)
        {
            var mapped = RecruitmentFilter.MapTroopRole(input);
            Assert.Equal(expected, (int)mapped);
        }

        [Fact]
        public void IsAnyRoleEnabled_AllowsPikeInfantryWhenShieldInfantryDisabled()
        {
            var settings = DefaultSettings();
            settings.RecruitShieldInfantry = false;
            settings.RecruitPikeInfantry = true;

            bool allowed = RecruitmentFilter.IsAnyRoleEnabled(
                RecruitmentFilter.RecruitmentRole.PikeInfantry,
                settings);

            Assert.True(allowed);
        }

        private static Settings DefaultSettings()
        {
            return new Settings
            {
                RecruitLightInfantry = false,
                RecruitShieldInfantry = false,
                RecruitSpearInfantry = false,
                RecruitShockInfantry = false,
                RecruitPikeInfantry = false,
                RecruitSkirmishers = false,
                RecruitFootArchers = false,
                RecruitCrossbowmen = false,
                RecruitMeleeCavalry = false,
                RecruitMountedSkirmisher = false,
                RecruitHorseArchers = false
            };
        }
    }
}
