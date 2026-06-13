using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
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
    // Order Types
    // ----------------------------------------------------
    public class TradeOrder
    {
        public EquipmentElement EquipmentElement { get; }
        public int Amount { get; }
        public bool IsBuy { get; } // true = buy, false = sell
        public bool IsSlaughter { get; } // true = slaughter animal
        
        public TradeOrder(EquipmentElement eqElement, int amount, bool isBuy, bool isSlaughter = false)
        {
            EquipmentElement = eqElement;
            Amount = amount;
            IsBuy = isBuy;
            IsSlaughter = isSlaughter;
        }
    }

    public static class HerdingCalculator
    {
        public static int GetMaxAnimalsAllowed(MobileParty party)
        {
            if (party == null) return 0;
            int infantry = 0;
            int cavalry = 0;
            var memberRoster = party.MemberRoster;
            for (int i = 0; i < memberRoster.Count; i++)
            {
                var el = memberRoster.GetElementCopyAtIndex(i);
                if (el.Character != null)
                {
                    if (el.Character.IsMounted)
                    {
                        cavalry += el.Number;
                    }
                    else
                    {
                        infantry += el.Number;
                    }
                }
            }
            return (infantry * 2) + (cavalry * 1);
        }

        public static int GetCurrentAnimalsCount(MobileParty party)
        {
            if (party == null) return 0;

            int infantry = 0;
            var memberRoster = party.MemberRoster;
            for (int i = 0; i < memberRoster.Count; i++)
            {
                var el = memberRoster.GetElementCopyAtIndex(i);
                if (el.Character != null && !el.Character.IsMounted)
                {
                    infantry += el.Number;
                }
            }

            int riding = 0;
            int pack = 0;
            int livestock = 0;

            var itemRoster = party.ItemRoster;
            for (int i = 0; i < itemRoster.Count; i++)
            {
                var el = itemRoster.GetElementCopyAtIndex(i);
                var item = el.EquipmentElement.Item;
                if (item != null)
                {
                    if (item.IsAnimal && !item.IsMountable)
                    {
                        livestock += el.Amount;
                    }
                    else if (item.IsMountable && item.HorseComponent != null)
                    {
                        if (item.HorseComponent.IsPackAnimal)
                        {
                            pack += el.Amount;
                        }
                        else
                        {
                            riding += el.Amount;
                        }
                    }
                }
            }

            return pack + livestock + Math.Max(0, riding - infantry);
        }

        public static int GetRemainingAnimalSlots(MobileParty party)
        {
            return Math.Max(0, GetMaxAnimalsAllowed(party) - GetCurrentAnimalsCount(party));
        }
    }

    public class RecruitOrder
    {
        public Hero Notable { get; }
        public int SlotIndex { get; }
        
        public RecruitOrder(Hero notable, int slotIndex)
        {
            Notable = notable;
            SlotIndex = slotIndex;
        }
    }

    public class GarrisonOrder
    {
        public CharacterObject Troop { get; }
        public int Amount { get; }
        
        public GarrisonOrder(CharacterObject troop, int amount)
        {
            Troop = troop;
            Amount = amount;
        }
    }

    public class RansomOrder
    {
        public CharacterObject Prisoner { get; }
        public int Amount { get; }
        
        public RansomOrder(CharacterObject prisoner, int amount)
        {
            Prisoner = prisoner;
            Amount = amount;
        }
    }

    public class MercenaryRecruitOrder
    {
        public CharacterObject Troop { get; }
        public int Amount { get; }
        
        public MercenaryRecruitOrder(CharacterObject troop, int amount)
        {
            Troop = troop;
            Amount = amount;
        }
    }

    public class DungeonOrder
    {
        public CharacterObject Prisoner { get; }
        public int Amount { get; }
        
        public DungeonOrder(CharacterObject prisoner, int amount)
        {
            Prisoner = prisoner;
            Amount = amount;
        }
    }

    // ----------------------------------------------------
    // Provider Interfaces
    // ----------------------------------------------------
    public interface ITradeOrderProvider
    {
        string ProviderName { get; }
        List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement);
        List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic);
    }

    public interface IRecruitOrderProvider
    {
        string ProviderName { get; }
        List<RecruitOrder> GetRecruitOrders(MobileParty party, Settlement settlement);
    }

    public interface IGarrisonOrderProvider
    {
        string ProviderName { get; }
        List<GarrisonOrder> GetGarrisonOrders(MobileParty party, Settlement settlement);
    }

    public interface IRansomOrderProvider
    {
        string ProviderName { get; }
        List<RansomOrder> GetRansomOrders(MobileParty party, Settlement settlement);
        List<MercenaryRecruitOrder> GetMercenaryRecruitOrders(MobileParty party, Settlement settlement);
    }

    public interface IDungeonOrderProvider
    {
        string ProviderName { get; }
        List<DungeonOrder> GetDungeonOrders(MobileParty party, Settlement settlement);
    }

    public enum LogisticsGoalType
    {
        FoodRestock,
        SpeedMounts
    }

    public class LogisticsGoal
    {
        public LogisticsGoalType GoalType { get; }
        public int TargetQuantity { get; }
        public int MinGoldReserve { get; }
        public bool IsSurvivalMode { get; }

        public LogisticsGoal(LogisticsGoalType goalType, int targetQuantity, int minGoldReserve, bool isSurvivalMode = false)
        {
            GoalType = goalType;
            TargetQuantity = targetQuantity;
            MinGoldReserve = minGoldReserve;
            IsSurvivalMode = isSurvivalMode;
        }
    }

    public interface ILogisticsGoalProvider
    {
        string ProviderName { get; }
        void SubmitLogisticsGoals(MobileParty party, Settlement settlement);
    }

    public interface IFiefAutomationProvider
    {
        string ProviderName { get; }
        void ProcessFiefAutomation(MobileParty party, Settlement settlement);
    }

    // ----------------------------------------------------
    // Automation Registry
    // ----------------------------------------------------
    public static class AutomationRegistry
    {
        private static readonly List<ProviderRegistration<ITradeOrderProvider>> TradeProviders = new();
        private static readonly List<ProviderRegistration<IRecruitOrderProvider>> RecruitProviders = new();
        private static readonly List<ProviderRegistration<IGarrisonOrderProvider>> GarrisonProviders = new();
        private static readonly List<ProviderRegistration<IRansomOrderProvider>> RansomProviders = new();
        private static readonly List<ProviderRegistration<IDungeonOrderProvider>> DungeonProviders = new();
        private static readonly List<ProviderRegistration<IFiefAutomationProvider>> FiefProviders = new();
        private static readonly List<ProviderRegistration<ILogisticsGoalProvider>> GoalProviders = new();
        private static readonly List<LogisticsGoal> CurrentGoals = new();

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

        // --- Garrison Providers ---
        public static void RegisterGarrisonProvider(IGarrisonOrderProvider provider)
        {
            lock (GarrisonProviders)
            {
                if (!GarrisonProviders.Any(r => EqualityComparer<IGarrisonOrderProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    GarrisonProviders.Add(new ProviderRegistration<IGarrisonOrderProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterGarrisonProvider(IGarrisonOrderProvider provider)
        {
            lock (GarrisonProviders)
            {
                GarrisonProviders.RemoveAll(r => EqualityComparer<IGarrisonOrderProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IGarrisonOrderProvider>> ActiveGarrisonProviders
        {
            get
            {
                lock (GarrisonProviders)
                {
                    return new List<ProviderRegistration<IGarrisonOrderProvider>>(GarrisonProviders);
                }
            }
        }

        // --- Ransom Providers ---
        public static void RegisterRansomProvider(IRansomOrderProvider provider)
        {
            lock (RansomProviders)
            {
                if (!RansomProviders.Any(r => EqualityComparer<IRansomOrderProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    RansomProviders.Add(new ProviderRegistration<IRansomOrderProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterRansomProvider(IRansomOrderProvider provider)
        {
            lock (RansomProviders)
            {
                RansomProviders.RemoveAll(r => EqualityComparer<IRansomOrderProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IRansomOrderProvider>> ActiveRansomProviders
        {
            get
            {
                lock (RansomProviders)
                {
                    return new List<ProviderRegistration<IRansomOrderProvider>>(RansomProviders);
                }
            }
        }

        // --- Dungeon Providers ---
        public static void RegisterDungeonProvider(IDungeonOrderProvider provider)
        {
            lock (DungeonProviders)
            {
                if (!DungeonProviders.Any(r => EqualityComparer<IDungeonOrderProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    DungeonProviders.Add(new ProviderRegistration<IDungeonOrderProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterDungeonProvider(IDungeonOrderProvider provider)
        {
            lock (DungeonProviders)
            {
                DungeonProviders.RemoveAll(r => EqualityComparer<IDungeonOrderProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IDungeonOrderProvider>> ActiveDungeonProviders
        {
            get
            {
                lock (DungeonProviders)
                {
                    return new List<ProviderRegistration<IDungeonOrderProvider>>(DungeonProviders);
                }
            }
        }

        // --- Fief Providers ---
        public static void RegisterFiefProvider(IFiefAutomationProvider provider)
        {
            lock (FiefProviders)
            {
                if (!FiefProviders.Any(r => EqualityComparer<IFiefAutomationProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    FiefProviders.Add(new ProviderRegistration<IFiefAutomationProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterFiefProvider(IFiefAutomationProvider provider)
        {
            lock (FiefProviders)
            {
                FiefProviders.RemoveAll(r => EqualityComparer<IFiefAutomationProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IFiefAutomationProvider>> ActiveFiefProviders
        {
            get
            {
                lock (FiefProviders)
                {
                    return new List<ProviderRegistration<IFiefAutomationProvider>>(FiefProviders);
                }
            }
        }

        // --- Goal Providers & Goals ---
        public static void RegisterGoalProvider(ILogisticsGoalProvider provider)
        {
            lock (GoalProviders)
            {
                if (!GoalProviders.Any(r => EqualityComparer<ILogisticsGoalProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    GoalProviders.Add(new ProviderRegistration<ILogisticsGoalProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterGoalProvider(ILogisticsGoalProvider provider)
        {
            lock (GoalProviders)
            {
                GoalProviders.RemoveAll(r => EqualityComparer<ILogisticsGoalProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<ILogisticsGoalProvider>> ActiveGoalProviders
        {
            get
            {
                lock (GoalProviders)
                {
                    return new List<ProviderRegistration<ILogisticsGoalProvider>>(GoalProviders);
                }
            }
        }

        public static void ClearLogisticsGoals()
        {
            lock (CurrentGoals)
            {
                CurrentGoals.Clear();
            }
        }

        public static void RegisterLogisticsGoal(LogisticsGoal goal)
        {
            lock (CurrentGoals)
            {
                CurrentGoals.Add(goal);
            }
        }

        public static IReadOnlyList<LogisticsGoal> ActiveLogisticsGoals
        {
            get
            {
                lock (CurrentGoals)
                {
                    return new List<LogisticsGoal>(CurrentGoals);
                }
            }
        }

        public static bool IsTradeOptimizerActive()
        {
            lock (TradeProviders)
            {
                return TradeProviders.Any(p => p.ProviderName == "TradingOptimizer" || p.ProviderName == "TradeOptimizer");
            }
        }
    }
}
