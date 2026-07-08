# Changelog - Smithing Optimizer

## [v0.5.0] - 2026-07-08

### Added
- **XP Efficiency Goal**: Added a raw Smithing XP per stamina optimization goal and made it the default optimizer mode.
- **Selected Crafter Awareness**: Optimization now uses the active forge character's skill, perks, stamina context, and available smithing actions instead of always using the main hero.
- **Forge Auto-Optimization**: Added an enabled-by-default auto-optimize toggle that runs when opening the forge, switching designs, selecting a different crafter, and after crafting.
- **Smelting and Refining XP Recommendations**: The optimizer now compares crafting XP against available smelting and refining actions, including perk-adjusted refining cases such as charcoal, and recommends better training actions without consuming inventory.
- **Manual Design Fingerprint Guard**: Added design fingerprint tracking so craft-time auto-optimization preserves manual setup edits instead of immediately changing the design back.

### Changed
- **Adaptive Sell Value Optimization**: Sell value scoring now uses the selected crafter's expected crafted weapon modifier, so high-skill crafters can favor different designs than low-skill crafters.
- **Shared Smithing Scoring**: Crafting, XP, sell value, smelting, and refining estimates now share a smithing score estimator for consistent crafter, perk, stamina, difficulty, and value handling.
- **XP Ranking Semantics**: Smithing XP ranking now excludes learning rate and learning limit; it ranks raw XP output only.
- **Optimizer Goal Labels**: Renamed the old value-oriented goal to plain sell value so it no longer implies XP is included.

### Fixed
- **Manual Optimize Button**: Fixed the forge optimizer button path so clicking the button actually invokes optimization from the crafting screen.
- **Craft-Time Auto-Swap Safety**: Moved craft-time auto-optimization to a postfix and gated it behind the last optimizer-applied design fingerprint to avoid overwriting user-edited setups.
