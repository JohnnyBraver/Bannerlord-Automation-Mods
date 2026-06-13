using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;

namespace EquipmentManager
{
    public static class EquipmentPatches
    {
        public static SPInventoryVM? ActiveInventoryVM { get; private set; }

        public static void OnSPInventoryVMConstructed(SPInventoryVM __instance)
        {
            ActiveInventoryVM = __instance;
        }

        [HarmonyPatch(typeof(SPInventoryVM), "OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalizePostfix()
        {
            ActiveInventoryVM = null;
        }

        public static void ManualTrigger()
        {
            if (ActiveInventoryVM == null) return;
            try
            {
                EquipmentEngine.OptimizeEquipment(ActiveInventoryVM);
            }
            catch (Exception)
            {
                // Ignore manual error
            }
        }
    }

    [HarmonyPatch(typeof(SettlementAutomationCore.SubModule), "ExecuteBackgroundAutomation")]
    public static class ExecuteBackgroundAutomationPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Settlement settlement)
        {
            try
            {
                if (MobileParty.MainParty != null)
                {
                    string settlementName = settlement != null ? settlement.Name.ToString() : "Unknown";
                    SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"[Post-Transaction] Distributing newly bought gear to player & companions in {settlementName}.");
                    EquipmentEngine.AutoEquipHeadless(MobileParty.MainParty, "Post-Transaction");
                }
            }
            catch (Exception ex)
            {
                SettlementAutomationCore.Helpers.Logger.WriteLog("EquipmentManager", $"Error in ExecuteBackgroundAutomation Postfix: {ex}");
            }
        }
    }
}

