# Changelog - Party Manager

## [v0.5.1] - 2026-06-28

### Fixed
- **Dynamic Speed Warnings**: Refactored cargo overburden and herding warnings to calculate actual percentage speed penalties relative to the party's actual pre-penalty speed, rather than displaying raw flat percentages.
- **Save Load Warn Suppression**: Added checks to skip herding and overburden warnings when loading a save directly inside a town, where the settlement entry speed is unknown.
- **MCM Settings Identification**: Updated MCM registration to target `PartyManager_v0_5` for version alignment.
