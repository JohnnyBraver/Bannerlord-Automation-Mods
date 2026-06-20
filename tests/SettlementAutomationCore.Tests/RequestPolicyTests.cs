using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using SettlementAutomationCore;
using SettlementAutomationCore.Helpers;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using Xunit;

namespace SettlementAutomationCore.Tests
{
    public class RequestPolicyTests
    {
        [Fact]
        public void SortRequests_OrdersByProfileThenPriorityDescending()
        {
            var requests = new[]
            {
                Request("routine-low", RequestProfile.Routine, 1),
                Request("critical-low", RequestProfile.Critical, 1),
                Request("critical-high", RequestProfile.Critical, 9),
                Request("luxury-high", RequestProfile.Luxury, 9),
                Request("essential", RequestProfile.Essential, 5),
                Request("opportunistic", RequestProfile.Opportunistic, 5)
            };

            var sorted = RequestPolicy.SortRequests(requests).Select(r => r.RequestorId).ToList();

            Assert.Equal(
                new[] { "critical-high", "critical-low", "essential", "routine-low", "opportunistic", "luxury-high" },
                sorted);
        }

        [Theory]
        [InlineData(RequestProfile.Critical, 250, true)]
        [InlineData(RequestProfile.Essential, 250, true)]
        [InlineData(RequestProfile.Routine, 150, true)]
        [InlineData(RequestProfile.Routine, 151, false)]
        [InlineData(RequestProfile.Opportunistic, 110, true)]
        [InlineData(RequestProfile.Opportunistic, 111, false)]
        [InlineData(RequestProfile.Luxury, 500, true)]
        public void IsPriceAllowedForProfile_AppliesCapsOnlyToRoutineAndOpportunistic(RequestProfile profile, int price, bool expected)
        {
            Assert.Equal(expected, RequestPolicy.IsPriceAllowedForProfile(profile, 100, price, 1.5f, 1.1f));
        }

        [Fact]
        public void GetGoldReserveForRequest_UsesExplicitReserveForLuxuryArmorStyleRequests()
        {
            var request = AutomationRequest.ForMarketItems(
                "EquipmentManager",
                new[] { MarketItem("armor_a") },
                1,
                RequestProfile.Luxury,
                5,
                25000);

            int reserve = RequestPolicy.GetGoldReserveForRequest(request, 1000, 10, 400);

            Assert.Equal(25000, reserve);
        }

        [Fact]
        public void CreatesImplicitSellReservation_IgnoresPurchaseCountMarketRequests()
        {
            var market = AutomationRequest.ForMarketItems("EquipmentManager", new[] { MarketItem("armor_a") }, 1);
            var food = AutomationRequest.ForInventoryTarget("PartyManager", RequestType.ItemCategory, "Food", 10, RequestProfile.Critical);

            Assert.False(RequestPolicy.CreatesImplicitSellReservation(market));
            Assert.True(RequestPolicy.CreatesImplicitSellReservation(food));
        }

        [Fact]
        public void InventoryItemView_MatchesExactItemAndModifierIdentity()
        {
            var item = Item("armor_a");
            var fine = Modifier("fine");
            var cracked = Modifier("cracked");
            var view = new InventoryItemView(
                InventoryLogic.InventorySide.OtherInventory,
                new EquipmentElement(item, fine, null!, false),
                1,
                100,
                InventoryItemCategory.Armor);

            Assert.True(view.MatchesEquipmentElement(new EquipmentElement(item, fine, null!, false)));
            Assert.False(view.MatchesEquipmentElement(new EquipmentElement(item, cracked, null!, false)));
            Assert.False(view.MatchesEquipmentElement(new EquipmentElement(Item("armor_b"), fine, null!, false)));
        }

