using System;
using System.IO;
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
