# Temporary Feedback Triage - 2026-06-17

Raw notes from live testing and feedback. This is intentionally temporary: keep the trail here, then split into focused fixes/issues once we start implementation.

## Trade Optimizer

- Village trade execution does not appear to reduce village gold supply correctly.
  - Example: sold 37 hardwood at 70 coins each.
  - Village still showed full 1,000 gold in the UI afterward.
  - Trade XP seemed to award normally.
  - Sometimes XP and gold do not apply correctly, especially in villages.
- Trading mode may not be working as intended.
  - Balanced mode can hold a full inventory.
  - Tester did not have trade perks yet; confirm whether perk-based pricing is causing conservative sell behavior.
  - Need a broader pass over trade logic because inventory feels full or near-full for too long.
- XP farming mode may be behaving oddly with looted trade goods.
  - Observed: party has a lot of raid-looted flax, but TradeOptimizer will not sell it.
  - Possible cause: looted flax is mixed with flax that was bought earlier, so provenance/paid-price logic may be blocking the whole stack.
  - Need inspect how the trade logic handles looted items, bought items, mixed stacks, and XP-farming sell criteria.
- Add inventory/capacity limiter for TradeOptimizer.
  - Desired behavior: reserve a percentage of total inventory capacity for loot and non-trade use.
  - Similar to Auto-Trader/Caravaneer style limits.
  - Proposed Core concept: `Reserve Capacity` setting, default around 10%.
  - TradeOptimizer should consume only the remaining capacity for trade goods after Core capacity reserve is applied.
- Add a crafting-material trade category for ingots.
  - Include all ingot types.
  - Default off.
  - This can probably be a simple toggle because ingots are player-made materials and do not behave like normal profit-traded market goods.
## Core / Shared Automation

- Consider adding a reserve-capacity concept to Core.
  - Default: 10% capacity reserved.
  - Purpose: leave room for loot, equipment, and player-directed inventory.
  - Should affect buy-side automation before TradeOptimizer sees free cargo capacity.
- Add quest-item/quest-animal protection for base-game quest deliveries.
  - Possible symptom: TradeOptimizer may sell cows given to the player by the "Deliver the Herd" quest.
  - The responsible provider may vary, so Core should likely reserve quest-related inventory before sell/trade/slaughter providers run.
  - Check whether the base game exposes quest-bound cattle/tools/materials directly, or whether Core needs a conservative quest-aware reservation rule.
- Confirm transaction effects in villages.
  - Core trade listener now applies gold deltas to the settlement component instead of the bound-town shortcut, matching village UI gold more closely.
  - Need live verification for village player gold, settlement gold, XP, and tax side effects.
- Core phase gating is in place for hostile/ineligible settlements.
  - Hostile villages may still run market phases, but notable recruitment and keep donations are blocked.
  - Raided/under-raid settlements skip market, recruitment, and donation phases.
  - Keep an eye on active/recent raid edge cases in live testing.
- Add an opt-in post-battle loot claim flow.
  - PartyManager can expose the setting, but Core should own the actual loot transfer so post-battle inventory changes use one shared path.
  - Desired behavior: automatically claim all battle loot for the main party, then let existing post-loot/post-battle processing run.
  - Need verify whether the correct hook is native loot collection/distribution or the loot inventory screen's take-all/accept path.
- Add recruitment spent gold notification reporting modes.
  - Currently: Recruitment uses silent gold deductions (`disableNotification: true` in `ExecuteNotableRecruitment`) to avoid spamming the message log with individual notifications when recruiting multiple units in a single automation cycle.
  - Idea: Offer a reporting setting to control how spent gold is shown:
    - **Silent**: No in-game notifications (current behavior).
    - **Individual**: Display native spent messages for each unit recruited.
    - **Consolidated**: Deduct and display total spent gold per recruitment batch once after the cycle is complete.


## Code Review Follow-ups

- EquipmentManager sale protection may treat stealth/civilian-compatible gear as a general upgrade.
  - `IsUpgradeForAnyTarget` checks battle, civilian, and stealth sets, but the battle branch does not apply armor-upgrade tier/budget policy.
  - This may be fine for "keep useful gear" behavior, but it is worth checking against the observed below-tier armor behavior so sale protection does not preserve or prioritize trash that only looks good in a different context.
- Core fief deposits still increment the construction reserve directly.
  - Hero gold is paid through `GiveGoldAction`, but `town.BoostBuildingProcess` is still incremented directly.
  - If Bannerlord has a native action/event for construction reserve deposits, using it may keep logs, notifications, achievements, or downstream systems more consistent.
- SmithingOptimizer is still structurally outside Core.
  - It is imported into the suite, but it still operates as a standalone UI/Harmony tool.
  - That is okay for 0.3.0/0.3.1, but future wood/material buying or smelting should go through Core requests/reservations instead of direct inventory manipulation.

## Future Mod Idea

- Quest completion helper.
  - Auto-submit cattle/tools when completing relevant quests.
  - Optionally hold cattle/tools in reserve for quests.
  - May conflict with TradeOptimizer/PartyManager, but Core reservations should win.
  - Could be a small provider that registers item reservations and/or submits quest completion actions later.

## Suggested First Pass

1. Reproduce and instrument village trade transactions.
2. Add Core reserve capacity and apply it to free cargo before trade analyzers.
3. Review TradeOptimizer balanced stance and no-perk pricing behavior.
4. Keep quest helper as a later design note unless it blocks trade/material reserves.
