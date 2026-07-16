using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EquipmentManager.Tests
{
    public class EquipmentTransactionSafetyTests
    {
        [Fact]
        public void EquipmentEngine_UsesCorePartyEquipmentTransactionHelpers()
        {
            string source = ReadSource("EquipmentManager", "EquipmentEngine.cs");

            Assert.Contains("PartyEquipmentTransaction.ForInventoryLogic", source);
            Assert.Contains("PartyEquipmentTransaction.ForParty", source);
            Assert.Contains("CoreEquipmentTransferContext", source);
            Assert.DoesNotContain("_party.ItemRoster.AddToCounts(available.EquipmentElement, -1)", source);
            Assert.DoesNotContain("equipment.AddEquipmentToSlotWithoutAgent(slot, available.EquipmentElement)", source);
        }

        [Fact]
        public void AutoSell_UsesTheSharedCategorySelectorInBothSalePaths()
        {
            string engine = ReadSource("EquipmentManager", "EquipmentEngine.cs");
            string provider = ReadSource("EquipmentManager", "EquipmentManagerProvider.cs");

            Assert.Contains("settings.AutoSellCategorySetting != AutoSellCategory.Disabled", engine);
            Assert.Contains("EquipmentSaleProtector.IsSelectedForAutoSale", engine);
            Assert.Contains("settings.AutoSellCategorySetting == AutoSellCategory.Disabled", provider);
            Assert.Contains("EquipmentSaleProtector.IsSelectedForAutoSale", provider);
        }

        [Fact]
        public void Settings_ExposeNestedPlayerTaskSectionsWithoutLegacyControls()
        {
            string source = ReadSource("EquipmentManager", "Settings.cs");

            Assert.Contains("Auto-Sell Equipment", source);
            Assert.Contains("Keep Positive Modifiers", source);
            Assert.Contains("This affects equipping only; it never changes auto-sell.", source);
            Assert.Contains("Manually locked items and all keep/protection rules remain protected.", source);
            Assert.Contains("Reserve Spare Armor For", source);
            Assert.Contains("Stealth gear is never reserved.", source);
            Assert.Contains("Additional Armor Sets to Keep\", 0, 5", source);
            Assert.Contains("Math.Min(5, settings.AdditionalArmorSetsToKeep)", ReadSource("EquipmentManager", "EquipmentSaleProtector.cs"));
            Assert.DoesNotContain("Keep Spare Sneaking Armor Sets", source);
            Assert.DoesNotContain("Buy Armor Upgrades", source);
            Assert.DoesNotContain("Armor Upgrade Spend Mode", source);
            Assert.DoesNotContain("Equipment Purchase Policy", source);
            Assert.DoesNotContain("Buy Bow Upgrades", source);
            Assert.DoesNotContain("Buy Crossbow Upgrades", source);
            Assert.DoesNotContain("AutoEquipBeforeSettlementTrade", source);
            Assert.DoesNotContain("AutoEquipAfterSettlementPurchases", source);
            Assert.DoesNotContain("AutoEquipAfterBattleLoot", source);

            Assert.Contains("public bool BuyBattleArmorUpgrades { get; set; } = true;", source);
            Assert.Contains("public bool BuyHandSlotWeapons { get; set; } = true;", source);
            Assert.DoesNotContain("AutoEquipBeforeSettlementTrade", ReadSource("EquipmentManager", "EquipmentManagerProvider.cs"));
            Assert.DoesNotContain("AutoEquipAfterSettlementPurchases", ReadSource("EquipmentManager", "SubModule.cs"));
            Assert.DoesNotContain("AutoEquipAfterBattleLoot", ReadSource("EquipmentManager", "SubModule.cs"));
        }

        [Fact]
        public void Settings_AssignOneUniqueOrderToEachMcmGroupPath()
        {
            string source = ReadSource("EquipmentManager", "Settings.cs");
            var groupOrders = new Dictionary<string, int>();

            foreach (Match match in Regex.Matches(source, "\\[SettingPropertyGroup\\(\\\"([^\\\"]+)\\\", GroupOrder = (\\d+)\\)\\]"))
            {
                string groupPath = match.Groups[1].Value;
                int groupOrder = int.Parse(match.Groups[2].Value);

                if (groupOrders.TryGetValue(groupPath, out int existingOrder))
                {
                    Assert.Equal(existingOrder, groupOrder);
                }
                else
                {
                    groupOrders.Add(groupPath, groupOrder);
                }
            }

            Assert.NotEmpty(groupOrders);
            Assert.Equal(groupOrders.Count, groupOrders.Values.Distinct().Count());
        }

        [Fact]
        public void AutoBuy_UsesOneTrackPolicyForEligibilityAndSubmission()
        {
            string source = ReadSource("EquipmentManager", "EquipmentManagerProvider.cs");

            Assert.Contains("BuildPurchaseTrackPolicies(settings, playerGold)", source);
            Assert.Contains("new EquipmentPurchaseTrackPolicy(EquipmentPurchaseTrack.BattleArmor", source);
            Assert.Contains("new EquipmentPurchaseTrackPolicy(EquipmentPurchaseTrack.Weapons", source);
            Assert.Contains("new EquipmentPurchaseTrackPolicy(EquipmentPurchaseTrack.CivilianArmor", source);
            Assert.Contains("new EquipmentPurchaseTrackPolicy(EquipmentPurchaseTrack.StealthGear", source);
            Assert.Contains("foreach (var policy in purchasePolicies)", source);
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
                    Directory.Exists(Path.Combine(dir.FullName, "EquipmentManager")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Bannerlord-Mods repository root.");
        }
    }
}
