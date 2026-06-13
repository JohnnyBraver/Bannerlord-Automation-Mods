using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TradeOptimizer
{
    [PrefabExtension("Inventory", "descendant::ButtonWidget[@Command.Click='ExecuteBuyAllItems']")]
    public class InventoryAutoTradePrefabExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionFileName(true)]
        public string MyXmlFile => "TradeOptimizerAutoTrade";
    }
}
