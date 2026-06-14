using System;

namespace PartyManager
{
    internal static class RecruitmentCapacityPlanner
    {
        public static int GetMaxRecruitablePartySize(
            int partySizeLimit,
            int recruitUpToPartySizePercent,
            bool enableGarrisonDonation,
            int currentGarrisonSize,
            int maxGarrisonSize)
        {
            int normalLimit = (int)Math.Ceiling(partySizeLimit * Math.Max(1, Math.Min(100, recruitUpToPartySizePercent)) / 100.0);
            normalLimit = Math.Min(partySizeLimit, normalLimit);
            if (normalLimit < partySizeLimit)
            {
                return normalLimit;
            }

            return partySizeLimit + GetAvailableGarrisonDonationSpace(enableGarrisonDonation, currentGarrisonSize, maxGarrisonSize);
        }

        public static int GetAvailableGarrisonDonationSpace(
            bool enableGarrisonDonation,
            int currentGarrisonSize,
            int maxGarrisonSize)
        {
            if (!enableGarrisonDonation)
            {
                return 0;
            }

            return Math.Max(0, maxGarrisonSize - currentGarrisonSize);
        }
    }
}

