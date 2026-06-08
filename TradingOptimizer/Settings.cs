using System;
using System.IO;
using Newtonsoft.Json;

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

        public bool AutoTradeOnEnterSettlement { get; set; } = true;
        public string Keybind { get; set; } = "T"; // Activated with Ctrl + Keybind
        public float FoodDaysToKeepPerSoldier { get; set; } = 0.1f;
        public bool TradeLivestock { get; set; } = true;
        public bool TradeMounts { get; set; } = false;
        public int MaxStackSizeToBuy { get; set; } = 100;
        public int MaxStackValueToBuy { get; set; } = 2000;
        public bool LimitToInventoryCapacity { get; set; } = true;
        public bool UseAveragePriceFallback { get; set; } = true;
        public float BuyPriceThresholdFactor { get; set; } = 0.80f; // Very Cheap (Green, <= 80%)
        public float SellPriceThresholdFactor { get; set; } = 1.30f; // Very Expensive (Red, >= 130%)

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
