# Settings Design Guidelines

Use these principles when adding or reviewing mod settings. A good settings menu lets players express intent without needing to understand the implementation.

## Design principles

### 1. Design around player decisions

Each setting should answer a question a player would naturally ask: what to automate, who it affects, when it applies, or how cautious it should be. Name settings after their outcome, not an internal mechanism.

Do not expose implementation values that a player cannot reasonably evaluate, such as internal priority numbers, execution paths, or coordination details between systems.

Before adding a setting, ask whether an existing setting already expresses the same choice. Prefer improving that setting over adding another overlapping control.

### 2. Make the structure match the task

Organize settings by the task the player is completing, not by the code that implements it. Put common actions first and diagnostics or niche preferences last.

Use nested groups only when they reduce scanning:

- Keep small features flat.
- For a large feature with distinct player-facing categories, make those categories the first level beneath the feature.
- Put settings shared by all categories on the feature's parent page, before its category subsections.
- Keep category-specific settings inside that category.
- Do not create empty or cosmetic layers of hierarchy.

The intended result is that a player can configure the normal case quickly, while detail remains available where it is relevant.

### 3. State scope and overrides plainly

Hints should explain a setting's scope whenever it could be misunderstood. Say whether it affects automatic actions, manual actions, a specific category, one loadout, or all applicable cases.

State meaningful exceptions and overrides close to the setting they affect. Avoid development-facing wording; describe what happens in game terms.

### 4. Choose controls that cannot create confusing states

Use a dropdown when the player is selecting one policy from a small set of mutually exclusive choices. Include a clear disabled option when the feature can be turned off independently.

Use separate toggles only when every combination is meaningful. If a combination is contradictory, surprising, or difficult to explain, represent it as one policy instead.

### 5. Make defaults useful and safe together

Defaults should support the mod's main use case, not merely leave every feature inactive. Enable low-surprise core behavior by default; make costly, disruptive, or niche behavior opt-in.

Review related defaults as a set. A parent feature should not be enabled while all of its essential child behavior is disabled, and a child should not appear active when its parent prevents it from running.

### 6. Bound numeric settings to meaningful choices

Use the narrowest useful range, a sensible increment, and a default with a clear game meaning. Cap values when larger values add no player value or create excessive work.

Explain whether zero disables the behavior and whether a number applies globally, per category, or per enabled loadout.

### 7. Preserve explicit player intent

Manual locks, explicit exclusions, and protection rules should override automation unless a setting explicitly says otherwise. Make dependencies on perks, locations, item state, or similar conditions visible in the hint.

### 8. Treat configuration changes as compatibility changes

Changing a setting's type, meaning, default, or valid range needs a saved-data plan. Map existing values to equivalent new behavior where practical, retain an explicit settings version, and test migrations before release.

### 9. Give settings one application path

Settings design includes the code that applies it. Convert related raw settings into a named policy, plan, or helper at the boundary of a runtime flow, then have that flow consume the result.

- Keep the rule that interprets a setting in one authoritative place.
- Reuse shared selectors for the same concept across multiple flows.
- Keep item- or situation-specific game rules separate from setting interpretation.
- Avoid scattering the same enablement, reserve, category, or priority checks through scanning, selection, and submission code.
- When several settings describe parallel categories, represent them as a collection of policies rather than a growing chain of special cases.

This keeps menu wording, runtime behavior, and tests aligned. It also makes a new option easier to add without missing one of the paths that must honor it.

## Review checklist

Before shipping a settings change, verify that:

- The grouping and order reflect player tasks.
- Labels, hints, defaults, options, ranges, and increments match the intended behavior.
- No controls overlap, conflict, or expose implementation-only details.
- Every visible option reaches the correct runtime behavior, including disabled states.
- Manual protections and documented overrides still work.
- Existing saved configurations migrate intentionally.
- Each changed setting has one clear application path, shared where multiple flows use the same rule.
- Tests cover the changed behavior and, where practical, the player-facing configuration contract.
