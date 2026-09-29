# ADR-026: Numeric semantics, company/currency/unit decimals, and round-then-sum

## Status

Accepted (2026-07-01)

## Context

ERP numbers (unit price, cost, quantity, weight, amount, percentage, exchange rate) each have their own decimal
places, display format and rounding rule, and the decimals come from different sources: non-monetary kinds can be
customized per company, amounts follow the currency (JPY=0 / USD=2 / BHD=3), quantities/weights follow the unit of
measure (KG=3 / PCS=0), and the exchange rate is a system-fixed intermediate conversion factor. Before the
implementation, these needs were scattered across carriers that did not work together:

| Concept | Current carrier | Gap |
|------|---------|------|
| Storage precision | `DbField.Precision/Scale` | Not linked to display/calculation/currency |
| Display format | `FormField.NumberFormat` → `LayoutColumnFactory` → `GridControl.FormatCell` | Filled in by hand per field, not aware of company/currency/unit |
| Semantic presets | `NumberFormatPresets` | Isolated, 0 production callers |
| Calculation rounding | None | No single entry point |

Without unified numeric semantics, three ERP-grade correctness problems had nowhere to be addressed: **total ≠ sum of
details** (floating precision, or summing at full precision and rounding afterwards), **wrongly rounding source
values** (rounding unit prices/exchange rates to display decimals, injecting error downstream), and **inconsistent
storage precision across providers** (extra decimals are left to the DB engine; SQLite does not enforce scale and the
other providers round, so the same data is stored with different precision).

The design draws on SAP ECC/S4 (CURR/CUKY, QUAN/UNIT, TCURX, T006, T001R, per-line rounding) and Odoo
(`res_currency` decimal_places, `float_round`, `round_per_line`), simplified to fit this framework's FormSchema-driven
architecture. It spans the definition layer (`Polhem.Definition`), the business logic layer (`Polhem.Business`), the
data access layer (`Polhem.Repository`) and the UI layer (`Polhem.UI.Avalonia`), and is a structural contract of the
framework's external API surface, hence this ADR. This ADR collects the "why" and the rejected alternatives, for the
cookbook to cite.

## Options considered

The following lists, one by one, the alternatives at the key decision points that "looked reasonable but were
rejected" (the adopted design is in the next section).

1. **Sum at full precision and round once** (`total = round(Σ full-precision details)`): intuitive, one less per-line
   rounding. **Rejected**: ERP documents require "the header total equals the sum of the detail column, digit for
   digit", and recomputing at full precision makes `total ≠ Σ displayed details`, so the books do not balance. This is
   exactly why SAP SD pricing and Odoo `round_per_line` both round per line.

2. **Round unit prices/costs/exchange rates to the "suggested display decimals" before storing**: keeps the data clean
   and the decimals consistent. **Rejected**: these three kinds are calculation sources, and rounding them to display
   decimals injects error into every downstream calculation (amount = quantity × unit price). Display decimals are
   purely for presentation, not a storage boundary; source values must be kept as-is at input precision.

3. **Bake the amount display format at delivery time** (the same path as percentages/exchange rates, writing
   `NumberFormat` in at delivery): consistent, zero resolution on the client. **Rejected**: the decimals of an amount
   depend on the **document currency**, and the currency is **document data that changes** (the user changes the
   currency in the UI, detail rows each have a different currency). The company session is fixed at delivery time, but
   the currency is not; hard-coding `N2` goes wrong when the currency is switched. Amount/unit decimals must be
   resolved at runtime.

4. **Adjust the DB column scale per company / per currency** (so the storage precision matches the business
   decimals): saves storage space, precise schema semantics. **Rejected**: every added company or currency would
   require an `ALTER TABLE`, which is not operationally feasible. DB scale should be a single high-capacity ceiling,
   orthogonal to display/calculation.

5. **Use `Dictionary<NumberKind,int>` for the company override table**: semantically intuitive. **Rejected**:
   `XmlSerializer` cannot serialize a `Dictionary` cleanly, and it violates the definition-layer convention that a
   collection property inherits a framework collection base (`KeyCollectionBase<T>` or `CollectionBase<T>`) rather
   than being a bare BCL collection. A key-value collection is used instead.

