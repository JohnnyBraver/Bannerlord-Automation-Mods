using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    public class SellableItem
    {
        public EquipmentElement EquipmentElement { get; }
        public int AvailableQuantity { get; }    // owned - reserved (explicit + implicit)

        public SellableItem(EquipmentElement equipmentElement, int availableQuantity)
        {
            EquipmentElement = equipmentElement;
            AvailableQuantity = availableQuantity;
        }

        public bool Matches(EquipmentElement equipmentElement)
        {
            return InventoryItemView.HasSameItemIdentity(EquipmentElement, equipmentElement);
        }
    }

    public class TradeContext
    {
        public Settlement Settlement { get; }
        public MobileParty Party { get; }
        public InventoryLogic Logic { get; }     // For price queries

        // Budget
        public int AvailableGold { get; }        // Gold minus expense reserve
        public int AvailableMerchantGold { get; } // Settlement-side gold available to pay for player sells

        // Pricing
        public bool SellPricesAreStatic { get; }

        // Cargo
        public float CargoCapacityBalance { get; } // usableCargoLimit - currentWeight (can go negative)
        public bool NeedsToSellCargo => CargoCapacityBalance < 0f;
        public float FreeCargoHeadroom => Math.Max(0f, CargoCapacityBalance); // clamped for buy-headroom checks
        public bool EnforceCargoLimit { get; }   // From Core settings

        // Animals
        public int FreeAnimalSlots { get; }      // Herding headroom
        public int MaxPackAnimalPurchases { get; }

        // Sellable inventory (unreserved items only)
        public IReadOnlyList<SellableItem> SellableItems { get; }

        public TradeContext(
            Settlement settlement,
            MobileParty party,
            InventoryLogic logic,
            int availableGold,
            int availableMerchantGold,
            bool sellPricesAreStatic,
            float cargoCapacityBalance,
            bool enforceCargoLimit,
            int freeAnimalSlots,
            int maxPackAnimalPurchases,
            IReadOnlyList<SellableItem> sellableItems)
        {
            Settlement = settlement;
            Party = party;
            Logic = logic;
            AvailableGold = availableGold;
            AvailableMerchantGold = availableMerchantGold;
            SellPricesAreStatic = sellPricesAreStatic;
            CargoCapacityBalance = cargoCapacityBalance;
            EnforceCargoLimit = enforceCargoLimit;
            FreeAnimalSlots = freeAnimalSlots;
            MaxPackAnimalPurchases = maxPackAnimalPurchases;
            SellableItems = sellableItems;
        }
    }
}
