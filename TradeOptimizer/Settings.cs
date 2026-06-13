using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace TradeOptimizer
{
    public enum PricingReferenceMode
    {
        PerkBased,
        AlwaysGlobal,
        AlwaysLocal
    }

    public class PricingReferenceModeOption
    {
        private readonly string _name;
        public PricingReferenceMode Value { get; }
        public PricingReferenceModeOption(string name, PricingReferenceMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum TradingStance
    {
        Balanced,
        MaxProfit
    }

    public class TradingStanceOption
    {
        private readonly string _name;
        public TradingStance Value { get; }
        public TradingStanceOption(string name, TradingStance value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum LootHandlingMode
    {
        Liquidate,
        XPFarm,
        Profit
    }

    public class LootHandlingModeOption
    {
        private readonly string _name;
        public LootHandlingMode Value { get; }
        public LootHandlingModeOption(string name, LootHandlingMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum TradingMode
    {
        None,
        SellOnly,
        BuyOnly,
        BuyAndSell
    }

    public class TradingModeOption
    {
        private readonly string _name;
        public TradingMode Value { get; }
        public TradingModeOption(string name, TradingMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "TradeOptimizer_v1";
        public override string DisplayName => "Trade Optimizer";
        public override string FolderName => "TradeOptimizer";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<PricingReferenceModeOption> PricingReferenceModeOptions = new List<PricingReferenceModeOption>
        {
            new PricingReferenceModeOption("Perk-Based (Vanilla-like)", PricingReferenceMode.PerkBased),
            new PricingReferenceModeOption("Always Global (Cheat/Sandbox)", PricingReferenceMode.AlwaysGlobal),
            new PricingReferenceModeOption("Always Local (Regional)", PricingReferenceMode.AlwaysLocal)
        };

        private static readonly IReadOnlyList<TradingStanceOption> TradingStanceOptions = new List<TradingStanceOption>
        {
            new TradingStanceOption("Balanced", TradingStance.Balanced),
            new TradingStanceOption("Max Profit", TradingStance.MaxProfit)
        };

        private static readonly IReadOnlyList<LootHandlingModeOption> LootHandlingModeOptions = new List<LootHandlingModeOption>
        {
            new LootHandlingModeOption("Liquidate (Sell All)", LootHandlingMode.Liquidate),
            new LootHandlingModeOption("XP Farm (Sell & Rebuy)", LootHandlingMode.XPFarm),
            new LootHandlingModeOption("Profit (Treat as Normal)", LootHandlingMode.Profit)
        };

        private static readonly IReadOnlyList<TradingModeOption> TradingModeOptions = new List<TradingModeOption>
        {
            new TradingModeOption("None (Disabled)", TradingMode.None),
            new TradingModeOption("Sell Only", TradingMode.SellOnly),
            new TradingModeOption("Buy Only", TradingMode.BuyOnly),
            new TradingModeOption("Buy & Sell", TradingMode.BuyAndSell)
        };

        [SettingPropertyBool("Auto-trade on Settlement Entry", RequireRestart = false,
            HintText = "Automatically evaluate and execute trades when entering a town or village.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 3)]
        public bool AutoTradeOnEnterSettlement { get; set; } = true;

        [SettingPropertyBool("Buy Blackened Stealth Gear", RequireRestart = false,
            HintText = "Automatically buy any available 'Blackened' armor/stealth items from merchants, regardless of standard trade restrictions.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 3)]
        public bool BuyBlackenedStealthGear { get; set; } = false;

        [SettingPropertyBool("Simulation Mode (Dry Run)", RequireRestart = false,
            HintText = "Show what the mod WOULD buy/sell without making real trades. Great for tuning thresholds.", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 3)]
        public bool SimulationMode { get; set; } = false;

        [SettingPropertyInteger("Initial Economy Settling Days", 0, 200, RequireRestart = false,
            HintText = "Number of campaign days to wait on a new game/save before auto-trading starts to let the initial economy stabilize (e.g., 50 days).", Order = 4)]
        [SettingPropertyGroup("General", GroupOrder = 3)]
        public int InitialSettlementDaysDelay { get; set; } = 50;

        [SettingPropertyDropdown("Pricing Reference Mode", RequireRestart = false,
            HintText = "Perk-Based: scales with perks. Always Global: uses global average. Always Local: uses local deviation.", Order = 1)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public Dropdown<PricingReferenceModeOption> PricingReferenceDropdown { get; set; } =
            new Dropdown<PricingReferenceModeOption>(PricingReferenceModeOptions, 0);

        [SettingPropertyDropdown("Trading Stance", RequireRestart = false,
            HintText = "Balanced: sells at margin, holds cheap items unless cargo is >= 80%. Max Profit: always holds cheap items.", Order = 2)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public Dropdown<TradingStanceOption> TradingStanceDropdown { get; set; } =
            new Dropdown<TradingStanceOption>(TradingStanceOptions, 0);

        [SettingPropertyDropdown("Loot Handling Mode", RequireRestart = false,
            HintText = "Liquidate: sell loot immediately. XP Farm: sell loot to crash price, rebuy in Transaction 2. Profit: evaluate normal.", Order = 3)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public Dropdown<LootHandlingModeOption> LootHandlingDropdown { get; set; } =
            new Dropdown<LootHandlingModeOption>(LootHandlingModeOptions, 2);

        [SettingPropertyFloatingInteger("Buy Price Threshold", 0.5f, 0.80f, "#0.00", RequireRestart = false,
            HintText = "Buy items priced at or below this fraction of their average price. Maxes out at 0.80 (20% below average - where green begins).", Order = 4)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public float BuyPriceThresholdFactor { get; set; } = 0.80f;

        [SettingPropertyFloatingInteger("Sell Price Threshold", 1.30f, 2.0f, "#0.00", RequireRestart = false,
            HintText = "Sell items priced at or above this fraction of their average price. Starts at 1.30 (30% above average - where red begins).", Order = 5)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public float SellPriceThresholdFactor { get; set; } = 1.30f;

        [SettingPropertyFloatingInteger("Food Logistics Price Throttle Factor", 0.5f, 3.0f, "#0.00", RequireRestart = false,
            HintText = "Postpone non-urgent food variety purchases if their price exceeds this multiplier of their average price. Range: 0.50 - 3.00. Default: 1.30 (130% - where red begins).", Order = 7)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public float FoodLogisticsPriceThrottleFactor { get; set; } = 1.30f;

        [SettingPropertyFloatingInteger("Mounts Logistics Price Throttle Factor", 0.5f, 3.0f, "#0.00", RequireRestart = false,
            HintText = "Postpone speed mount purchases if their price exceeds this multiplier of their average price. Range: 0.50 - 3.00. Default: 1.50 (150% - mounts are harder to find cheap).", Order = 8)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public float MountsLogisticsPriceThrottleFactor { get; set; } = 1.50f;

        [SettingPropertyBool("Force Buy Critical Variety Food", RequireRestart = false,
            HintText = "If current supply of a food type is under 1/10 of its target variety quantity (rounded up), force buy it regardless of price to prevent complete lack of that food type.", Order = 6)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 2)]
        public bool ForceBuyMinVarietyFood { get; set; } = true;

        [SettingPropertyBool("Limit to Carry Capacity", RequireRestart = false,
            HintText = "Stop buying when your party's carry weight would be exceeded.", Order = 7)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public bool LimitToInventoryCapacity { get; set; } = true;

        [SettingPropertyInteger("Max Stack Size to Buy", 1, 500, RequireRestart = false,
            HintText = "Maximum number of any single item type to buy per trade stop.", Order = 5)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int MaxStackSizeToBuy { get; set; } = 100;

        [SettingPropertyInteger("Max Stack Total Value (d)", 100, 50000, RequireRestart = false,
            HintText = "Stop buying an item type once its total stack value exceeds this in denars.", Order = 6)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int MaxStackValueToBuy { get; set; } = 2000;

        [SettingPropertyDropdown("Food Trading Policy", RequireRestart = false,
            HintText = "Control how food items are auto-traded.", Order = 1)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public Dropdown<TradingModeOption> FoodTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 3); // Default: Buy & Sell (index 3)

        [SettingPropertyInteger("Party Food Days to Keep", 1, 100, RequireRestart = false,
            HintText = "Keep at least this many days of food supply for the party before selling food items.", Order = 4)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int PartyFoodDaysToKeep { get; set; } = 10;

        [SettingPropertyDropdown("Livestock Trading Policy", RequireRestart = false,
            HintText = "Control how livestock (animals) are auto-traded.", Order = 2)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public Dropdown<TradingModeOption> LivestockTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 0); // Default: None (index 0)

        [SettingPropertyDropdown("Mounts Trading Policy", RequireRestart = false,
            HintText = "Control how mounts (horses, camels) are auto-traded.", Order = 3)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public Dropdown<TradingModeOption> MountsTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 0); // Default: None (index 0)

        [SettingPropertyInteger("Minimum Gold Reserve", 0, 50000, RequireRestart = false,
            HintText = "Never let your gold balance drop below this amount when buying. Default: 1000 denars.", Order = 1)]
        [SettingPropertyGroup("Budget Protection", GroupOrder = 0)]
        public int MinimumGoldReserve { get; set; } = 1000;

        [SettingPropertyBool("Auto-Buy Food to Restock", RequireRestart = false, HintText = "Automatically buy food to maintain supply for soldiers.", Order = 5)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public bool AutoBuyFood { get; set; } = true;

        [SettingPropertyInteger("Min Party Size for Variety", 1, 100, RequireRestart = false, HintText = "Minimum party size before the mod starts buying a variety of foods for Steward XP.", Order = 6)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int MinPartySizeForVariety { get; set; } = 20;

        [SettingPropertyInteger("Min Gold for Variety", 500, 20000, RequireRestart = false, HintText = "Minimum gold reserve required to buy food varieties.", Order = 7)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int MinGoldForVariety { get; set; } = 3000;

        [SettingPropertyBool("Auto-Buy Riding Mounts for Speed", RequireRestart = false, HintText = "Automatically buy mounts for foot soldiers to upgrade party speed.", Order = 8)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public bool AutoBuyMounts { get; set; } = true;

        [SettingPropertyInteger("Min Gold for Mounts", 1000, 50000, RequireRestart = false, HintText = "Minimum gold reserve required to buy mounts.", Order = 9)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int MinGoldForMounts { get; set; } = 10000;

        [SettingPropertyInteger("Inventory Reservation Priority Threshold", 1, 100, RequireRestart = false, HintText = "Requests with priority higher than this will protect inventory items from being auto-sold.", Order = 10)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int InventoryReservationPriorityThreshold { get; set; } = 30;

        [SettingPropertyInteger("Min Days of Expenses to Keep", 0, 100, RequireRestart = false,
            HintText = "Ensure you keep enough gold to cover this many days of party wages/expenses (excluding daily income).", Order = 2)]
        [SettingPropertyGroup("Budget Protection", GroupOrder = 0)]
        public int MinDaysExpensesToKeep { get; set; } = 10;

        // Helper properties for cleaner logic access
        public PricingReferenceMode PricingReference => PricingReferenceDropdown.SelectedValue.Value;
        public TradingStance Stance => TradingStanceDropdown.SelectedValue.Value;
        public LootHandlingMode LootHandling => LootHandlingDropdown.SelectedValue.Value;
        public bool ShouldSplitTransactions => LootHandling == LootHandlingMode.XPFarm;

        public TradingMode FoodTradingMode => FoodTradingModeDropdown.SelectedValue.Value;
        public TradingMode LivestockTradingMode => LivestockTradingModeDropdown.SelectedValue.Value;
        public TradingMode MountsTradingMode => MountsTradingModeDropdown.SelectedValue.Value;
    }
}
