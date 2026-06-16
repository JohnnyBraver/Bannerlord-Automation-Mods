# Automation Requests

Settlement Automation Core owns market execution. Domain modules describe what they want, and Core decides whether the current settlement, budget, cargo space, and herding limits allow the purchase.

## Automation Flow

Settlement automation intentionally keeps "decide what we want" separate from "touch the game inventory."

1. Preparation providers run before market decisions. This lets modules finish non-trading setup, such as EquipmentManager equipping the best available gear before anything decides what can be sold.
2. Core builds an `AutomationRequestContext` with categorized merchant and player inventory snapshots.
3. Request providers submit item requests.
4. Pre-sell providers submit sell orders after preparation and request gathering.
5. Recruitment, garrison, ransom, dungeon, and fief phases run on their own provider interfaces.
6. Core executes item requests against fresh `InventoryLogic`.
7. Free trade runs afterward with request reservations applied.
8. Core asks registered report providers for per-provider, per-phase in-game report lines, then writes those lines itself.

Domain modules should inspect context and submit requests. They should not execute transfers directly.

Modules that need to change party state before trade planning should implement `IAutomationPreparationProvider`. Preparation runs before the visibility snapshot, so later request providers and pre-sell providers see the prepared inventory state.

EquipmentManager uses preparation for headless auto-equip. It tracks a virtual candidate pool during the single equip transaction, so gear freed by a combat upgrade can be handed down to civilian or stealth outfits before Core asks any provider what should be sold.

EquipmentManager does not change item lock icons for its own keep rules. It builds a sale-protection plan and only submits sell orders for quantities that are not manually locked and not protected by the current settings.

After battles, EquipmentManager does not equip immediately when the battle event ends. It waits for `ItemsLooted` to report equipment added to the main party, then runs headless auto-equip on the next campaign tick.

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

EquipmentManager owns equipment upgrade purchases, including stealth/blackened gear. TradeOptimizer does not buy equipment as a trade commodity.

## Equipment Keep Rules

EquipmentManager keep rules protect items from automatic sale only. They do not toggle the game's lock icon.

- `Keep Positive Modifiers` is optional and off by default.
- Tier/quality alone does not protect equipment from sale.
- `Keep Donation Items` protects cheap perk-donation gear from sale without locking it.
- `Additional Armor Sets to Keep` can reserve the best spare armor pieces per enabled outfit type.
- Spare combat, civilian, and sneaking armor reserves are separate toggles and are off by default.

## Market Reporting

Core owns writing in-game automation messages. Domain modules can own the message content by implementing `IAutomationReportProvider`.

Reporting is per provider and per transaction phase. Core builds an `AutomationReportContext` for each provider/phase pair that produced completed market activity and calls:

```csharp
IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context)
```

The provider may return no lines, one line, or multiple lines. Core writes the returned lines in order, which avoids competing message writes between modules. Providers that do not implement `IAutomationReportProvider` still get a generic fallback line.

Reports are naturally ordered by the execution phase that produced them. Within a phase, provider activity follows that phase's registered provider order.

Core's in-game market reporting mode controls the core-authored summary at the end of the automation cycle:

- `Off`: write no in-game automation report lines. The file log is still written.
- `Silent Mods`: write module-owned report lines, then add core fallback summaries only for provider/phase activity that had no registered reporter or returned no lines.
- `Full`: write module-owned report lines, then add core summaries for every provider/phase that completed activity. This is the default.

Current report phases are:

- `PreSell`: completed pre-sell orders from `IPreSellProvider`.
- `PriorityRequest`: completed item-request purchases from `IAutomationRequestProvider`.
- `FreeTrade`: completed buy, sell, or slaughter actions from `IFreeTradeAnalyzer`.

The report context includes `ProviderName`, `Stage`, `BoughtItems`, `SoldItems`, `SlaughteredItems`, and `CargoStatus`. Item lists contain completed activity only: item name, inventory category, quantity, and gold total. Core still writes the detailed aggregate file log summary after in-game reporting.

Report providers can optionally implement `IAutomationReportStyleProvider` to supply a `ReportHeaderColor` as an RGBA `uint`. Core applies that color when it writes the provider's in-game report lines. Providers without a style hook get a deterministic color derived from the provider name, and fallback summaries use the same provider-name color.

The core summary and generic fallback lines are written after module-owned report lines. They group visible items by category, so equipment, food, mounts, livestock, and trade goods remain readable instead of disappearing into a single "more" count.

Detailed request gathering, skip reasons, price-cap decisions, cargo failures, reserve failures, and per-request purchase lines remain in the mod log files. The HUD message is intentionally compact so normal town visits do not become noisy.

Report providers are called after execution, not during planning. They should describe completed actions, not attempt to execute transfers or re-check market conditions.

## Completion and Rejections

Mods currently get completed market activity through the reporting hook, grouped by provider and phase. They do not get a structured callback for every rejected order or skipped request.

Rejected or skipped item-request details are opt-in Core file-log diagnostics. `Log Rejected Order Details` is off by default because completed transactions are usually the important signal during normal play.

When enabled, Core writes one detailed file-log entry for item requests it could not fulfill, including a structured reason code and detail text. Current reason codes cover:

- `InvalidQuantity`: the request asked for zero or fewer items.
- `GoldReserveReached`: projected gold was already at or below the applicable reserve.
- `UnsupportedQuantityMode`: Core does not execute that quantity mode.
- `NoMarketCandidates`: a market purchase request did not include candidates.
- `CandidateNotFromMerchantInventory`: a market candidate did not come from the merchant side.
- `MerchantStockMissing`: a submitted market candidate no longer exists in merchant stock.
- `NoMatchingMerchantStock`: an inventory-category or specific-item request found no matching merchant stock.
- `CandidateItemMissing`: the candidate roster element no longer has an item.
- `PricePolicyExceeded`: the fresh price violates the request profile's price policy.
- `GoldReserveBreach`: buying the candidate would breach the applicable gold reserve.
- `CargoCapacityExceeded`: buying the candidate would exceed carry capacity while cargo protection is enabled.
- `HerdingLimitExceeded`: buying the candidate would exceed available animal/herding slots.
- `NoAffordableMatchingStock`: matching stock existed, but no candidate survived affordability and policy checks.

These reason codes are currently groundwork for Core-owned diagnostics. They are not yet a mod callback contract.

Rejected or skipped work is most accurate in Core because Core is the component that can know the final reason. For example, a request can be skipped after the provider submitted it because the item disappeared from the merchant, the modifier no longer matches, the fresh price violates the request profile, the purchase would breach gold reserve, cargo capacity is full, or herding slots are exhausted.

A future structured result hook should probably stay Core-owned too: Core would record accepted, partially fulfilled, and rejected provider submissions with reason codes, then offer those result records to report providers. Mods can format those records, but should not infer rejection reasons from their original planning snapshot.

## Settings Ownership

- Core settings control global budget reserve, cargo enforcement, price limits for Routine and Opportunistic requests, and in-game market report detail.
- PartyManager settings control food, mounts, recruitment filters, recruitment size targets, and PartyManager request spend modes.
- EquipmentManager settings control armor upgrade selection, explicit armor reserves, and EquipmentManager request spend modes.
- TradeOptimizer settings control profit-focused buy/sell behavior.

TradeOptimizer does not expose gold reserve or carry-capacity settings. Core supplies those limits through `TradeContext` before any optimizer proposal is executed.

TradeOptimizer may trade food or mounts as commodities when its category trading policies allow it. PartyManager owns party restocking needs for food and riding mounts.
