using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

namespace SmithingOptimizer
{
    [ViewModelMixin("RefreshValues")]
    public class WeaponDesignVMMixin : BaseViewModelMixin<WeaponDesignVM>
    {
        public WeaponDesignVMMixin(WeaponDesignVM vm) : base(vm) { }

        [DataSourceMethod]
        public void ExecuteSmithingOptimize()
        {
            CraftingPatches.ManualTrigger();
        }
    }
}
