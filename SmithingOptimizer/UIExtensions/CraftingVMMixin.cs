using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;

namespace SmithingOptimizer
{
    [ViewModelMixin("RefreshValues")]
    public class CraftingVMMixin : BaseViewModelMixin<CraftingVM>
    {
        public CraftingVMMixin(CraftingVM vm) : base(vm) { }

        [DataSourceMethod]
        public void ExecuteSmithingOptimize()
        {
            CraftingPatches.ManualTrigger();
        }
    }
}
