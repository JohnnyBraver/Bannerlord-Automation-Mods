# Changelog - Settlement Automation Core

## [v0.5.4] - 2026-07-04

### Fixed
- **Settlement Gold Sell Cap**: Free-trade execution now caps sell quantities by the settlement's available trade gold before transferring items, preventing villages from accepting more goods than they can pay for.
- **Chained Trade Budget Tracking**: Core now carries settlement-side trade gold through the automation context, reducing it after sells and replenishing it after buys so later providers see the correct remaining merchant cash.
- **Trade Pricing Context**: Core now exposes whether settlement sell prices are static through `TradeContext`, allowing trade providers to follow settlement pricing rules without hardcoding settlement-type checks.

## [v0.5.3] - 2026-07-03

### Added
- **Ignore Weight Limit Bypass setting**: Added a new dropdown setting **"Ignore Weight Limit for"** (defaulting to **"All Requests"**) in MCM under Trading Policies. This allows vital/prioritized item requests (like emergency food) to bypass carry capacity limits and execute even when the party is overburdened.

### Changed
- **Prioritized Request Cargo Handling**: Item request fulfillment now applies the ignore-weight tier when deciding whether cargo capacity should block a purchase. This keeps Core needs such as food restoration separate from Trade Optimizer's profit-trading cargo rules.

## [v0.5.2] - 2026-06-28

### Fixed
- **Gold Discrepancy Tracking**: Replaced estimated item gold values with actual transaction debt differences reported by the game's native `InventoryLogic` after executing trades, ensuring accurate gold reporting in all core logs.
- **Background Trade Load Safety**: Introduced a `_justLoadedTicks` delay guard that prevents background trade tasks from executing immediately upon loading a save, resolving potential timing issues before game systems are initialized.
- **Price Limit Multiplier Customization**: Integrated settings from the MCM profile to dynamically apply custom essential, routine, and opportunistic price multipliers during the item request fulfillment phase.

### Added
- **Settlement Entry Speed Tracking**: Added speed tracking logic (`LastSettlementEntrySpeed`) that captures the player party's speed when entering a settlement, which is exposed to other sub-modules (like `PartyManager`) to calculate warning thresholds.
