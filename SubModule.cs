using System;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

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
