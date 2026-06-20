using System;

namespace PartyManager
{
    internal static class PartyLogisticsPlanner
    {
        public static int CalculateRidingMountReserve(int footTroops, int freePartySlots)
        {
            return Math.Max(0, footTroops) + Math.Max(0, freePartySlots);
        }

        public static int CalculateProcessableRidingMounts(
            int ridingMounts,
            int footTroops,
            int freePartySlots,
            SellRidingMountsMode mode,
            bool preserveFootReserve)
        {
            if (ridingMounts <= 0 || mode == SellRidingMountsMode.Never)
            {
                return 0;
            }

            if (!preserveFootReserve && mode == SellRidingMountsMode.All)
            {
                return ridingMounts;
            }

            int reserve = preserveFootReserve
                ? CalculateRidingMountReserve(footTroops, freePartySlots)
                : Math.Max(0, footTroops);

            return Math.Max(0, ridingMounts - reserve);
        }

        public static int CalculateHerdingPenaltyPercent(int partySize, int herdSize)
        {
            if (herdSize <= partySize)
            {
                return 0;
            }

            if (partySize <= 0)
            {
                return 80;
            }

            float excessRatio = (herdSize - partySize) / (float)partySize;
            float penalty = Math.Min(0.8f, 0.3f * excessRatio);
            return (int)Math.Round(penalty * 100f);
        }

        public static int CalculateOverburdenPenaltyPercent(float weightCarried, float inventoryCapacity)
        {
            if (inventoryCapacity <= 0f || weightCarried <= inventoryCapacity)
            {
                return 0;
            }

            float penalty = 0.4f * weightCarried / inventoryCapacity;
            return (int)Math.Round(penalty * 100f);
        }
    }
}