6. **Keep the static `NumberFormatPresets` table**: no change to existing code. **Rejected**: it is an isolated table
   of format strings, not semantics-driven, with 0 production callers, and it cannot carry the rounding strategy or
   the source of the decimals. It is refactored into the `NumberKind` enum + `NumberKindProfile`.

## Decision

Adopt an overall design "centered on the `NumberKind` semantic attribute, with four sources of decimals, two layers of
rounding, and storage precision kept orthogonal". Six core decisions:

- **D1: the `NumberKind` semantic attribute drives three things**: `FormField` (passed on to `LayoutFieldBase`)
  carries `NumberKind`, which decides (a) the kind of display format (`N`/`P`), (b) whether to round on write
  (`Round` vs `Preserve`), and (c) the source of the decimals. The members and framework defaults are a signed-off
  contract:

  | `NumberKind` | Rounding strategy | Source of decimals | Framework default |
  |-------------|---------|---------|:-------:|
  | `Quantity` / `Weight` | `Round` | Unit of measure (bound to `UnitField`; falls back to the company if absent) | 0 / 3 |
  | `Amount` | `Round` | Currency (bound to `CurrencyField`; falls back to the master / company if absent) | 2 |
  | `Percent` | `Round` | Company × Kind | 2 |
  | `UnitPrice` / `Cost` | `Preserve` | Company (display only) | 4 |
  | `ExchangeRate` | `Preserve` | Fixed by the system | 5 |

- **D2: round-then-sum (the ERP iron rule)**: the total of a `Round` kind = **the sum of the rounded detail values**,
  never recomputed at full precision. Each detail line is first rounded to its decimals with `RoundByKind` and then
  summed, so `Σ details == total` holds by construction. Transaction currency and home currency each do round-then-sum
  independently by their own currency key field.

- **D3: two layers of rounding are kept apart**: the detail layer rounds to the **currency's natural decimals / the
  unit's decimals** (system level: `CurrencySettings` / `UnitSettings`); the final document layer then optionally
  applies a **cash rounding unit** (overridable by the company, SAP T001R style, such as CHF→0.05), which acts only on
  the final amount payable and deliberately produces a rounding difference (booked to a DIFF account). Currency
  decimals are always system level, the cash rounding unit can be company level, and the two are not mixed up.

- **D4: `Preserve` never writes back a rounded value**: `UnitPrice` / `Cost` / `ExchangeRate` are kept as-is at input
  precision, and the decimals are for display only (display rounding does not write back to the bound value).
  `RoundByKind` returns the original value for these kinds. The only hard boundary is the DB scale capacity ceiling
  (see D6).

- **D5: non-monetary kinds are baked at delivery, currency/unit are resolved at runtime**:
  - Company decimals (`Percent`, unit price/cost) and system-fixed ones (exchange rate) are baked on the **per-call
    clone** in `SystemBusinessObject.LoadAndLocalizeSchema` (`NumberFormatApplier.Bake` writes
    `FormField.NumberFormat`); a `NumberFormat` filled in by the author always takes precedence; the cached schema is
    never mutated.
  - `Amount` (following the currency) and `Quantity`/`Weight` bound to a `UnitField` (following the unit) are **not
    baked**: at delivery only the referenced field name is marked, and the decimals are resolved at runtime from that
    field's current value (the UI recomputes when the currency/unit changes; the BO rounds by the document's
    currency/unit). This follows SAP's per-field CUKY/UNIT: an amount field binds `FormField.CurrencyField` (if not
    specified, falls back to the master's `sys_currency` → the company's `DefaultCurrency` → the framework's 2), and a
    quantity/weight field binds `FormField.UnitField`.