        [Fact]
        public void MarketItemRequest_MatchesExactItemAndModifierIdentity()
        {
            var item = Item("armor_a");
            var fine = Modifier("fine");
            var cracked = Modifier("cracked");
            var request = AutomationRequest.ForMarketItems(
                "EquipmentManager",
                new[]
                {
                    new InventoryItemView(
                        InventoryLogic.InventorySide.OtherInventory,
                        new EquipmentElement(item, fine, null!, false),
                        1,
                        100,
                        InventoryItemCategory.Armor)
                },
                1);

            Assert.True(request.MatchesEquipmentElement(new EquipmentElement(item, fine, null!, false)));
            Assert.False(request.MatchesEquipmentElement(new EquipmentElement(item, cracked, null!, false)));
            Assert.False(request.MatchesEquipmentElement(new EquipmentElement(Item("armor_b"), fine, null!, false)));
        }

        [Fact]
        public void MarketActivitySummary_AggregatesAndLimitsInGameSummary()
        {
            var summary = new MarketActivitySummary();
            summary.AddBought("Grain", 3, 60);
            summary.AddBought("Grain", 2, 40);
            summary.AddSold("Sword", 1, 250);
            summary.AddSlaughtered("Hog", 4);
            summary.AddGoldDelta(-125);

            var inGame = summary.BuildInGameLines("Sargot", "123 / 456 capacity (27%)");
            string log = summary.BuildLogSummary("Sargot");

            Assert.Contains(inGame, line => line.Contains("Purchases: -100d"));
            Assert.Contains(inGame, line => line.Contains("Sales: +250d"));
            Assert.Contains(inGame, line => line.Contains("Bought: 5x Grain (-100d)"));
            Assert.Contains(inGame, line => line.Contains("Sold: 1x Sword (+250d)"));
            Assert.Contains(inGame, line => line.Contains("Slaughtered: 4x Hog"));
            Assert.Contains(inGame, line => line.Contains("Cargo: 123 / 456 capacity (27%)"));
            Assert.Contains("Gold change: -125d", log);
            Assert.Contains("Bought: 5x Grain (-100d)", log);
            Assert.Contains("Sold: 1x Sword (+250d)", log);
        }

        [Fact]
        public void MarketActivitySummary_ExportsAggregatedReportItems()
        {
            var summary = new MarketActivitySummary();
            summary.AddBought("Armor", InventoryItemCategory.Armor, 1, 2000);
            summary.AddBought("Grain", InventoryItemCategory.Food, 3, 60);
            summary.AddBought("Grain", InventoryItemCategory.Food, 2, 40);
            summary.AddSold("Sword", InventoryItemCategory.Weapon, 1, 250);

            Assert.Equal(new[] { "Grain", "Armor" }, summary.BoughtItems.Select(item => item.ItemName).ToArray());
            Assert.Equal(5, summary.BoughtItems[0].Quantity);
            Assert.Equal(100, summary.BoughtItems[0].Gold);
            Assert.Equal(InventoryItemCategory.Food, summary.BoughtItems[0].Category);
            Assert.Equal("Food", summary.BoughtItems[0].CategoryName);
            Assert.Equal("Sword", summary.SoldItems.Single().ItemName);
            Assert.Equal(InventoryItemCategory.Weapon, summary.SoldItems.Single().Category);
        }

        [Fact]
        public void GenericProviderActivitySummary_GroupsItemsByCategory()
        {
            var report = new AutomationProviderReport(
                "TradeOptimizer",
                AutomationTransactionStage.FreeTrade,
                new[]
                {
                    new AutomationReportItem("Grain", InventoryItemCategory.Food, 5, 100),
                    new AutomationReportItem("Fish", InventoryItemCategory.Food, 4, 80),
                    new AutomationReportItem("Butter", InventoryItemCategory.Food, 3, 120),
                    new AutomationReportItem("Cheese", InventoryItemCategory.Food, 2, 90),
                    new AutomationReportItem("Date Fruit", InventoryItemCategory.Food, 1, 70),
                    new AutomationReportItem("Body Armor", InventoryItemCategory.Armor, 1, 2000)
                },
                new List<AutomationReportItem>(),
                new List<AutomationReportItem>());

            string summary = SubModule.BuildGenericProviderActivitySummary(report);

            Assert.Contains("bought Armor 1x (-2000d)", summary);
            Assert.Contains("Food 15x (-460d)", summary);
            Assert.DoesNotContain("Date Fruit", summary);
        }

