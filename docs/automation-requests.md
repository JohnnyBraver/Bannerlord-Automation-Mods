# Automation Requests

Settlement Automation Core owns market execution. Domain modules describe what they want, and Core decides whether the current settlement, budget, cargo space, and herding limits allow the purchase.

## Automation Flow

Settlement automation intentionally keeps "decide what we want" separate from "touch the game inventory."

1. Core builds an `AutomationRequestContext` with categorized merchant and player inventory snapshots.
2. Request providers submit item requests.
3. Recruitment, garrison, ransom, dungeon, and fief phases run on their own provider interfaces.
4. Core executes item requests against fresh `InventoryLogic`.
5. Free trade runs afterward with request reservations applied.

Domain modules should inspect context and submit requests. They should not execute transfers directly.

## Request Ordering

Requests are ordered by `RequestProfile`, then by `Priority` from 9 to 1.

- `Critical`: must-have survival purchases. No price cap.
- `Essential`: important party needs. No price cap.
- `Routine`: normal maintenance. Uses the Core routine price limit.
- `Opportunistic`: cheap-only buys. Uses the Core opportunistic price limit.
- `Luxury`: wanted upgrades. No markup cap, but still constrained by reserve and budget.

Priority is only a tie-breaker inside the same profile. The default priority is 5.

Changing a spend mode changes when the request runs and whether Core applies a price cap. It does not bypass gold reserve, cargo capacity, herding limits, missing inventory, or exact market-item validation.

## Spend Mode Settings

Mods expose spend-mode settings only for the item requests they submit. Priorities remain code-owned tie-breakers.

| Module | Request family | Setting | Default |
| --- | --- | --- | --- |
| PartyManager | Critical food | `Critical Food Spend Mode` | `Critical` |
| PartyManager | Food variety | `Food Variety Spend Mode` | `Essential` |
| PartyManager | Total food buffer | `Food Buffer Spend Mode` | `Routine` |
| PartyManager | Riding mounts | `Riding Mount Spend Mode` | `Routine` |
| EquipmentManager | Top armor | `Top Armor Spend Mode` | `Luxury` |
| EquipmentManager | Stealth gear | `Stealth Gear Spend Mode` | `Luxury` |

## Quantity Modes

- `DesiredInventoryCount`: buy until player inventory contains the requested count.
- `PurchaseCount`: buy up to this many entries from an ordered merchant candidate list.

Recruitment is intentionally outside this item request pipeline. PartyManager submits recruit orders through the recruitment provider, and Core executes them before item purchases.

## Inventory Visibility

`AutomationRequestContext` exposes categorized merchant and player inventory views. Current categories are food, mounts, pack animals, livestock, trade goods, armor, and weapons.

Use inventory visibility when the provider needs to pick exact visible items. EquipmentManager uses merchant armor visibility to submit ordered exact armor candidates. PartyManager food variety does not need visibility because it can request known food item ids from the loaded item catalog and let Core skip unavailable foods.

## PartyManager Food

PartyManager owns party needs. TradeOptimizer owns profit-making trade behavior.

Food requests are layered:

- Critical-food spend mode `Food` category request to `Critical Food Days`.
- Food-variety spend mode `SpecificItem` requests for every loaded food item in the game catalog.
- Food-buffer spend mode `Food` category request to `Party Food Days to Keep`.

Specific food requests do not require the food to be visible in the current merchant inventory. If olives are not in the shop, Core skips the olive request and later fills the routine food buffer with whatever food is available.

The default food shape is:

1. Critical category food for emergency days.
2. Specific food items for variety.
3. Routine category food for the full buffer.

This lets variety run before the broad buffer request without letting variety outrank emergency food.

## Market Candidates

Market candidate requests are for exact visible merchant inventory entries. Providers pass ordered `InventoryItemView` candidates from `AutomationRequestContext.MerchantInventory`; Core reopens fresh inventory logic, re-prices the item, matches by item id plus modifier id, and buys only if current budget rules still allow it.

Market candidate requests are advisory. If the item disappeared, changed modifier, became unaffordable, or would violate reserve/cargo/herding rules, Core skips it safely.

Market candidate providers should pass candidates from `context.MerchantInventory`. Core rejects non-merchant candidates during execution.

## Settings Ownership

- Core settings control global budget reserve, cargo enforcement, and price limits for Routine and Opportunistic requests.
- PartyManager settings control food, mounts, recruitment filters, recruitment size targets, and PartyManager request spend modes.
- EquipmentManager settings control armor upgrade selection, explicit armor reserves, and EquipmentManager request spend modes.
- TradeOptimizer settings control profit-focused buy/sell behavior.

TradeOptimizer may trade food or mounts as commodities when its category trading policies allow it. PartyManager owns party restocking needs for food and riding mounts.
