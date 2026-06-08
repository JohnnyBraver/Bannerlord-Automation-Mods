using System;
using System.IO;
using System.Text.Json;

namespace TradingOptimizer
{
    public class Settings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "TradingOptimizer.json"
        );

        public static Settings Instance { get; private set; } = new Settings();

        public bool AutoTradeOnEnterSettlement { get; set; } = false;
        public bool FullyAutomaticMode { get; set; } = true;
        public string Keybind { get; set; } = "T"; // Activated with Ctrl + Keybind
        public float FoodDaysToKeepPerSoldier { get; set; } = 0.1f;
        public bool TradeLivestock { get; set; } = true;
        public bool TradeMounts { get; set; } = false;
        public bool SellEquipment { get; set; } = false;
        public bool BuyEquipmentForDonation { get; set; } = false;
        public float MaxCostPerXp { get; set; } = 1.0f;
        public bool DonateWeapons { get; set; } = true;
        public bool DonateArmor { get; set; } = true;
        public int MaxStackSizeToBuy { get; set; } = 100;
        public int MaxStackValueToBuy { get; set; } = 2000;
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