- **D6: DB scale is a capacity ceiling, orthogonal to display/calculation**: numeric columns use `Decimal` + a single
  high framework-wide scale (such as 8), with no per-company/per-currency `ALTER`. Display decimals (`NumberFormat`)
  and calculation decimals (`RoundByKind`) have nothing to do with the DB scale. When an API import exceeds the scale,
  the Repository write layer **explicitly** applies `decimal.Round(value, DbField.Scale, AwayFromZero)` (implicit DB
  conversion cannot be relied on; it is inconsistent across providers); this is physical truncation to the storage
  capacity, not business rounding, and the scale is far beyond business significance, so it does not conflict with
  D4.

## Consequences

- **Correctness**: the total always equals the sum of the details (D2); zero error propagation from source values
  (D4); consistent storage precision across providers (D6).
- **Multi-tenant / multi-currency**: the same schema delivered to two companies can carry different formats
  (`Percent` P2 vs P4); within the same document, transaction and home currency, and different rows of the same
  column, can have different currencies/units, each with its decimals resolved at runtime.
- **Compatibility (existing data needs no migration)**: `FormField`/`LayoutFieldBase` gain `NumberKind` and
  `FormSchema` gains `CurrencyField`, all with an empty `[DefaultValue]` → existing XML deserializes unchanged;
  `st_company` gains four columns (`number_formats_xml`/`default_currency`/`cash_rounding_xml`/
  `allowed_currencies_xml`) and `CompanyInfo` gains `[Key(4)]`~`[Key(7)]`; when the columns are empty in old data,
  everything falls back to the framework defaults; appending keys at the end is MessagePack compatible.
- **New definition types**: `DefineType.CurrencySettings` (TCURX style, a curated system-level ISO 4217 table) and
  `DefineType.UnitSettings` (T006 style) use the existing dual mode of `IDefineStorage` (file/`st_define`) + triple
  serialization + are shipped to the client with `GetDefine`; without a definition each falls back on its own.
- **Follow-up rules (when adding a numeric field)**: a field declaring semantics always sets `NumberKind`; an amount
  field binds `CurrencyField` as needed (the transaction currency can omit it and use the master's `sys_currency`), a
  quantity/weight field binds `UnitField`; BO calculations always use `decimal` and go through `RoundByKind`
  round-then-sum; summing at full precision and rounding afterwards is forbidden, and so is rounding a `Preserve`
  kind.
- **Not done (future items)**: exchange rate factors (TCURF), price unit (KPEIN), absorbing the DIFF rounding
  difference in the header, porting `NumericEdit` to the then `Bee.UI.Maui` project (since removed) and to Blazor.

## Implementation evolution

### 2026-07-22: `CompanyInfo` has no integer keys

The "Compatibility" item says `CompanyInfo` gained `[Key(4)]`~`[Key(7)]` and that appending keys at the end is
MessagePack compatible. [ADR-030](adr-030-messagepack-name-based-keys.md) switched the wire to property-name keys,
and since [ADR-036](adr-036-wire-serialization-externalized.md) `CompanyInfo` carries no MessagePack attributes: it
travels as a name-keyed map written by `src/Polhem.Api.Core/MessagePack/CompanyInfoFormatter.cs`, where a new
member has to be added (`WireContractDriftTests` reports one that is missing).

### 2026-09-10: the company's home currency becomes mandatory

The original decision allowed the company's home currency (`CompanyInfo.DefaultCurrency`) to be blank: when an amount
had no referenced currency, the fallback chain of D5 went all the way down to the framework default of 2 decimals,
and the "Compatibility" item also said "when the columns are empty in old data, everything falls back to the
framework defaults". **Now a company must have a home currency, and a blank one is a configuration error.**

**Only one path changes**: when an amount field has no referenced currency (no `CurrencyField` bound, no
`sys_currency` on the master, or that cell is still empty) **and there is a company context**, `NumberFormatResolver`
now throws `InvalidOperationException` instead of falling back to the framework default.
**Without a company context** (in the UI before entering a company, `RoundingContext.ForCompany(null)`) it still falls
back to the framework default of 2 decimals; that is not a configuration error.
The check does not depend on whether a currency master is deployed.

Reasons:

- **The fallback buys silently wrong decimals.** Falling back to 2 decimals when the company has not chosen a home
  currency looks perfectly normal, but those decimals were not decided by any of the company's currencies, and
  home-currency amounts (`home_amount`) are rounded with the wrong decimals as well.
- **The failure happens at calculation, not at loading or entering the company.** The framework itself does not write
  `st_company` (the company master is maintained externally), so there is no place to check on write; blocking it
  when entering the company would also make calls that only read the company name, and never touch amounts, fail.

As a consequence:

- The framework does not pick a default currency on the company's behalf. `st_company.default_currency` still has no
  database default value; whoever creates the company data writes it.
- In an existing deployment, a company whose home currency is blank will, after upgrading, throw when saving documents
  with amount calculations and during live UI calculation; the value must be filled in first.

### 2026-09-11: quantity / weight must be bound to a unit of measure

The D1 table of the original decision said "Unit of measure (bound to `UnitField`; falls back to the company if
absent)", and D5 also baked the company decimals at delivery into quantity/weight fields with no unit bound.
**Now: a field marked `Quantity` / `Weight` must bind a `UnitField`, and the company no longer decides the decimals
of quantities and weights.**
A number that needs no unit (a count such as number of pieces or boxes, where the unit is implied by the meaning)
uses a plain number without a `NumberKind`.

