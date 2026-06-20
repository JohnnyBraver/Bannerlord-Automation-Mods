using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

    public enum SettlementRecruitmentSource
    {
        NotableVolunteer,
        TavernMercenary
    }

    public class SettlementRecruitmentCandidate
    {
        public SettlementRecruitmentSource Source { get; }
        public CharacterObject Troop { get; }
        public int AvailableCount { get; }
        public int UnitCost { get; }
        public Hero? Notable { get; }
        public int SlotIndex { get; }
        public int Sequence { get; }

        public SettlementRecruitmentCandidate(
            SettlementRecruitmentSource source,
            CharacterObject troop,
            int availableCount,
            int unitCost,
            Hero? notable,
            int slotIndex,
            int sequence)
        {
            Source = source;
            Troop = troop;
            AvailableCount = availableCount;
            UnitCost = unitCost;
            Notable = notable;
            SlotIndex = slotIndex;
            Sequence = sequence;
        }
    }

    public class SettlementRecruitmentContext
    {
        public MobileParty Party { get; }
        public Settlement Settlement { get; }
        public IReadOnlyList<SettlementRecruitmentCandidate> Candidates { get; }
        public int HeroGold { get; }
        public int PartySize { get; }
        public int PartySizeLimit { get; }
        public bool CanRecruitNotables { get; }
        public bool CanRecruitMercenaries { get; }

        public SettlementRecruitmentContext(
            MobileParty party,
            Settlement settlement,
            IReadOnlyList<SettlementRecruitmentCandidate> candidates,
            int heroGold,
            int partySize,
            int partySizeLimit,
            bool canRecruitNotables,
            bool canRecruitMercenaries)
        {
            Party = party;
            Settlement = settlement;
            Candidates = candidates;
            HeroGold = heroGold;
            PartySize = partySize;
            PartySizeLimit = partySizeLimit;
            CanRecruitNotables = canRecruitNotables;
            CanRecruitMercenaries = canRecruitMercenaries;
        }
    }

    public class SettlementRecruitmentOrder
    {
        public SettlementRecruitmentCandidate Candidate { get; }
        public int Amount { get; }
        public bool AllowOverPartySize { get; }

        public SettlementRecruitmentOrder(SettlementRecruitmentCandidate candidate, int amount, bool allowOverPartySize = false)
        {
            Candidate = candidate;
            Amount = amount;
            AllowOverPartySize = allowOverPartySize;
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

    public enum PrisonerDispositionAction
    {
        Ransom,
        DonateToDungeon
    }

    public class PrisonerDispositionContext
    {
        public MobileParty Party { get; }
        public Settlement Settlement { get; }
        public IReadOnlyList<PrisonerStack> Prisoners { get; }
        public int PrisonerCount { get; }
        public int PrisonerSizeLimit { get; }
        public bool CanRansom { get; }
        public bool CanDonateToDungeon { get; }

        public PrisonerDispositionContext(
            MobileParty party,
            Settlement settlement,
            IReadOnlyList<PrisonerStack> prisoners,
            int prisonerCount,
            int prisonerSizeLimit,
            bool canRansom,
            bool canDonateToDungeon)
        {
            Party = party;
            Settlement = settlement;
            Prisoners = prisoners;
            PrisonerCount = prisonerCount;
            PrisonerSizeLimit = prisonerSizeLimit;
            CanRansom = canRansom;
            CanDonateToDungeon = canDonateToDungeon;
        }
    }

    public class PrisonerStack
    {
        public CharacterObject Prisoner { get; }
        public int Amount { get; }
        
        public PrisonerStack(CharacterObject prisoner, int amount)
        {
            Prisoner = prisoner;
            Amount = amount;
        }
    }

    public class PrisonerDispositionOrder
    {
        public CharacterObject Prisoner { get; }
        public int Amount { get; }
        public PrisonerDispositionAction Action { get; }

        public PrisonerDispositionOrder(CharacterObject prisoner, int amount, PrisonerDispositionAction action)
        {
            Prisoner = prisoner;
            Amount = amount;
            Action = action;
        }
    }

    // ----------------------------------------------------
    // Provider Interfaces
    // ----------------------------------------------------
    public interface IAutomationPreparationProvider
    {
        string ProviderName { get; }
        void PrepareForAutomation(MobileParty party, Settlement settlement);
    }

    public interface IPreSellProvider
    {
        string ProviderName { get; }
        List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement);
    }

    public interface IFreeTradeAnalyzer
    {
        string ProviderName { get; }
        TradeProposal AnalyzeMarket(TradeContext context);
    }

    public interface ISettlementCleanupProvider
    {
        string ProviderName { get; }
        TradeProposal AnalyzeSettlementCleanup(TradeContext context);
    }

    public interface ISettlementRecruitmentProvider
    {
        string ProviderName { get; }
        IReadOnlyList<SettlementRecruitmentOrder> GetRecruitmentOrders(SettlementRecruitmentContext context);
    }

    public interface IGarrisonOrderProvider
    {
        string ProviderName { get; }
        List<GarrisonOrder> GetGarrisonOrders(MobileParty party, Settlement settlement);
    }

    public interface IPrisonerDispositionProvider
    {
        string ProviderName { get; }
        IReadOnlyList<PrisonerDispositionOrder> GetPrisonerDispositionOrders(PrisonerDispositionContext context);
    }

    public interface IFiefAutomationProvider
    {
        string ProviderName { get; }
        IReadOnlyList<FiefAutomationOrder> GetFiefAutomationOrders(MobileParty party, Settlement settlement, bool isSurplusPhase);
    }

    // ----------------------------------------------------
    // Automation Registry
    // ----------------------------------------------------
    public static class AutomationRegistry
    {
        private static readonly List<ProviderRegistration<IAutomationPreparationProvider>> PreparationProviders = new();
        private static readonly List<ProviderRegistration<IPreSellProvider>> PreSellProviders = new();
        private static readonly List<ProviderRegistration<IFreeTradeAnalyzer>> FreeTradeAnalyzers = new();
        private static readonly List<ProviderRegistration<ISettlementCleanupProvider>> SettlementCleanupProviders = new();
        private static readonly List<ProviderRegistration<ISettlementRecruitmentProvider>> RecruitmentProviders = new();
        private static readonly List<ProviderRegistration<IGarrisonOrderProvider>> GarrisonProviders = new();
        private static readonly List<ProviderRegistration<IPrisonerDispositionProvider>> PrisonerDispositionProviders = new();
        private static readonly List<ProviderRegistration<IFiefAutomationProvider>> FiefProviders = new();
        private static readonly List<ProviderRegistration<IPostBattleAutomationProvider>> PostBattleProviders = new();
        private static readonly List<ProviderRegistration<IAutomationReservationProvider>> ReservationProviders = new();
        private static readonly List<ProviderRegistration<IAutomationReportProvider>> ReportProviders = new();

        private static readonly List<ProviderRegistration<IAutomationRequestProvider>> RequestProviders = new();
        private static readonly List<AutomationRequest> CurrentRequests = new();
        private static readonly List<ItemReservation> CurrentReservations = new();
        private static readonly HashSet<string> WarnedConflictProviderSets = new();

        // --- Preparation Providers ---
        public static void RegisterPreparationProvider(IAutomationPreparationProvider provider)
        {
            lock (PreparationProviders)
            {
                if (!PreparationProviders.Any(r => EqualityComparer<IAutomationPreparationProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    PreparationProviders.Add(new ProviderRegistration<IAutomationPreparationProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterPreparationProvider(IAutomationPreparationProvider provider)
        {
            lock (PreparationProviders)
            {
                PreparationProviders.RemoveAll(r => EqualityComparer<IAutomationPreparationProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IAutomationPreparationProvider>> ActivePreparationProviders
        {
            get
            {
                lock (PreparationProviders)
                {
                    return new List<ProviderRegistration<IAutomationPreparationProvider>>(PreparationProviders);
                }
            }
        }

        // --- Pre-Sell Providers ---
        public static void RegisterPreSellProvider(IPreSellProvider provider)
        {
            lock (PreSellProviders)
            {
                if (!PreSellProviders.Any(r => EqualityComparer<IPreSellProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    PreSellProviders.Add(new ProviderRegistration<IPreSellProvider>(provider, provider.ProviderName, callingAssembly));
                    WarnIfProviderSetMayConflict("pre-sell trade", PreSellProviders);
                }
            }
        }

        public static void UnregisterPreSellProvider(IPreSellProvider provider)
        {
            lock (PreSellProviders)
            {
                PreSellProviders.RemoveAll(r => EqualityComparer<IPreSellProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IPreSellProvider>> ActivePreSellProviders
        {
            get
            {
                lock (PreSellProviders)
                {
                    return new List<ProviderRegistration<IPreSellProvider>>(PreSellProviders);
                }
            }
        }

        // --- Free Trade Analyzers ---
        public static void RegisterFreeTradeAnalyzer(IFreeTradeAnalyzer provider)
        {
            lock (FreeTradeAnalyzers)
            {
                if (!FreeTradeAnalyzers.Any(r => EqualityComparer<IFreeTradeAnalyzer>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    FreeTradeAnalyzers.Add(new ProviderRegistration<IFreeTradeAnalyzer>(provider, provider.ProviderName, callingAssembly));
                    WarnIfProviderSetMayConflict("free trade", FreeTradeAnalyzers);
                }
            }
        }

        public static void UnregisterFreeTradeAnalyzer(IFreeTradeAnalyzer provider)
        {
            lock (FreeTradeAnalyzers)
            {
                FreeTradeAnalyzers.RemoveAll(r => EqualityComparer<IFreeTradeAnalyzer>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IFreeTradeAnalyzer>> ActiveFreeTradeAnalyzers
        {
            get
            {
                lock (FreeTradeAnalyzers)
                {
                    return new List<ProviderRegistration<IFreeTradeAnalyzer>>(FreeTradeAnalyzers);
                }
            }
        }

        // --- Settlement Cleanup Providers ---
        public static void RegisterSettlementCleanupProvider(ISettlementCleanupProvider provider)
        {
            lock (SettlementCleanupProviders)
            {
                if (!SettlementCleanupProviders.Any(r => EqualityComparer<ISettlementCleanupProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    SettlementCleanupProviders.Add(new ProviderRegistration<ISettlementCleanupProvider>(provider, provider.ProviderName, callingAssembly));
                    WarnIfProviderSetMayConflict("settlement cleanup", SettlementCleanupProviders);
                }
            }
        }

        public static void UnregisterSettlementCleanupProvider(ISettlementCleanupProvider provider)
        {
            lock (SettlementCleanupProviders)
            {
                SettlementCleanupProviders.RemoveAll(r => EqualityComparer<ISettlementCleanupProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<ISettlementCleanupProvider>> ActiveSettlementCleanupProviders
        {
            get
            {
                lock (SettlementCleanupProviders)
                {
                    return new List<ProviderRegistration<ISettlementCleanupProvider>>(SettlementCleanupProviders);
                }
            }
        }

        // --- Recruitment Providers ---
        public static void RegisterSettlementRecruitmentProvider(ISettlementRecruitmentProvider provider)
        {
            lock (RecruitmentProviders)
            {
                if (!RecruitmentProviders.Any(r => EqualityComparer<ISettlementRecruitmentProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    RecruitmentProviders.Add(new ProviderRegistration<ISettlementRecruitmentProvider>(provider, provider.ProviderName, callingAssembly));
                    WarnIfProviderSetMayConflict("settlement recruitment", RecruitmentProviders);
                }
            }
        }

        public static void UnregisterSettlementRecruitmentProvider(ISettlementRecruitmentProvider provider)
        {
            lock (RecruitmentProviders)
            {
                RecruitmentProviders.RemoveAll(r => EqualityComparer<ISettlementRecruitmentProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<ISettlementRecruitmentProvider>> ActiveSettlementRecruitmentProviders
        {
            get
            {
                lock (RecruitmentProviders)
                {
                    return new List<ProviderRegistration<ISettlementRecruitmentProvider>>(RecruitmentProviders);
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
                    WarnIfProviderSetMayConflict("garrison donation", GarrisonProviders);
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

        // --- Prisoner Disposition Providers ---
        public static void RegisterPrisonerDispositionProvider(IPrisonerDispositionProvider provider)
        {
            lock (PrisonerDispositionProviders)
            {
                if (!PrisonerDispositionProviders.Any(r => EqualityComparer<IPrisonerDispositionProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    PrisonerDispositionProviders.Add(new ProviderRegistration<IPrisonerDispositionProvider>(provider, provider.ProviderName, callingAssembly));
                    WarnIfProviderSetMayConflict("prisoner disposition", PrisonerDispositionProviders);
                }
            }
        }

        public static void UnregisterPrisonerDispositionProvider(IPrisonerDispositionProvider provider)
        {
            lock (PrisonerDispositionProviders)
            {
                PrisonerDispositionProviders.RemoveAll(r => EqualityComparer<IPrisonerDispositionProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IPrisonerDispositionProvider>> ActivePrisonerDispositionProviders
        {
            get
            {
                lock (PrisonerDispositionProviders)
                {
                    return new List<ProviderRegistration<IPrisonerDispositionProvider>>(PrisonerDispositionProviders);
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
                    WarnIfProviderSetMayConflict("fief automation", FiefProviders);
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

        // --- Post-Battle Providers ---
        public static void RegisterPostBattleProvider(IPostBattleAutomationProvider provider)
        {
            lock (PostBattleProviders)
            {
                if (!PostBattleProviders.Any(r => EqualityComparer<IPostBattleAutomationProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    PostBattleProviders.Add(new ProviderRegistration<IPostBattleAutomationProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterPostBattleProvider(IPostBattleAutomationProvider provider)
        {
            lock (PostBattleProviders)
            {
                PostBattleProviders.RemoveAll(r => EqualityComparer<IPostBattleAutomationProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IPostBattleAutomationProvider>> ActivePostBattleProviders
        {
            get
            {
                lock (PostBattleProviders)
                {
                    return new List<ProviderRegistration<IPostBattleAutomationProvider>>(PostBattleProviders);
                }
            }
        }

        // --- Reservation Providers ---
        public static void RegisterReservationProvider(IAutomationReservationProvider provider)
        {
            lock (ReservationProviders)
            {
                if (!ReservationProviders.Any(r => EqualityComparer<IAutomationReservationProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    ReservationProviders.Add(new ProviderRegistration<IAutomationReservationProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterReservationProvider(IAutomationReservationProvider provider)
        {
            lock (ReservationProviders)
            {
                ReservationProviders.RemoveAll(r => EqualityComparer<IAutomationReservationProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IAutomationReservationProvider>> ActiveReservationProviders
        {
            get
            {
                lock (ReservationProviders)
                {
                    return new List<ProviderRegistration<IAutomationReservationProvider>>(ReservationProviders);
                }
            }
        }

        // --- Report Providers ---
        public static void RegisterReportProvider(IAutomationReportProvider provider)
        {
            lock (ReportProviders)
            {
                if (!ReportProviders.Any(r => EqualityComparer<IAutomationReportProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    ReportProviders.Add(new ProviderRegistration<IAutomationReportProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterReportProvider(IAutomationReportProvider provider)
        {
            lock (ReportProviders)
            {
                ReportProviders.RemoveAll(r => EqualityComparer<IAutomationReportProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IAutomationReportProvider>> ActiveReportProviders
        {
            get
            {
                lock (ReportProviders)
                {
                    return new List<ProviderRegistration<IAutomationReportProvider>>(ReportProviders);
                }
            }
        }

        // --- Request Providers ---
        public static void RegisterRequestProvider(IAutomationRequestProvider provider)
        {
            lock (RequestProviders)
            {
                if (!RequestProviders.Any(r => EqualityComparer<IAutomationRequestProvider>.Default.Equals(r.Provider, provider)))
                {
                    string callingAssembly = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";
                    RequestProviders.Add(new ProviderRegistration<IAutomationRequestProvider>(provider, provider.ProviderName, callingAssembly));
                }
            }
        }

        public static void UnregisterRequestProvider(IAutomationRequestProvider provider)
        {
            lock (RequestProviders)
            {
                RequestProviders.RemoveAll(r => EqualityComparer<IAutomationRequestProvider>.Default.Equals(r.Provider, provider));
            }
        }

        public static IReadOnlyList<ProviderRegistration<IAutomationRequestProvider>> ActiveRequestProviders
        {
            get
            {
                lock (RequestProviders)
                {
                    return new List<ProviderRegistration<IAutomationRequestProvider>>(RequestProviders);
                }
            }
        }

        public static void ClearRequests()
        {
            lock (CurrentRequests)
            {
                CurrentRequests.Clear();
            }
        }

        public static void RegisterRequest(AutomationRequest request)
        {
            lock (CurrentRequests)
            {
                CurrentRequests.Add(request);
            }
        }

        public static IReadOnlyList<AutomationRequest> ActiveRequests
        {
            get
            {
                lock (CurrentRequests)
                {
                    return new List<AutomationRequest>(CurrentRequests);
                }
            }
        }

        // --- Reservation Registries ---
        public static void ClearReservations()
        {
            lock (CurrentReservations)
            {
                CurrentReservations.Clear();
            }
        }

        public static void RegisterReservation(ItemReservation reservation)
        {
            lock (CurrentReservations)
            {
                CurrentReservations.Add(reservation);
            }
        }

        public static IReadOnlyList<ItemReservation> ActiveReservations
        {
            get
            {
                lock (CurrentReservations)
                {
                    return new List<ItemReservation>(CurrentReservations);
                }
            }
        }

        internal static IReadOnlyList<string> BuildProviderConflictWarnings()
        {
            var warnings = new List<string>();
            AddConflictWarning(warnings, "pre-sell trade", ActivePreSellProviders);
            AddConflictWarning(warnings, "free trade", ActiveFreeTradeAnalyzers);
            AddConflictWarning(warnings, "settlement cleanup", ActiveSettlementCleanupProviders);
            AddConflictWarning(warnings, "settlement recruitment", ActiveSettlementRecruitmentProviders);
            AddConflictWarning(warnings, "garrison donation", ActiveGarrisonProviders);
            AddConflictWarning(warnings, "prisoner disposition", ActivePrisonerDispositionProviders);
            AddConflictWarning(warnings, "fief automation", ActiveFiefProviders);
            return warnings;
        }

        private static void WarnIfProviderSetMayConflict<T>(string label, IReadOnlyList<ProviderRegistration<T>> registrations)
        {
            var warning = BuildProviderConflictWarning(label, registrations);
            if (warning == null)
            {
                return;
            }

            var key = BuildProviderConflictKey(label, registrations);
            lock (WarnedConflictProviderSets)
            {
                if (!WarnedConflictProviderSets.Add(key))
                {
                    return;
                }
            }

            Helpers.Logger.WriteLog("SettlementAutomationCore", warning);
        }

        private static void AddConflictWarning<T>(List<string> warnings, string label, IReadOnlyList<ProviderRegistration<T>> registrations)
        {
            var warning = BuildProviderConflictWarning(label, registrations);
            if (warning != null)
            {
                warnings.Add(warning);
            }
        }

        private static string? BuildProviderConflictWarning<T>(string label, IReadOnlyList<ProviderRegistration<T>> registrations)
        {
            if (registrations.Count <= 1)
            {
                return null;
            }

            string providers = string.Join(", ", registrations.Select(reg => reg.ProviderName).Distinct().OrderBy(name => name));
            return $"WARNING: Multiple providers registered for {label}: {providers}. These providers share the same game-state pool and can submit conflicting orders; Core executes them in registration order and clamps unavailable quantities.";
        }

        private static string BuildProviderConflictKey<T>(string label, IReadOnlyList<ProviderRegistration<T>> registrations)
        {
            string providers = string.Join("|", registrations.Select(reg => reg.ProviderName).Distinct().OrderBy(name => name));
            return $"{label}:{providers}";
        }
    }
}
