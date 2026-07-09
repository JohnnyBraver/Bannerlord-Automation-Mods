# Changelog - Equipment Manager

## [v0.5.0] - 2026-07-09

### Added
- **Weapon Preference Profiles**: Added MCM dropdowns for one-handed swords, one-handed axes and maces, two-handed weapons, thrust polearms, swing polearms, ranged weapons, and shields.
- **Weapon Property Matching**: Added `Preserve All`, `Ignore Minor`, and `Stats Only` matching modes. The default is `Ignore Minor`, so strong stat upgrades are no longer rejected just because they lose small tactical extras such as hook, knockdown, or wide-grip flags.
- **Ranged and Shield Priorities**: Added ranged priorities for balanced, damage, and accuracy choices, plus shield priorities for balanced, max HP, and max size choices.

### Changed
- **Weighted Weapon Upgrade Scoring**: Weapon upgrades now use role-aware scoring instead of requiring every stat to improve. This lets higher damage, reach, or tier weapons beat faster sidegrades when the selected preference says they should.
- **Shared Upgrade Decisions**: Auto-equip, cascade fitting, auto-buy, sale protection, and drawback reporting now use the same weapon evaluator so the mod makes consistent decisions across town entry, post-purchase equipping, and inventory cleanup.
- **Ammo and Throwing Behavior Preserved**: Existing ammo and throwing weapon preference dropdowns are unchanged and continue to use their count/damage rules.

### Fixed
- **Stat Tradeoff Upgrades**: Fixed cases where weapons like longer, harder-hitting swords could be rejected forever because an equipped weapon had slightly better speed or handling.
- **Mounted Ranged Compatibility**: Mounted battle loadouts now reject long bows for heroes without the mounted bow override and reject horseback reload-blocked ranged weapons unless the mounted crossbow perk applies.
- **Drawback Messages**: Rejected higher-scoring weapon candidates now report a clearer reason, such as property mismatch or mounted incompatibility.
- **Weapon Slot Labels**: Drawback and cascade messages now use readable labels like `Weapon 1` instead of internal names such as `WeaponItemBeginSlot`.