Reasons:

- **The two fallback chains used to fall back to different things.** Every step of the amount fallback chain yields a
  currency code; the company only decides "which currency", and the decimals are decided by the system-level currency
  master. The second step of the quantity fallback chain, however, had the company provide decimals directly.
- **Units have no counterpart to the "home currency".** A company has a home currency as its default currency, but it
  has no default unit: the same order can sell PCS and KG at the same time. The only part of the amount fallback
  chain that can be matched on the unit side is "binding is mandatory".
- SAP's ABAP Dictionary likewise requires a `QUAN` field to name its reference unit field; numbers without a unit use
  `DEC`.

Runtime behavior is aligned with multi-currency, and differs only where units have no counterpart:

| Situation | Quantity / weight | Compared with amounts |
|------|-----------|---------|
| No `UnitField` bound | `FormExpressionCalculator` throws `InvalidOperationException` when rounding a computed field; display and bake do not throw | When the company has no home currency it likewise throws at calculation and not at display |
| Bound, but the row's unit code is empty | `RoundByKind` returns the original value, no rounding | An amount falls back to the header currency, then the company's home currency; a unit has nothing to fall back to, and without decimals no information is thrown away |
| The unit code is not in the unit master | Falls back to the framework default (Quantity 0 / Weight 3) | A currency falls back to 0.01 |
| No unit master deployed | Falls back to the framework default, bypassing the company | A currency first consults the company's decimals table |

As a consequence:

- `NumberFormatApplier.Bake` never bakes any `Quantity` / `Weight` field, consistent with amounts.
- The `Quantity` / `Weight` entries in the company decimals table (`CompanyInfo.NumberFormats`) no longer take effect.
- The company overload `RoundByKind(value, kind, company)` has no unit code and returns the original value for
  quantities/weights; detail rounding must use the overload that takes a unit code.
- The original D5 statement "baked on the per-call clone in `SystemBusinessObject.LoadAndLocalizeSchema`" no longer
  holds: the definition API supplies the schema as-is, and baking is done by the consumer (the .NET heads, in
  `FormDefinitionLoader` of `Polhem.Api.Client`).

## References

- Cookbook: `docs/en/development-cookbook.md` §Numeric Semantics, Company Decimals, and Rounding (how-to and API
  entry points)
- Related ADRs: [ADR-005](adr-005-formschema-driven.md) (FormSchema-driven),
  [ADR-012](adr-012-session-company-context.md) (session company context),
  [ADR-017](adr-017-db-cache-invalidation.md) (cache invalidation)
- SAP: ABAP CURR/QUAN must bind CUKY/UNIT, ALV `CFIELDNAME`, currency decimals TCURX, unit decimals T006
  (ANDEC/DECAN), per-line rounding / cash rounding T001R
- Odoo: `res_currency` (decimal_places/rounding), `float_round`, tax `round_per_line` (default) vs `round_globally`
