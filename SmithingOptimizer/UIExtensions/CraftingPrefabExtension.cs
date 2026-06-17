using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace SmithingOptimizer
{
    [PrefabExtension("Crafting", "descendant::ListPanel[@Id='RightBottomButtons']/Children")]
    public class CraftingOptimizePrefabExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;

        [PrefabExtensionFileName(true)]
        public string MyXmlFile => "SmithingOptimizerOptimize";
    }
}