        [Fact]
        public void BuildGenericProviderReportLine_UsesShortCoreSummary()
        {
            var equipmentReport = new AutomationProviderReport(
                "EquipmentManager",
                AutomationTransactionStage.PriorityRequest,
                new[]
                {
                    new AutomationReportItem("Blackened Armor", InventoryItemCategory.Armor, 1, 2096)
                },
                new List<AutomationReportItem>(),
                new List<AutomationReportItem>());
            var tradeReport = new AutomationProviderReport(
                "TradeOptimizer",
                AutomationTransactionStage.FreeTrade,
                new[]
                {
                    new AutomationReportItem("Clay", InventoryItemCategory.TradeGood, 44, 352)
                },
                new List<AutomationReportItem>(),
                new List<AutomationReportItem>());

            string equipmentLine = SubModule.BuildGenericProviderReportLine(equipmentReport);
            string tradeLine = SubModule.BuildGenericProviderReportLine(tradeReport);

            Assert.Equal("[Core] Equipment requests: bought Armor 1x (-2096d)", equipmentLine);
            Assert.Equal("[Core] Free trade: bought Trade goods 44x (-352d)", tradeLine);
        }

        [Fact]
        public void BuildGenericProviderReportLine_SummarizesMixedFreeTrade()
        {
            var report = new AutomationProviderReport(
                "TradeOptimizer",
                AutomationTransactionStage.FreeTrade,
                new[]
                {
                    new AutomationReportItem("Clay", InventoryItemCategory.TradeGood, 44, 352),
                    new AutomationReportItem("Grain", InventoryItemCategory.Food, 5, 75)
                },
                new[]
                {
                    new AutomationReportItem("Wine", InventoryItemCategory.TradeGood, 7, 490),
                    new AutomationReportItem("Hog", InventoryItemCategory.Livestock, 2, 120)
                },
                new List<AutomationReportItem>());

            string line = SubModule.BuildGenericProviderReportLine(report);

            Assert.Equal("[Core] Free trade: sold Livestock 2x (+120d), Trade goods 7x (+490d); bought Food 5x (-75d), Trade goods 44x (-352d)", line);
        }

        [Fact]
        public void SelectCoreSummaryReports_UsesSilentModsOrFullMode()
        {
            var reports = new List<AutomationProviderReport>
            {
                Report("EquipmentManager", AutomationTransactionStage.PriorityRequest),
                Report("TradeOptimizer", AutomationTransactionStage.FreeTrade)
            };
            var displayedProviderKeys = new HashSet<string> { "EquipmentManager:PriorityRequest" };

            var silent = SubModule.SelectCoreSummaryReports(reports, displayedProviderKeys, CoreReportingMode.SilentMods);
            var full = SubModule.SelectCoreSummaryReports(reports, displayedProviderKeys, CoreReportingMode.Full);
            var off = SubModule.SelectCoreSummaryReports(reports, displayedProviderKeys, CoreReportingMode.Off);

            Assert.Equal(new[] { "TradeOptimizer" }, silent.Select(report => report.ProviderName).ToArray());
            Assert.Equal(new[] { "EquipmentManager", "TradeOptimizer" }, full.Select(report => report.ProviderName).ToArray());
            Assert.Empty(off);
        }

        [Fact]
        public void GetProviderHeaderColor_DerivesStableColorsAndUsesProviderOverride()
        {
            var customProvider = new StyledReportProvider();

            uint tradeColor = SubModule.GetProviderHeaderColor("TradeOptimizer");

            Assert.Equal(tradeColor, SubModule.GetProviderHeaderColor("tradeoptimizer"));
            Assert.NotEqual(tradeColor, SubModule.GetProviderHeaderColor("EquipmentManager"));
            Assert.Equal(0xFFu, tradeColor & 0xFFu);
            Assert.Equal(0x11223344u, SubModule.GetProviderHeaderColor("AnyProvider", customProvider));
        }

