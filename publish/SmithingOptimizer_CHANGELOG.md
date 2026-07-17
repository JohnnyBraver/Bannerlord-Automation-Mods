# Changelog - Smithing Optimizer

## [v0.6.1] - 2026-07-17

### Fixed

- **Working Forge Button**: The OPT button correctly invokes the shared optimizer from the forge screen.
- **Cross-Weapon Alternatives**: Weapon-type comparisons use each type's own unlocked parts and report a valid alternative without changing the selected weapon type.

### Changed

- **Clearer Results**: In-game optimization messages show the selected result only. The file log keeps the full calculation, including ratios, raw values, stamina, materials, and weapon-type comparisons.
- **Training Recommendations**: Smelting and refining recommendations now compare the best available action against the current craft using the selected Smithing XP basis.

## [v0.6.0] - 2026-07-17

### Added

- **Independent Optimization Controls**: Choose Sell Value, Damage, or Smithing XP, then choose raw result, stamina efficiency, or material efficiency.
- **Damage Quality Gate**: Damage optimization can require a chosen chance of crafting a selected quality or better.
- **Crafting-Order Optimization**: Selecting an order can automatically choose affordable unlocked parts that give the selected smith the required completion chance.
- **Cross-Weapon Alternatives**: Sell Value and Smithing XP report a better unlocked weapon type while keeping the current weapon type selected.

### Changed

- **Faster Part Search**: Replaced exhaustive Blade x Guard x Grip x Pommel enumeration with bounded complete-design coordinate ascent and slider refinement.
- **Cached Sell Value Searches**: Reuses results for weapon types whose unlocked parts, materials, Crafting level, and Crafting perks are unchanged.
- **Automatic Timing**: General automatic optimization now runs when a new part is unlocked or the selected smith changes, instead of on forge opening, design changes, and crafting completion.
- **Consistent Decisions**: Manual activation and automatic part switching now run one shared capture, search, comparison, application, logging, and error-handling pipeline.
- **Skill-Aware Evaluation**: The optimizer resolves the active forge hero through `CraftingCampaignBehavior`, and uses the game model for difficulty, free-build XP, and stamina.
- **Quality-Aware Sell Value**: Sell Value is calculated across the active smith's possible quality outcomes instead of from one random crafted modifier.
- **Hardwood Supply Automation**: Keeps the existing hardwood purchase request, removes charcoal requests, uses a fixed internal priority, and prefilters requests when the party is already at the gold reserve.
- **Automation Settings**: Hardwood can be set to any whole amount from 0 to 500; charcoal and player-facing supply priority settings were removed.
- **Settings Profile**: Uses the new `SmithingOptimizer_v0_6_0` MCM profile. Configure it once after updating; older profiles are kept separately by MCM.
- **Training Recommendations**: Smelting and refining suggestions use the selected Smithing XP basis and stop once they find a better available action.

### Fixed

- **Random Candidate Scoring**: v0.5.0 treated one random modifier roll as expected sell value, producing unstable search results. Evaluation is now deterministic.
- **Large-Unlock Forge Hangs**: The Cartesian candidate explosion that stalled the optimizer after many unlocks has been removed.
- **Trigger Drift and Reentrancy**: A repeated click or unlock event cannot run a second optimizer against the same mutable design.
- **Quality-Model Compatibility**: Reflected quality scoring is pinned to the supported CampaignSystem assembly and disables safely with a clear message if it changes.
