using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace EquipmentManager
{
    [HarmonyPatch]
    public static class EquipmentPatches
    {
        public static SPInventoryVM? ActiveInventoryVM { get; private set; }

        // Manually patched in SubModule.OnSubModuleLoad
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