        [Fact]
        public void BuildRejectedOrderLogLine_IncludesReasonCodeAndDetail()
        {
            string line = SubModule.BuildRejectedOrderLogLine(
                "Sargot",
                "PartyManager Routine/0 DesiredInventoryCount ItemCategory:Food qty=10 reserve=Core",
                RejectedOrderReason.CargoCapacityExceeded,
                "Grain would exceed cargo capacity");

            Assert.Contains("Request skipped at Sargot", line);
            Assert.Contains("PartyManager", line);
            Assert.Contains("[CargoCapacityExceeded]", line);
            Assert.Contains("Grain would exceed cargo capacity", line);
        }

        [Theory]
        [InlineData(1000f, 800f, 10, 100f)]
        [InlineData(1000f, 920f, 10, 0f)]
        [InlineData(1000f, 500f, 0, 500f)]
        [InlineData(1000f, 500f, 150, 0f)]
        public void CalculateFreeCargoCapacity_ReservesConfiguredCapacity(float capacity, float currentWeight, int reservePercent, float expected)
        {
            Assert.Equal(expected, TradeContextFactory.CalculateFreeCargoCapacity(capacity, currentWeight, reservePercent));
        }

        [Theory]
        [InlineData(true, false, false, false, false, true)]
        [InlineData(false, true, false, false, false, true)]
        [InlineData(false, true, true, false, false, false)]
        [InlineData(false, true, false, true, false, false)]
        [InlineData(false, true, false, false, true, false)]
        [InlineData(false, false, false, false, false, false)]
        public void CanRunNotableRecruitment_GatesHostileAndRaidedSettlements(
            bool isTown,
            bool isVillage,
            bool isAtWar,
            bool isRaided,
            bool isUnderRaid,
            bool expected)
        {
            Assert.Equal(expected, SubModule.CanRunNotableRecruitment(isTown, isVillage, isAtWar, isRaided, isUnderRaid));
        }

        [Fact]
        public void AutomationPhasePolicy_AllowsHostileVillageTradeButNotRecruitment()
        {
            var policy = SubModule.AutomationPhasePolicy.ForFacts(
                isTown: false,
                isVillage: true,
                isCastle: false,
                isHostile: true,
                isSameFaction: false,
                isOwnedByPlayerClan: false,
                isRaidedOrUnderRaid: false);

            Assert.True(policy.FreeTrade);
            Assert.True(policy.PriorityNeeds);
            Assert.False(policy.Recruitment);
            Assert.False(policy.GarrisonDonation);
            Assert.False(policy.DungeonDonation);
        }

        [Fact]
        public void ApplyTradeGoldDelta_AppliesOnlyBalanceDifference()
        {
            var deltas = new List<int>();

            int updated = InventoryHelper.ApplyTradeGoldDelta(1000, 740, deltas.Add);

            Assert.Equal(740, updated);
            Assert.Equal(new[] { -260 }, deltas);

            updated = InventoryHelper.ApplyTradeGoldDelta(updated, 740, deltas.Add);

            Assert.Equal(740, updated);
            Assert.Equal(new[] { -260 }, deltas);
        }

        [Fact]
        public void AutomationPhasePolicy_GatesKeepDonationBySameFaction()
        {
            var neutralCastle = SubModule.AutomationPhasePolicy.ForFacts(
                isTown: false,
                isVillage: false,
                isCastle: true,
                isHostile: false,
                isSameFaction: false,
                isOwnedByPlayerClan: false,
                isRaidedOrUnderRaid: false);
            var sameFactionCastle = SubModule.AutomationPhasePolicy.ForFacts(
                isTown: false,
                isVillage: false,
                isCastle: true,
                isHostile: false,
                isSameFaction: true,
                isOwnedByPlayerClan: false,
                isRaidedOrUnderRaid: false);

            Assert.False(neutralCastle.GarrisonDonation);
            Assert.False(neutralCastle.DungeonDonation);
            Assert.True(sameFactionCastle.GarrisonDonation);
            Assert.True(sameFactionCastle.DungeonDonation);
        }

