using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace SettlementAutomationCore.Transactions
{
    public sealed class PartyEquipmentCandidate
    {
        public PartyEquipmentCandidate(EquipmentElement equipmentElement, int quantity)
        {
            EquipmentElement = equipmentElement;
            Quantity = quantity;
        }

        public EquipmentElement EquipmentElement { get; }
        public int Quantity { get; }
    }

    public sealed class PartyEquipmentTransactionResult
    {
        private PartyEquipmentTransactionResult(bool success, string? failureReason)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public string FailureReason { get; }

        public static PartyEquipmentTransactionResult Succeeded()
        {
            return new PartyEquipmentTransactionResult(true, null);
        }

        public static PartyEquipmentTransactionResult Failed(string reason)
        {
            return new PartyEquipmentTransactionResult(false, reason);
        }
    }

    public interface IPartyEquipmentTransaction
    {
        IReadOnlyList<PartyEquipmentCandidate> BuildAvailableEquipmentPool();

        PartyEquipmentTransactionResult EquipItem(
            EquipmentElement equipmentElement,
            Hero hero,
            InventoryLogic.InventorySide targetSide,
            EquipmentIndex slot);

        void RefreshAfterTransaction();
    }

    public static class PartyEquipmentTransaction
    {
        public static IPartyEquipmentTransaction ForInventoryLogic(InventoryLogic inventoryLogic, SPInventoryVM? inventoryVm = null)
        {
            if (inventoryLogic == null) throw new ArgumentNullException(nameof(inventoryLogic));
            return new InventoryLogicPartyEquipmentTransaction(inventoryLogic, inventoryVm);
        }

        public static IPartyEquipmentTransaction ForParty(MobileParty party, SPInventoryVM? inventoryVm = null)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            return new DirectPartyEquipmentTransaction(party, inventoryVm);
        }
    }

    public static class InventoryUiRefresh
    {
        public static void Refresh(SPInventoryVM? inventoryVm = null, string loggerName = "SettlementAutomationCore")
        {
            try
            {
                if (inventoryVm != null)
                {
                    inventoryVm.ExecuteRemoveZeroCounts();
                    inventoryVm.RefreshValues();

                    InvokeOptional(inventoryVm, "UpdateRightCharacter");
                    InvokeOptional(inventoryVm, "UpdateLeftCharacter");
                    InvokeOptional(inventoryVm, "RefreshInformationValues");

                    Game.Current?.EventManager?.TriggerEvent(new InventoryEquipmentTypeChangedEvent(!inventoryVm.IsCivilianMode));
                }

                Campaign.Current?.CurrentMenuContext?.Refresh();
            }
            catch (Exception ex)
            {
                Helpers.Logger.WriteLog(loggerName, $"Error refreshing inventory UI/menu state: {ex.Message}");
            }
        }

        private static void InvokeOptional(SPInventoryVM inventoryVm, string methodName)
        {
            var method = inventoryVm.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(inventoryVm, null);
        }
    }

    internal sealed class InventoryLogicPartyEquipmentTransaction : IPartyEquipmentTransaction
    {
        private readonly InventoryLogic _inventoryLogic;
        private readonly SPInventoryVM? _inventoryVm;

        public InventoryLogicPartyEquipmentTransaction(InventoryLogic inventoryLogic, SPInventoryVM? inventoryVm)
        {
            _inventoryLogic = inventoryLogic;
            _inventoryVm = inventoryVm;
        }

        public IReadOnlyList<PartyEquipmentCandidate> BuildAvailableEquipmentPool()
        {
            var availableItems = new List<PartyEquipmentCandidate>();
            var playerItems = _inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
            foreach (var element in playerItems)
            {
                if (element.IsEmpty || element.Amount <= 0 || element.EquipmentElement.Item == null) continue;
                availableItems.Add(new PartyEquipmentCandidate(element.EquipmentElement, element.Amount));
            }

            return availableItems;
        }

        public PartyEquipmentTransactionResult EquipItem(
            EquipmentElement equipmentElement,
            Hero hero,
            InventoryLogic.InventorySide targetSide,
            EquipmentIndex slot)
        {
            if (hero?.CharacterObject == null)
            {
                return PartyEquipmentTransactionResult.Failed("target hero was missing");
            }

            var equipElement = new ItemRosterElement(equipmentElement, 1);
            var command = TransferCommand.Transfer(
                1,
                InventoryLogic.InventorySide.PlayerInventory,
                targetSide,
                equipElement,
                EquipmentIndex.None,
                slot,
                hero.CharacterObject);
            _inventoryLogic.AddTransferCommand(command);
            return PartyEquipmentTransactionResult.Succeeded();
        }

        public void RefreshAfterTransaction()
        {
            InventoryUiRefresh.Refresh(_inventoryVm);
        }
    }

    internal sealed class DirectPartyEquipmentTransaction : IPartyEquipmentTransaction
    {
        private readonly MobileParty _party;
        private readonly SPInventoryVM? _inventoryVm;

        public DirectPartyEquipmentTransaction(MobileParty party, SPInventoryVM? inventoryVm)
        {
            _party = party;
            _inventoryVm = inventoryVm;
        }

        public IReadOnlyList<PartyEquipmentCandidate> BuildAvailableEquipmentPool()
        {
            var availableItems = new List<PartyEquipmentCandidate>();
            foreach (var element in _party.ItemRoster)
            {
                if (element.IsEmpty || element.Amount <= 0 || element.EquipmentElement.Item == null) continue;
                availableItems.Add(new PartyEquipmentCandidate(element.EquipmentElement, element.Amount));
            }

            return availableItems;
        }

        public PartyEquipmentTransactionResult EquipItem(
            EquipmentElement equipmentElement,
            Hero hero,
            InventoryLogic.InventorySide targetSide,
            EquipmentIndex slot)
        {
            if (hero == null)
            {
                return PartyEquipmentTransactionResult.Failed("target hero was missing");
            }

            var equipment = GetEquipmentForSide(hero, targetSide);
            var currentEquipment = equipment[slot];

            _party.ItemRoster.AddToCounts(equipmentElement, -1);

            if (!currentEquipment.IsEmpty && currentEquipment.Item != null)
            {
                _party.ItemRoster.AddToCounts(currentEquipment, 1);
            }

            equipment.AddEquipmentToSlotWithoutAgent(slot, equipmentElement);
            return PartyEquipmentTransactionResult.Succeeded();
        }

        public void RefreshAfterTransaction()
        {
            InventoryUiRefresh.Refresh(_inventoryVm);
        }

        private static Equipment GetEquipmentForSide(Hero hero, InventoryLogic.InventorySide side)
        {
            return side switch
            {
                InventoryLogic.InventorySide.CivilianEquipment => hero.CivilianEquipment,
                InventoryLogic.InventorySide.StealthEquipment => hero.StealthEquipment,
                _ => hero.BattleEquipment
            };
        }
    }
}
