# Prisoner Manager - Feature Outline

This mod will manage party prisoners automatically upon entering settlements, integrating with `SettlementAutomationCore` to coordinate ransoming and donations.

## Core Features

1. **Auto-Ransom Prisoners (Phase 1: Revenue Generation)**
   - Automatically ransoms standard prisoners in taverns for gold.
   - Respects player settings for:
     - Minimum tier to ransom.
     - Keep certain troop types (e.g. noble prisoners or specific cultures if the player intends to recruit them later).
     - Keep hero prisoners (never auto-ransom heroes unless explicitly toggled).

2. **Auto-Donate Prisoners (Phase 2: Roster & Party Operations)**
   - Automatically donates prisoners to friendly keeps/dungeons to gain **Influence** and **Steward/Leadership XP**.
   - Prioritizes high-tier prisoners for maximum influence returns.
   - Respects a customizable cap on influence to avoid wasting prisoners once daily limits or targets are reached.

## Integration with SettlementAutomationCore

```csharp
public class PrisonerManagerProvider : IPrisonerOrderProvider
{
    public string ProviderName => "PrisonerManager";

    // Returns prisoners to ransom for gold in Phase 1
    public List<PrisonerRansomOrder> GetRansomOrders(MobileParty party, Settlement settlement);

    // Returns prisoners to donate to keeps in Phase 2
    public List<PrisonerDonateOrder> GetDonateOrders(MobileParty party, Settlement settlement);
}
```
