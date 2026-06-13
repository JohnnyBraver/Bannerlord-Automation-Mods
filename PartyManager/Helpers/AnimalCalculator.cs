using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace PartyManager.Helpers
{
    public class ItemRosterElementInfo
    {
        public EquipmentElement EquipmentElement { get; }
        public int Amount { get; }
        public ItemRosterElementInfo(EquipmentElement eq, int amount)
        {
            EquipmentElement = eq;
            Amount = amount;
        }
    }

    public static class AnimalCalculator
    {
        public static void CalculatePartyAnimals(MobileParty party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
            out List<ItemRosterElementInfo> ridingItems, out List<ItemRosterElementInfo> packItems, out List<ItemRosterElementInfo> livestockItems)
        {
            infantry = 0;
            cavalry = 0;
            riding = 0;
            pack = 0;
            livestock = 0;

            ridingItems = new List<ItemRosterElementInfo>();
            packItems = new List<ItemRosterElementInfo>();
            livestockItems = new List<ItemRosterElementInfo>();

            // Count Troops
            var memberRoster = party.MemberRoster;
            for (int i = 0; i < memberRoster.Count; i++)
            {
                var element = memberRoster.GetElementCopyAtIndex(i);
                if (element.Character != null)
                {
                    if (element.Character.IsMounted)
                    {
                        cavalry += element.Number;
                    }
                    else
                    {
                        infantry += element.Number;
                    }
                }
            }

            // Count Animals in inventory
            var itemRoster = party.ItemRoster;
            for (int i = 0; i < itemRoster.Count; i++)
            {
                var el = itemRoster.GetElementCopyAtIndex(i);
                var item = el.EquipmentElement.Item;
                if (item != null)
                {
                    if (item.IsAnimal)
                    {
                        livestock += el.Amount;
                        livestockItems.Add(new ItemRosterElementInfo(el.EquipmentElement, el.Amount));
                    }
                    else if (item.IsMountable && item.HorseComponent != null)
                    {
                        if (item.HorseComponent.IsPackAnimal)
                        {
                            pack += el.Amount;
                            packItems.Add(new ItemRosterElementInfo(el.EquipmentElement, el.Amount));
                        }
                        else
                        {
                            riding += el.Amount;
                            ridingItems.Add(new ItemRosterElementInfo(el.EquipmentElement, el.Amount));
                        }
                    }
                }
            }
        }
    }
}
