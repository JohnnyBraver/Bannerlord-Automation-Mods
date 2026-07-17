# Changelog Guide

This repository's changelogs are release records for both players and maintainers. Start with the player-visible difference, then include concise implementation detail when it explains a bug fix, performance change, compatibility boundary, or migration concern.

## Scope

- Compare the new release with the previous public release.
- Include new player capabilities, changed player workflows, removed behavior, and fixed player-visible problems.
- Include internal implementation detail when it is the reason a release is safer, faster, deterministic, or compatibility-limited.
- Omit routine refactors, test coverage, commits, and temporary investigation details.
- Mention a default only when that default itself changes what a player will experience after updating.
- Mention a new settings profile as a `Changed` entry whenever players must configure settings again.

## Format

Use this structure when a section has relevant entries:

```markdown
## [vX.Y.Z] - YYYY-MM-DD

### Added

- **Feature Name**: What the player can do now.

### Changed

- **Workflow Name**: What now behaves differently for the player.

### Fixed

- **Problem Name**: The player-visible problem that no longer occurs.

```

## Writing Rules

- Use one short sentence per bullet.
- Start every bullet with a bold, player-facing label: `**Feature Name**:`.
- Prefer concrete verbs such as choose, buy, optimize, show, or prevent.
- State the outcome before the mechanism; add the mechanism only when it makes the release record more useful.
- Do not repeat the release version in prose when the heading already states it.
- Do not claim a mod sells, crafts, equips, or spends items unless it actually performs that action.

## Review Checklist

- Would a player understand what changes in play without knowing the code?
- If an internal detail is included, does it explain a meaningful safety, performance, compatibility, or migration difference?
- Is each item a functional or UX difference from the previous release?
- Are defaults, technical details, and internal names omitted unless they affect migration or compatibility?
- Are settings migration instructions present in `Changed` when the MCM profile changes?
