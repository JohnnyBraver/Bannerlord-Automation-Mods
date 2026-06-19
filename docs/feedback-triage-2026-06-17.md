# Temporary Feedback Triage - 2026-06-17

Raw notes from live testing and feedback. This is intentionally temporary: keep the trail here, then split into focused fixes/issues once we start implementation.

## Equipment Manager

- Add hand-slot auto-buy.
- Add settings for shields.
- Add settings for bows and other non-craftable weapons.
- Add settings for craftable weapon categories.
- Fix missing max-upgrades-per-visit behavior.
- Add a max weapon upgrades setting.
- Fix weapon upgrade report text. Current text appears to say something like "weapon begin slot".
- Investigate armor purchases below the specified tier:
  - Observed armor was bought and equipped on the main loadout despite being below the configured tier.
  - It may have been a stealth-compatible hand-me-down from stealth upgrade purchasing.
  - Default stealth buying should only buy blackened items.

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
  - Village gold, player gold, and XP may diverge depending on how Core applies transfer commands.
  - Need compare town vs village behavior and native UI refresh/settlement roster behavior.
- Core should skip settlement automation steps when the settlement is not eligible.
  - Observed symptom: immediately after winning a hostile village raid battle, PartyManager auto-recruited a troop from that hostile settlement.
  - This should not happen.
  - Core should gate settlement phases before asking providers for orders when normal settlement services are unavailable.
  - Block or skip relevant phases for hostile settlements, active/recent raid targets, and settlements where the native UI would not allow normal recruitment/trade/service access.

## Campaign Behavior / Raid Edge Case

- Winning village raids while the player is knocked out appears to count as being captured by the village.
  - Need determine whether this is caused by a mod or native behavior.
  - Search for any automation/campaign behavior that reacts to village raids, map event ending, capture, or player defeated state.

## Code Review Follow-ups

- TradeOptimizer simulation reconstruction still has stale slaughter-yield bookkeeping.
  - `TradeOptimizerProvider` now tracks inventory diffs with side/modifier snapshot IDs, but the old meat/hides adjustment still checks plain `DefaultItems.Meat.StringId` and `DefaultItems.Hides.StringId`.
  - Slaughter arbitrage currently looks disabled, so this is probably dormant, but if that path comes back it may double-count generated meat/hides or fail to cancel them from simulated trade orders.
- TradeOptimizer's `SimulateAndCollectOrders` has a dangerous buy-side path.
  - The only current caller uses `runBuy: false`, but the method accepts `runBuy: true`.
  - If reused for buys, new player inventory keys are `PlayerInventory:item:modifier`, while merchant entries are mapped as `OtherInventory:item:modifier`; that can leave `eqElementMap[key]` missing when converting simulated purchases into `TradeOrder`s.
- TradeOptimizer's same-stop exclusion uses display names.
  - Sold items are added to `localExcludedItems` by `itemObj.Name.ToString()`, and buy candidates are checked the same way.
  - That can collide across variants with the same display name, depend on localization, and does not match the newer item/modifier identity model.
- Multiple free-trade analyzers can over-consume the same sellable quantity.
  - Core builds one `TradeContext` before iterating analyzers.
  - `ExecuteTradeProposal` clamps each proposal to the original `context.SellableItems`, but it does not reduce that availability after a provider sells something, so a later analyzer could sell the same starting quantity again.
  - This is low risk while TradeOptimizer is the only active analyzer, but it matters for the additive provider model.
- Exact market requests reserve sellable inventory by item id, not exact market identity.
  - `AutomationRequest.ForMarketItems` captures exact `InventoryItemView` candidates, but `AutomationRequest.MatchesItem` for `MarketItem` only checks `StringId`.
  - That can over-reserve or under-reserve when modifiers/variants exist, and it is a mismatch with the newer `SnapshotId` / `HasSameItemIdentity` approach.
- EquipmentManager sale protection may treat stealth/civilian-compatible gear as a general upgrade.
  - `IsUpgradeForAnyTarget` checks battle, civilian, and stealth sets, but the battle branch does not apply top-armor tier/budget policy.
  - This may be fine for "keep useful gear" behavior, but it is worth checking against the observed below-tier armor behavior so sale protection does not preserve or prioritize trash that only looks good in a different context.
- FiefManager deposits mutate hero gold directly.
  - `FiefManagerProvider` subtracts from `Hero.MainHero.Gold` and adds to `town.BoostBuildingProcess` directly.
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
2. Fix EquipmentManager weapon upgrade caps/report text.
3. Add Core reserve capacity and apply it to free cargo before trade analyzers.
4. Review TradeOptimizer balanced stance and no-perk pricing behavior.
5. Keep quest helper as a later design note unless it blocks trade/material reserves.
