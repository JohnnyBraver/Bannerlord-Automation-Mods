using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace SettlementAutomationCore.Helpers
{
    public static class TroopHelper
    {
        public static void GetLeafTroops(CharacterObject troop, List<CharacterObject> leafTroops)
        {
            if (troop.UpgradeTargets == null || troop.UpgradeTargets.Length == 0)
            {
                leafTroops.Add(troop);
                return;
            }
            foreach (var target in troop.UpgradeTargets)
            {
                if (target != null)
                {
                    GetLeafTroops(target, leafTroops);
                }
            }
        }
    }
}
