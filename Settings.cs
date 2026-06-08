using System;
using System.IO;
using Newtonsoft.Json;

namespace SmithingOptimizer
{
    public enum OptimizationGoal
    {
        Profit, // XP and Sell Value
        Damage  // Max swing/thrust damage
    }

    public class Settings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "SmithingOptimizer.json"
        );

        public static Settings Instance { get; private set; } = new Settings();

        public bool AutoSwitchEnabled { get; set; } = true;
        public OptimizationGoal Goal { get; set; } = OptimizationGoal.Profit;
        public bool LimitToInventory { get; set; } = true;
        public string Keybind { get; set; } = "O"; // Activated with Ctrl + Keybind

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    Instance = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
                }
                else
                {
                    Instance = new Settings();
                    Save();
                }
            }
            catch (Exception)
            {
                Instance = new Settings();
            }
        }

        public static void Save()
        {
            try
            {
                string? dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonConvert.SerializeObject(Instance, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception)
            {
                // Ignore save errors
            }
        }
    }
}
