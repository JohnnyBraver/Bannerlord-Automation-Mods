using System;
using System.IO;
using System.Text.Json;

namespace EquipmentManager
{
    public class Settings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "EquipmentManager.json"
        );

        public static Settings Instance { get; private set; } = new Settings();

        public string Keybind { get; set; } = "E"; // Activated with Ctrl + Keybind
        public bool AutoEquipCompanions { get; set; } = true;
        public bool AutoEquipArmor { get; set; } = true;
        public bool AutoEquipWeapons { get; set; } = true;
        public bool OptimizeCivilianForSneaking { get; set; } = false;
        public float SneakingWeightPenaltyFactor { get; set; } = 2.0f;
        public int MinTierToKeep { get; set; } = 5;
        public string MinQualityToKeep { get; set; } = "Fine";
        public bool KeepPositiveModifiers { get; set; } = true;
        public bool LockDonationWeapons { get; set; } = true;
        public bool LockDonationArmor { get; set; } = true;
        public float MaxCostPerXp { get; set; } = 1.0f;
        public bool SellUnlockedEquipment { get; set; } = true;
        public bool LimitToInventoryCapacity { get; set; } = true;

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    Instance = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
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
                string json = JsonSerializer.Serialize(Instance, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception)
            {
                // Ignore save errors
            }
        }
    }
}