        [Fact]
        public void AutomationPhasePolicy_AllowsOwnedCastleFiefAutomationWithoutMarket()
        {
            var policy = SubModule.AutomationPhasePolicy.ForFacts(
                isTown: false,
                isVillage: false,
                isCastle: true,
                isHostile: false,
                isSameFaction: true,
                isOwnedByPlayerClan: true,
                isRaidedOrUnderRaid: false);

            Assert.True(policy.FiefMinimum);
            Assert.True(policy.FiefSurplus);
            Assert.True(policy.GarrisonDonation);
            Assert.True(policy.DungeonDonation);
            Assert.False(policy.FreeTrade);
            Assert.False(policy.PreSell);
            Assert.False(policy.Tavern);
        }

        [Fact]
        public void AutomationPhasePolicy_RaidedSettlementsSkipMarketRecruitmentAndDonation()
        {
            var policy = SubModule.AutomationPhasePolicy.ForFacts(
                isTown: true,
                isVillage: false,
                isCastle: false,
                isHostile: false,
                isSameFaction: true,
                isOwnedByPlayerClan: true,
                isRaidedOrUnderRaid: true);

            Assert.False(policy.FreeTrade);
            Assert.False(policy.Recruitment);
            Assert.False(policy.GarrisonDonation);
            Assert.False(policy.DungeonDonation);
            Assert.True(policy.FiefMinimum);
            Assert.True(policy.FiefSurplus);
        }

        [Fact]
        public void ConsumeSellableItems_ReducesOnlyMatchingIdentity()
        {
            var item = Item("grain");
            var fine = Modifier("fine");
            var cracked = Modifier("cracked");
            var fineElement = new EquipmentElement(item, fine, null!, false);
            var crackedElement = new EquipmentElement(item, cracked, null!, false);
            var sellableItems = new[]
            {
                new SellableItem(fineElement, 5),
                new SellableItem(crackedElement, 7)
            };

            var updated = SubModule.ConsumeSellableItems(sellableItems, fineElement, 3).ToList();

            Assert.Equal(2, updated[0].AvailableQuantity);
            Assert.Equal(7, updated[1].AvailableQuantity);
        }

        private static AutomationRequest Request(string id, RequestProfile profile, int priority)
        {
            return AutomationRequest.ForInventoryTarget(id, RequestType.ItemCategory, "Food", 1, profile, priority);
        }

        private static AutomationProviderReport Report(string providerName, AutomationTransactionStage stage)
        {
            return new AutomationProviderReport(
                providerName,
                stage,
                new List<AutomationReportItem>
                {
                    new AutomationReportItem("Grain", InventoryItemCategory.Food, 1, 10)
                },
                new List<AutomationReportItem>(),
                new List<AutomationReportItem>());
        }

        private sealed class StyledReportProvider : IAutomationReportProvider, IAutomationReportStyleProvider
        {
            public string ProviderName => "AnyProvider";
            public uint? ReportHeaderColor => 0x11223344u;

            public IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context)
            {
                return new List<string>();
            }
        }

        private static InventoryItemView MarketItem(string id)
        {
            return new InventoryItemView(
                InventoryLogic.InventorySide.OtherInventory,
                new EquipmentElement(Item(id), null, null!, false),
                1,
                100,
                InventoryItemCategory.Armor);
        }

        private static ItemObject Item(string id)
        {
            return new ItemObject(id);
        }

        private static ItemModifier Modifier(string id)
        {
            return new ItemModifier
            {
                StringId = id
            };
        }

        private static string ReadSource(params string[] parts)
        {
            string root = FindRepoRoot();
            return File.ReadAllText(Path.Combine(root, Path.Combine(parts)));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "SettlementAutomationCore")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "TradeOptimizer")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }
    }
}
