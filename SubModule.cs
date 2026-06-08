using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;

namespace SmithingOptimizer
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
                HarmonyInstance = new Harmony("com.smithing.optimizer");

                // Manually patch the single constructor of WeaponDesignVM
                var targetConstructor = typeof(WeaponDesignVM).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                if (targetConstructor != null)
                {
                    var postfixMethod = typeof(CraftingPatches).GetMethod(nameof(CraftingPatches.WeaponDesignVMConstructorPostfix), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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
                        "SmithingOptimizer_Error.txt"
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

            // Check if we are holding Ctrl (Left or Right)
            if (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
            {
                if (Enum.TryParse<InputKey>(Settings.Instance.Keybind, true, out var targetKey))
                {
                    if (Input.IsKeyReleased(targetKey))
                    {
                        TriggerManualOptimization();
                    }
                }
            }
        }

        private void TriggerManualOptimization()
        {
            // Delegate to the patch instance
            CraftingPatches.ManualTrigger();
        }
    }
}
