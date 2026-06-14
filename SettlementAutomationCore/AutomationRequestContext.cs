using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SettlementAutomationCore
{
    [Flags]
    public enum InventoryItemCategory
    {
        None = 0,
        Food = 1 << 0,
        Mount = 1 << 1,
        PackAnimal = 1 << 2,
        Livestock = 1 << 3,
        TradeGood = 1 << 4,
        Armor = 1 << 5,
        Weapon = 1 << 6
    }

    public class InventoryItemView
    {
        public string SnapshotId { get; }
        public InventoryLogic.InventorySide Side { get; }
        public EquipmentElement EquipmentElement { get; }
        public ItemObject Item { get; } = null!;
        public int Quantity { get; }
        public int UnitPrice { get; }
        public float Weight { get; }
        public InventoryItemCategory Categories { get; }

        public InventoryItemView(
            InventoryLogic.InventorySide side,
            EquipmentElement equipmentElement,
            int quantity,
            int unitPrice,
            InventoryItemCategory categories)
        {
            Side = side;
            EquipmentElement = equipmentElement;
            Item = equipmentElement.Item!;
            Quantity = quantity;
            UnitPrice = unitPrice;
            Weight = Item.Weight;
            Categories = categories;
            SnapshotId = CreateSnapshotId(side, equipmentElement);
        }

        public bool HasCategory(InventoryItemCategory category)
        {
            return (Categories & category) == category;
        }

        public bool MatchesEquipmentElement(EquipmentElement equipmentElement)
        {
            return HasSameItemIdentity(EquipmentElement, equipmentElement);
        }

        public static string CreateSnapshotId(InventoryLogic.InventorySide side, EquipmentElement equipmentElement)
        {
            string itemId = equipmentElement.Item?.StringId ?? "";
            string modifierId = equipmentElement.ItemModifier?.StringId ?? "";
            return $"{side}:{itemId}:{modifierId}";
        }

        public static bool HasSameItemIdentity(EquipmentElement left, EquipmentElement right)
        {
            string leftItemId = left.Item?.StringId ?? "";
            string rightItemId = right.Item?.StringId ?? "";
            if (!string.Equals(leftItemId, rightItemId, StringComparison.Ordinal))
            {
                return false;
            }

            string leftModifierId = left.ItemModifier?.StringId ?? "";
            string rightModifierId = right.ItemModifier?.StringId ?? "";
            return string.Equals(leftModifierId, rightModifierId, StringComparison.Ordinal);
        }

        public static InventoryItemCategory Classify(ItemObject item)
        {
            if (item == null) return InventoryItemCategory.None;

            InventoryItemCategory categories = InventoryItemCategory.None;
            if (item.IsFood) categories |= InventoryItemCategory.Food;
            if (item.IsMountable && item.HorseComponent != null && !item.HorseComponent.IsPackAnimal) categories |= InventoryItemCategory.Mount;
            if (item.IsMountable && item.HorseComponent != null && item.HorseComponent.IsPackAnimal) categories |= InventoryItemCategory.PackAnimal;
            if (item.IsAnimal && !item.IsMountable) categories |= InventoryItemCategory.Livestock;
            if (item.IsTradeGood) categories |= InventoryItemCategory.TradeGood;
            if (item.HasArmorComponent) categories |= InventoryItemCategory.Armor;
            if (item.WeaponComponent != null || item.PrimaryWeapon != null) categories |= InventoryItemCategory.Weapon;

            return categories;
        }
    }

    public class CategorizedInventoryView
    {
        public IReadOnlyList<InventoryItemView> All { get; }
        public IReadOnlyList<InventoryItemView> Food { get; }
        public IReadOnlyList<InventoryItemView> Mounts { get; }
        public IReadOnlyList<InventoryItemView> PackAnimals { get; }
        public IReadOnlyList<InventoryItemView> Livestock { get; }
        public IReadOnlyList<InventoryItemView> TradeGoods { get; }
        public IReadOnlyList<InventoryItemView> Armor { get; }
        public IReadOnlyList<InventoryItemView> Weapons { get; }

        public CategorizedInventoryView(IEnumerable<InventoryItemView> items)
        {
            var all = items?.ToList() ?? new List<InventoryItemView>();
            All = all;
            Food = all.Where(i => i.HasCategory(InventoryItemCategory.Food)).ToList();
            Mounts = all.Where(i => i.HasCategory(InventoryItemCategory.Mount)).ToList();
            PackAnimals = all.Where(i => i.HasCategory(InventoryItemCategory.PackAnimal)).ToList();
            Livestock = all.Where(i => i.HasCategory(InventoryItemCategory.Livestock)).ToList();
            TradeGoods = all.Where(i => i.HasCategory(InventoryItemCategory.TradeGood)).ToList();
            Armor = all.Where(i => i.HasCategory(InventoryItemCategory.Armor)).ToList();
            Weapons = all.Where(i => i.HasCategory(InventoryItemCategory.Weapon)).ToList();
        }

        public static CategorizedInventoryView Empty => new CategorizedInventoryView(new List<InventoryItemView>());
    }

    public class AutomationRequestContext
    {
        public MobileParty Party { get; }
        public Settlement Settlement { get; }
        public CategorizedInventoryView MerchantInventory { get; }
        public CategorizedInventoryView PlayerInventory { get; }

        public AutomationRequestContext(
            MobileParty party,
            Settlement settlement,
            CategorizedInventoryView merchantInventory,
            CategorizedInventoryView playerInventory)
        {
            Party = party;
            Settlement = settlement;
            MerchantInventory = merchantInventory;
            PlayerInventory = playerInventory;
        }

        public static AutomationRequestContext Empty(MobileParty party, Settlement settlement)
        {
            return new AutomationRequestContext(party, settlement, CategorizedInventoryView.Empty, CategorizedInventoryView.Empty);
        }

        public static AutomationRequestContext FromInventoryLogic(MobileParty party, Settlement settlement, InventoryLogic logic)
        {
            var merchant = BuildInventoryView(logic, InventoryLogic.InventorySide.OtherInventory, true);
            var player = BuildInventoryView(logic, InventoryLogic.InventorySide.PlayerInventory, false);
            return new AutomationRequestContext(party, settlement, merchant, player);
        }

        private static CategorizedInventoryView BuildInventoryView(InventoryLogic logic, InventoryLogic.InventorySide side, bool isMerchantSide)
        {
            var views = new List<InventoryItemView>();
            var elements = logic.GetElementsInRoster(side);
            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element.IsEmpty || element.Amount <= 0 || element.EquipmentElement.Item == null) continue;

                var categories = InventoryItemView.Classify(element.EquipmentElement.Item);
                int unitPrice = logic.GetItemPrice(element.EquipmentElement, isMerchantSide);
                views.Add(new InventoryItemView(side, element.EquipmentElement, element.Amount, unitPrice, categories));
            }

            return new CategorizedInventoryView(views);
        }
    }
}
