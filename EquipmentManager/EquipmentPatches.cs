using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace EquipmentManager
{
    public static class EquipmentPatches
    {
        public static SPInventoryVM? ActiveInventoryVM { get; private set; }

        public static void SPInventoryVMConstructorPostfix(SPInventoryVM __instance)
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
}
