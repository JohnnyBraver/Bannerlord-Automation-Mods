using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

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

                // Manually patch the single constructor of SPInventoryVM
                var targetConstructor = typeof(SPInventoryVM).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(EquipmentPatches).GetMethod(nameof(EquipmentPatches.SPInventoryVMConstructorPostfix), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (postfixMethod != null)
                    {
                        HarmonyInstance.Patch(targetConstructor, postfix: new HarmonyMethod(postfixMethod));
                    }
                }

                HarmonyInstance.PatchAll();
            }
            catch (Exception ex)
            {
                try
                {
                    string path = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Mount and Blade II Bannerlord",
                        "Configs",
                        "EquipmentManager_Error.txt"
                    );
                    System.IO.File.WriteAllText(path, ex.ToString());
                }
                catch
                {
                    // Ignore nested errors
                }
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
