using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

namespace TradingOptimizer
{
    [ViewModelMixin("RefreshValues")]
    public class SPInventoryVMMixin : BaseViewModelMixin<SPInventoryVM>
    {
        public SPInventoryVMMixin(SPInventoryVM vm) : base(vm) { }

        [DataSourceMethod]
        public void ExecuteAutoTrade()
        {
            TradingPatches.ManualTrigger();
        }
    }
}
