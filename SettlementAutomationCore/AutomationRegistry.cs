using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;

namespace SettlementAutomationCore
{
    // ----------------------------------------------------
    // Provider Wrapper for Debugging Traceability
    // ----------------------------------------------------
    public class ProviderRegistration<T>
    {
        public T Provider { get; }
        public string ProviderName { get; }
        public string RegisteredByAssembly { get; }
        public DateTime RegisteredAt { get; }

        public ProviderRegistration(T provider, string providerName, string assemblyName)
        {
            Provider = provider;
            ProviderName = providerName;
            RegisteredByAssembly = assemblyName;
            RegisteredAt = DateTime.Now;
        }
    }

    // ----------------------------------------------------
    // Provider Interfaces
    // ----------------------------------------------------
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
        List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement);
        List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic);
    }

    // Planned for future recruitment automation (PartyManager)
    public interface IRecruitOrderProvider
    {
        string ProviderName { get; }
        // For recruiting logic (spends gold, modifies member roster)
    }

    // Planned for future prisoner management (PrisonerManager)
    public interface IPrisonerOrderProvider
    {
        string ProviderName { get; }
        // For ransoming and donations logic
    }

    // ----------------------------------------------------
    // Automation Registry
    // ----------------------------------------------------
    public static class AutomationRegistry
    {
        private static readonly List<ProviderRegistration<ITradeOrderProvider>> TradeProviders = new();
        private static readonly List<ProviderRegistration<IRecruitOrderProvider>> RecruitProviders = new();
        private static readonly List<ProviderRegistration<IPrisonerOrderProvider>> PrisonerProviders = new();

        // --- Trade Providers ---
        public static void RegisterTradeProvider(ITradeOrderProvider provider)
        {
            lock (TradeProviders)
            {
                if (!TradeProviders.Any(r => EqualityComparer<ITradeOrderProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    TradeProviders.Add(new ProviderRegistration<ITradeOrderProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterTradeProvider(ITradeOrderProvider provider)
        {
            lock (TradeProviders)
            {
                TradeProviders.RemoveAll(r => EqualityComparer<ITradeOrderProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<ITradeOrderProvider>> ActiveTradeProviders
        {
            get
            {
                lock (TradeProviders)
                {
                    return new List<ProviderRegistration<ITradeOrderProvider>>(TradeProviders);
                }
            }
        }

        // --- Recruit Providers ---
        public static void RegisterRecruitProvider(IRecruitOrderProvider provider)
        {
            lock (RecruitProviders)
            {
                if (!RecruitProviders.Any(r => EqualityComparer<IRecruitOrderProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    RecruitProviders.Add(new ProviderRegistration<IRecruitOrderProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterRecruitProvider(IRecruitOrderProvider provider)
        {
            lock (RecruitProviders)
            {
                RecruitProviders.RemoveAll(r => EqualityComparer<IRecruitOrderProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IRecruitOrderProvider>> ActiveRecruitProviders
        {
            get
            {
                lock (RecruitProviders)
                {
                    return new List<ProviderRegistration<IRecruitOrderProvider>>(RecruitProviders);
                }
            }
        }

        // --- Prisoner Providers ---
        public static void RegisterPrisonerProvider(IPrisonerOrderProvider provider)
        {
            lock (PrisonerProviders)
            {
                if (!PrisonerProviders.Any(r => EqualityComparer<IPrisonerOrderProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    PrisonerProviders.Add(new ProviderRegistration<IPrisonerOrderProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterPrisonerProvider(IPrisonerOrderProvider provider)
        {
            lock (PrisonerProviders)
            {
                PrisonerProviders.RemoveAll(r => EqualityComparer<IPrisonerOrderProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IPrisonerOrderProvider>> ActivePrisonerProviders
        {
            get
            {
                lock (PrisonerProviders)
                {
                    return new List<ProviderRegistration<IPrisonerOrderProvider>>(PrisonerProviders);
                }
            }
        }
    }
}
