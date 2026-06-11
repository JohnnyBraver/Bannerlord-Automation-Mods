using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;

namespace TradingCore
{
    public class TradeOrder
    {
        public EquipmentElement EquipmentElement { get; }
        public int Amount { get; }
        public bool IsBuy { get; } // true = buy, false = sell
        
        public TradeOrder(EquipmentElement eqElement, int amount, bool isBuy)
        {
            EquipmentElement = eqElement;
            Amount = amount;
            IsBuy = isBuy;
        }
    }

    public interface ITradeOrderProvider
    {
        string ProviderName { get; }
        
        // Phase 1: Return orders that should execute first (e.g. to crash prices)
        List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement);
        
        // Phase 2: Return normal buy/sell orders based on current prices (after pre-sell completes)
        List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic);
    }

    public static class TradeCoordinator
    {
        private static readonly List<ITradeOrderProvider> Providers = new List<ITradeOrderProvider>();

        public static void RegisterProvider(ITradeOrderProvider provider)
        {
            lock (Providers)
            {
                if (!Providers.Contains(provider))
                {
                    Providers.Add(provider);
                }
            }
        }

        public static void UnregisterProvider(ITradeOrderProvider provider)
        {
            lock (Providers)
            {
                Providers.Remove(provider);
            }
        }

        public static IReadOnlyList<ITradeOrderProvider> ActiveProviders
        {
            get
            {
                lock (Providers)
                {
                    return new List<ITradeOrderProvider>(Providers);
                }
            }
        }
    }
}
