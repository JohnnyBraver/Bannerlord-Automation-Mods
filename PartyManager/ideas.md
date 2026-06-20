# Party Manager - Feature Outline

This mod will handle party configuration, troop recruitment, mounts management, and speed optimization upon entering settlements.

## Core Features

1. **Auto-Recruitment (Phase 2: Roster & Party Operations)**
   - Automatically hires troops from the settlement recruiters.
   - **Customizable Filters**:
     - **Troop Class**: Regular, Noble, Mercenary.
     - **Level Bounds**: Min/Max tiers to recruit.
     - **Combat Archetype**: Throwing, Shield/Melee, Bow/Ranged, Crossbow.
     - **Evaluation Time**: Evaluates the filter against the *final upgrade tier* of the troop tree (recommended) or the *purchase time tier*.
     - **Mounted Toggles**: Recruit only cavalry, only infantry, or both.
   - Automatically checks remaining party capacity and spends gold within budget limits.
   - **Garrison Donation for Influence (Idea)**:
     - Option to recruit troops even when over the party capacity cap.
     - Automatically dumps those excess recruits into the settlement's garrison to gain **Influence** cheaply.
     - Custom filters for influence thresholds, min cost-per-influence, and garrison capacity.

2. **Mounts & Carry Capacity Management (Phase 3: Supplies & Investment)**
   - Auto-purchase riding/pack animals to maximize party speed and carry capacity.
   - Cap purchase levels based on party composition (e.g. mounted units support 1 spare horse; foot troops support up to 2 mounts to ride for speed upgrades).
   - **Herding Protection**: Automatically sells excess livestock/mounts when entering a town to prevent triggering the "herding speed penalty" (triggered when herd size > party size).
   - Loss limit settings to prevent selling animals at extreme losses unless herding penalty is severe.

## Integration with SettlementAutomationCore

```csharp
public class PartyManagerProvider : ISettlementRecruitmentProvider, IPrisonerDispositionProvider, IAutomationRequestProvider
{
    public string ProviderName => "PartyManager";

    // Returns ordered intent for available notable volunteers and tavern mercenaries.
    public IReadOnlyList<SettlementRecruitmentOrder> GetRecruitmentOrders(SettlementRecruitmentContext context);

    // Returns ordered intent for prisoner ransom or dungeon donation.
    public IReadOnlyList<PrisonerDispositionOrder> GetPrisonerDispositionOrders(PrisonerDispositionContext context);
}
```
