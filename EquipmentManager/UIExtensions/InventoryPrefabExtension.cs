using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace EquipmentManager
{
    [PrefabExtension("Inventory", "descendant::ButtonWidget[@Command.Click='ExecuteSellAllItems']")]
    public class InventoryEquipPrefabExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionFileName(true)]
        public string MyXmlFile => "EquipmentManagerEquip";
    }
}
