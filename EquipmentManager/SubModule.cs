using System;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace EquipmentManager
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony? HarmonyInstance { get; private set; }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Settings.Load();

            try
            {
                HarmonyInstance = new Harmony("com.equipment.manager");
                HarmonyInstance.PatchAll();
            }
            catch (Exception)
            {
                // Ignore patch errors
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            // Manual keybind trigger (Ctrl + Keybind) when inventory is active
            if (EquipmentPatches.ActiveInventoryVM != null)
            {
                if (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
                {
                    if (Enum.TryParse<InputKey>(Settings.Instance.Keybind, true, out var targetKey))
                    {
                        if (Input.IsKeyReleased(targetKey))
                        {
                            EquipmentPatches.ManualTrigger();
                        }
                    }
                }
            }
        }
    }
}
