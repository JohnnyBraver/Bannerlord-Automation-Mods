# Changelog - Equipment Manager

## [v0.5.2] - 2026-07-16

### Fixed
- **MCM Group Order**: Fixed the top-level settings order and ensured Auto-Sell and Auto-Buy parent pages are discovered before their nested pages.
- **Auto-Buy Navigation**: Removed the redundant nested General page. Shared purchase controls now appear directly on the Auto-Buy page before Armor and Weapons.

## [v0.5.1] - 2026-07-16

### Added
- **Weapon and Armor Auto-Sell Choices**: Choose Disabled, Armor Only, Weapons Only, or Weapons & Armor. Selecting Armor Only preserves all weapons for later loadout changes.
- **Category-Based Protection**: Positive modifiers and donation candidates can now be protected for armor, weapons, or both independently.
- **Loadout-Specific Purchase Controls**: Battle armor, civilian armor, stealth gear, and weapons now have separate enablement, reserves, limits, and relevant purchase rules.

### Changed
- **Reworked MCM Layout**: Settings are organized around Auto-Equip, Auto-Sell, and a nested Auto-Buy hierarchy for armor and weapons. Weapon evaluation now sits beside allowed weapon types.
- **Combat-Focused Defaults**: Battle armor and hand-slot weapon upgrades are enabled by default; civilian armor and stealth gear remain opt-in with their own budgets.
- **Simpler Auto-Equip**: Removed timing controls from MCM. Auto-equip now runs at its normal automation lifecycle points whenever Equipment Manager is enabled.
- **Centralized Purchase Policies**: Auto-buy settings are applied through shared track policies so reserves, limits, and ordering are handled consistently.

### Fixed
- **Unwanted Weapon Sales**: Armor-only auto-sell excludes weapons before sale eligibility is evaluated in both automatic sale paths.
- **Stealth Spare Armor Reserves**: Spare armor reserves now apply only to battle and/or civilian loadouts, never stealth gear.

### Settings Profile
- Equipment Manager now uses the `EquipmentManager_v0_5_1` MCM profile. Configure the refreshed settings once after updating; the v0.5.0 profile is retained separately by MCM and is not reused.

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
