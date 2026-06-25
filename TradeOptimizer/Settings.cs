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

    public enum CostBasisMode
    {
        PerkBased,
        Always,
        Never
    }

    public class CostBasisModeOption
    {
        private readonly string _name;
        public CostBasisMode Value { get; }
        public CostBasisModeOption(string name, CostBasisMode value) { _name = name; Value = value; }
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

    public enum TradeReportDetailMode
    {
        TopTradeGoods,
        Full
    }

    public class TradeReportDetailModeOption
    {
        private readonly string _name;
        public TradeReportDetailMode Value { get; }
        public TradeReportDetailModeOption(string name, TradeReportDetailMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum TradeReportSortMode
    {
        Amount,
        MarketValue,
        PaidPrice
    }

    public class TradeReportSortModeOption
    {
        private readonly string _name;
        public TradeReportSortMode Value { get; }
        public TradeReportSortModeOption(string name, TradeReportSortMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "TradeOptimizer_v0_4_1";
        public override string DisplayName => "Trade Optimizer";
        public override string FolderName => "TradeOptimizer";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<PricingReferenceModeOption> PricingReferenceModeOptions = new List<PricingReferenceModeOption>
        {
            new PricingReferenceModeOption("Perk-Based (Vanilla-like)", PricingReferenceMode.PerkBased),
            new PricingReferenceModeOption("Always Global (Cheat/Sandbox)", PricingReferenceMode.AlwaysGlobal),
            new PricingReferenceModeOption("Always Local (Regional)", PricingReferenceMode.AlwaysLocal)
        };

        private static readonly IReadOnlyList<CostBasisModeOption> CostBasisModeOptions = new List<CostBasisModeOption>
        {
            new CostBasisModeOption("Perk-Based (Vanilla-like)", CostBasisMode.PerkBased),
            new CostBasisModeOption("Always Use (QoL/Cheat)", CostBasisMode.Always),
            new CostBasisModeOption("Never Use", CostBasisMode.Never)
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

        private static readonly IReadOnlyList<TradeReportDetailModeOption> TradeReportDetailModeOptions = new List<TradeReportDetailModeOption>
        {
            new TradeReportDetailModeOption("Top Trade Goods", TradeReportDetailMode.TopTradeGoods),
            new TradeReportDetailModeOption("Full Item List", TradeReportDetailMode.Full)
        };

        private static readonly IReadOnlyList<TradeReportSortModeOption> TradeReportSortModeOptions = new List<TradeReportSortModeOption>
        {
            new TradeReportSortModeOption("Amount", TradeReportSortMode.Amount),
            new TradeReportSortModeOption("Market Value", TradeReportSortMode.MarketValue),
            new TradeReportSortModeOption("Paid Price", TradeReportSortMode.PaidPrice)
        };

        [SettingPropertyBool("Enable Trade Optimizer", RequireRestart = false,
            HintText = "Master automation switch. When disabled, Trade Optimizer will not run settlement-entry trade or automatic reports. The manual inventory button still works.", Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool("Auto-trade on Settlement Entry", RequireRestart = false,
            HintText = "Automatically evaluate and execute trades when entering a town or village.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoTradeOnEnterSettlement { get; set; } = true;

        [SettingPropertyBool("Simulation Mode (Dry Run)", RequireRestart = false,
            HintText = "Show what the mod WOULD buy/sell without making real trades. Great for tuning thresholds.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool SimulationMode { get; set; } = false;

        [SettingPropertyInteger("Initial Economy Settling Days", 0, 200, RequireRestart = false,
            HintText = "Number of campaign days to wait on a new game/save before profit auto-trading starts. This only pauses TradeOptimizer free trade, not Core item requests. Default: 50.", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int InitialSettlementDaysDelay { get; set; } = 50;

        [SettingPropertyDropdown("Pricing Reference Mode", RequireRestart = false,
            HintText = "Perk-Based: scales with perks. Always Global: uses global average. Always Local: uses local deviation.", Order = 1)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public Dropdown<PricingReferenceModeOption> PricingReferenceDropdown { get; set; } =
            new Dropdown<PricingReferenceModeOption>(PricingReferenceModeOptions, 0);

        [SettingPropertyDropdown("Cost Basis Mode", RequireRestart = false,
            HintText = "Perk-Based: uses purchase cost if Tier 1 perks owned. Always: always uses purchase cost. Never: always sells relative to market averages.", Order = 2)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public Dropdown<CostBasisModeOption> CostBasisDropdown { get; set; } =
            new Dropdown<CostBasisModeOption>(CostBasisModeOptions, 0);

        [SettingPropertyDropdown("Trading Stance", RequireRestart = false,
            HintText = "Balanced: sells at margin, holds cheap items unless cargo is >= 80%. Max Profit: always holds cheap items.", Order = 3)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public Dropdown<TradingStanceOption> TradingStanceDropdown { get; set; } =
            new Dropdown<TradingStanceOption>(TradingStanceOptions, 0);

        [SettingPropertyDropdown("Loot Handling Mode", RequireRestart = false,
            HintText = "Liquidate: sell loot immediately. XP Farm: sell loot to crash price, rebuy in Transaction 2. Profit: evaluate normal.", Order = 4)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public Dropdown<LootHandlingModeOption> LootHandlingDropdown { get; set; } =
            new Dropdown<LootHandlingModeOption>(LootHandlingModeOptions, 2);

        private float _buyPriceThresholdFactor = 0.80f;
        [SettingPropertyFloatingInteger("Buy Price Threshold", 0.5f, 1.30f, "#0.00", RequireRestart = false,
            HintText = "Buy items priced at or below this fraction of their average price. Maxes out at 1.30. Default: 0.80.", Order = 5)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public float BuyPriceThresholdFactor
        {
            get => _buyPriceThresholdFactor;
            set => _buyPriceThresholdFactor = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        private float _sellPriceThresholdFactor = 1.30f;
        [SettingPropertyFloatingInteger("Sell Price Threshold", 0.80f, 2.0f, "#0.00", RequireRestart = false,
            HintText = "Sell items priced at or above this fraction of their average price. Starts at 0.80. Default: 1.30.", Order = 6)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public float SellPriceThresholdFactor
        {
            get => _sellPriceThresholdFactor;
            set => _sellPriceThresholdFactor = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        private float _goodBuyThreshold = 0.50f;
        [SettingPropertyFloatingInteger("Good Buy Threshold", 0.10f, 0.80f, "#0.00", RequireRestart = false,
            HintText = "Only buy items priced at or below this fraction of their average price once usable cargo capacity is >= Cargo Limit Threshold. Default: 0.50.", Order = 7)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public float GoodBuyThreshold
        {
            get => _goodBuyThreshold;
            set => _goodBuyThreshold = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        private float _goodSellThreshold = 2.00f;
        [SettingPropertyFloatingInteger("Good Sell Threshold", 1.50f, 3.00f, "#0.00", RequireRestart = false,
            HintText = "Force sell conflict items if current price is at or above this multiplier of their average price/cost basis under Balanced stance. Default: 2.00.", Order = 8)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public float GoodSellThreshold
        {
            get => _goodSellThreshold;
            set => _goodSellThreshold = (float)System.Math.Round(value / 0.10f) * 0.10f;
        }

        private float _cargoLimitThreshold = 0.75f;
        [SettingPropertyFloatingInteger("Cargo Limit Threshold", 0.10f, 0.90f, "#0.00", RequireRestart = false,
            HintText = "The usable capacity threshold at which Balanced stance starts restricting buying and liquidating conflict items. Default: 0.75.", Order = 9)]
        [SettingPropertyGroup("Price Margins", GroupOrder = 1)]
        public float CargoLimitThreshold
        {
            get => _cargoLimitThreshold;
            set => _cargoLimitThreshold = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        [SettingPropertyDropdown("Food Trading Policy", RequireRestart = false,
            HintText = "Control how food items are auto-traded.", Order = 1)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 2)]
        public Dropdown<TradingModeOption> FoodTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 3); // Default: Buy & Sell (index 3)

        [SettingPropertyDropdown("Livestock Trading Policy", RequireRestart = false,
            HintText = "Control how livestock (animals) are auto-traded.", Order = 2)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 2)]
        public Dropdown<TradingModeOption> LivestockTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 0); // Default: None (index 0)

        [SettingPropertyDropdown("Mounts Trading Policy", RequireRestart = false,
            HintText = "Control how mounts (horses, camels) are auto-traded.", Order = 3)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 2)]
        public Dropdown<TradingModeOption> MountsTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 0); // Default: None (index 0)

        [SettingPropertyDropdown("Crafting Materials Trading Policy", RequireRestart = false,
            HintText = "Control how smithing materials such as charcoal and ingots are auto-traded.", Order = 4)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 2)]
        public Dropdown<TradingModeOption> CraftingMaterialsTradingModeDropdown { get; set; } =
            new Dropdown<TradingModeOption>(TradingModeOptions, 0); // Default: None (index 0)

        [SettingPropertyInteger("Max Stack Size to Buy", 1, 500, RequireRestart = false,
            HintText = "Maximum number of any single item type to buy per trade stop.", Order = 5)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 2)]
        public int MaxStackSizeToBuy { get; set; } = 100;

        private int _maxStackValueToBuy = 2000;

        [SettingPropertyInteger("Max Stack Total Value", 500, 50000, RequireRestart = false,
            HintText = "Stop buying an item type once its total stack value exceeds this in denars. Default: 2000 denars. Snapping step: 500.", Order = 6)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 2)]
        public int MaxStackValueToBuy
        {
            get => _maxStackValueToBuy;
            set => _maxStackValueToBuy = ((value + 250) / 500) * 500;
        }

        [SettingPropertyDropdown("Level of Detail", RequireRestart = false,
            HintText = "Top Trade Goods reports only the most important trade goods. Full Item List reports every traded item.", Order = 1)]
        [SettingPropertyGroup("Trade Reporting", GroupOrder = 3)]
        public Dropdown<TradeReportDetailModeOption> TradeReportDetailDropdown { get; set; } =
            new Dropdown<TradeReportDetailModeOption>(TradeReportDetailModeOptions, 0);

        [SettingPropertyInteger("Max Items to Print Details For", 1, 20, RequireRestart = false,
            HintText = "Maximum trade-good item types shown for buys and for sells in the concise in-game TradeOptimizer report.", Order = 2)]
        [SettingPropertyGroup("Trade Reporting", GroupOrder = 3)]
        public int TopTradeGoodsToReport { get; set; } = 4;

        [SettingPropertyBool("Apply Max Items Per Side", RequireRestart = false,
            HintText = "If enabled, the max item count applies separately to sold and bought trade goods. If disabled, the max applies to the combined report.", Order = 3)]
        [SettingPropertyGroup("Trade Reporting", GroupOrder = 3)]
        public bool ApplyTradeReportLimitPerSide { get; set; } = true;

        [SettingPropertyDropdown("Sorting Mode", RequireRestart = false,
            HintText = "Choose how the concise in-game TradeOptimizer report selects top trade goods.", Order = 4)]
        [SettingPropertyGroup("Trade Reporting", GroupOrder = 3)]
        public Dropdown<TradeReportSortModeOption> TradeReportSortDropdown { get; set; } =
            new Dropdown<TradeReportSortModeOption>(TradeReportSortModeOptions, 2);

        // Helper properties for cleaner logic access
        public PricingReferenceMode PricingReference => PricingReferenceDropdown.SelectedValue.Value;
        public CostBasisMode CostBasis => CostBasisDropdown.SelectedValue.Value;
        public TradingStance Stance => TradingStanceDropdown.SelectedValue.Value;
        public LootHandlingMode LootHandling => LootHandlingDropdown.SelectedValue.Value;
        public bool ShouldSplitTransactions => LootHandling == LootHandlingMode.XPFarm;

        public TradingMode FoodTradingMode => FoodTradingModeDropdown.SelectedValue.Value;
        public TradingMode LivestockTradingMode => LivestockTradingModeDropdown.SelectedValue.Value;
        public TradingMode MountsTradingMode => MountsTradingModeDropdown.SelectedValue.Value;
        public TradingMode CraftingMaterialsTradingMode => CraftingMaterialsTradingModeDropdown.SelectedValue.Value;
        public TradeReportDetailMode TradeReportDetail => TradeReportDetailDropdown.SelectedValue.Value;
        public TradeReportSortMode TradeReportSort => TradeReportSortDropdown.SelectedValue.Value;
    }
}
