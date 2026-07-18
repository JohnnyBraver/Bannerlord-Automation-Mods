# Changelog - Trade Optimizer

## [v0.6.1] - 2026-07-04

### Fixed
- **Village Cash-Aware Sell Planning**: Sell planning now respects the settlement's remaining trade gold, so Trade Optimizer does not propose profitable sells that a village cannot afford.
- **Village Sell Price Stability**: Sell planning now uses Core's static-price context flag to keep the first per-stack sell price for villages because village prices do not decay during the transaction. Town sell planning still rechecks demand-sensitive prices unit by unit.
- **Merchant-Gold Diagnostics**: Planner diagnostics now distinguish `MerchantGoldDepleted` and include a compact plan block summary in the runtime log when remaining candidates are blocked.
- **Merchant-Gold Simulation State**: Buy and sell simulation now keeps settlement-side gold in sync with planned transfers, including the experimental swap path.

## [v0.6.0] - 2026-07-03

### Fixed
- **Re-Entry Sell Behavior & Drift**: Resolved a reference pricing bug where passed local transaction prices took precedence over native average purchase prices. Reference prices are now calculated correctly by prioritizing tracked average purchase prices from `TradeSkillCampaignBehavior`, preventing unprofitable selling on settlement re-entry.
- **Planner Completion on Town Entry**: Reworked the free-trade path around an internal planner that simulates balance, cargo, merchant stock, owned counts, same-stop exclusions, and demand-sensitive prices before returning executable actions. One settlement entry now exhausts the direct trades it can justify at that moment instead of leaving a decaying continuation for immediate re-entry.
- **Direct-Trade Adapter Accuracy**: Replaced final-inventory diff reconstruction with planner-recorded actions that preserve the exact `EquipmentElement` identity. This avoids display-name matching errors and keeps Core execution aligned with the items the planner actually selected.
- **Concise Report Food Visibility**: The concise in-game Trade Optimizer report now selects top traded items across all categories, so profitable food trades such as Date Fruit are no longer hidden while Core reports them as Food.
- **Execution vs Planning Log Clarity**: Planner gold is now labeled as estimated, and the actual post-Core execution report is written back into `TradeOptimizer_Log.txt` for easier reconciliation with Core's runtime log.
- **Simulation Mode Safety**: Dry-run mode now keeps planner diagnostics and reports without returning executable Core actions.
- **Loot and Policy Application**: Loot handling and XP-farm activation now respect the configured category trading policies, and loot liquidate/XP-farm modes explicitly force loot sale only where intended.
- **Eager Swap Break**: Corrected the buy loop so that it evaluates all merchant items to find the absolute best item by profit density, instead of breaking on the first item that exceeds cargo and greedily swapping.
- **XP Farm Activation Route**: Replaced the simple pre-sell loot dumping with an advanced activation route. Looted item stacks are now locked and kept in player inventory during the sell phase. In the buy phase, the mod automatically calculates and buys the minimum necessary activation units from the merchant to raise the stack's average price $\ge 1$ denar, unlocking the cost basis so they can be sold for massive Trade XP in future markets.
- **InventorySide Namespace Qualification**: Fixed a namespaces compilation issue by fully qualifying `InventorySide` to `InventoryLogic.InventorySide`.
- **Simulation Cargo Balance Alignment**: Resolved a discrepancy where the simulation clamped starting cargo capacity to `0` when overburdened (via `FreeCargoHeadroom`), causing it to greedily plan buys using space freed by sells, whereas the Core execution logic used the raw negative capacity (preventing buys until the entire overburdened capacity was cleared). Now uses `CargoCapacityBalance` in both places to match Core execution perfectly.

### Added
- **MCM Margin Swapping Toggle**: Introduced a new checkbox setting **"Enable Margin Swapping"** (Order 10) in MCM. This allows players to enable or disable the cargo swap-selling behavior depending on their preferences.
- **Buy Cap Policy Controls**: Added a **Buy Cap Policy** dropdown (`None`, `Count`, `Value`, `Both`) plus per-cap mode dropdowns (`Per Visit`, `Inventory`). The default is **None**, so Trade Optimizer buys every profitable item allowed by price, cargo, budget, merchant stock, and policy settings.
- **Typed Planner Diagnostics**: Added typed block reasons for stack count cap, stack value cap, cargo/overburden, budget, price threshold, herding, same-stop exclusion, unknown average/reference price, no profit, and category policy. Completion logs now summarize why remaining candidates were blocked.
- **Mod Version Load Log**: Dynamically retrieves assembly metadata to report the mod version on module load and at the start of every optimization run.
- **Detailed XP Farm Activation Logs**: Added logging output indicating full stack unlocks or partial activations (listing resulting average price and the remaining units needed to unlock).

### Optimized
- **O(N + M) Swapping Complexity**: Optimized the worst owned item search to run once per buy loop iteration rather than nested inside the merchant item loops, reducing complexity from `O(N * M)` to `O(N + M)` to prevent city-entry lag.
- **Dual-Worst Swap Fallback**: Pre-calculates the top two worst owned items per iteration, automatically falling back to the second worst if the absolute worst matches the buy candidate's type, maintaining 100% swap correctness.
- **World Average Price Cache**: Added a transient, thread-safe cache for world average prices in `PricingService` (cleared at the beginning of each optimization run) to prevent thousands of redundant loops over ~53 towns during item reference price initialization, eliminating city-entry frame drops.
- **O(1) Herding Penalty Protection Check**: Replaced the nested loop that summed simulated animal purchases on every candidate evaluation with a running counter (`totalAnimalsBoughtInSim`), reducing the herding check complexity from $O(N)$ to $O(1)$ per item.

### Changed
- **Margin Swapping Defaulted Off**: Margin swapping remains available as an explicit experimental opt-in, but the default path is now direct sell/buy only. When swapping is disabled, insufficient cargo resolves to overburdened instead of triggering cargo-for-profit swaps or selling merely to make room.
- **Main Loop Structure**: Split the old monolithic trading routine into focused planning phases for snapshot capture, sells, XP-farm activation, direct buys, and experimental margin swaps.
- **Buy Caps Are Buy-Only**: Count/value limits only block future purchases. They never force Trade Optimizer to sell down existing inventory.
- **Top Report Naming**: The concise report mode is now labeled **Top Items** because it can include food, livestock, mounts, and trade goods when those categories are enabled.
- **Log Condensation & Clarity**: Replaced spammy, repetitive unit-by-unit logs with aggregated sell summaries, swap pairings, and a structured optimization run results table detailing starting/ending prices, margins, gold, and reference valuations.
