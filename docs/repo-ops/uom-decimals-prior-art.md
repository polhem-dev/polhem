# External evidence on unit-of-measure decimals (prior art)

**This is not a pitfall log**, so it is not in `gotchas/`. It is **external evidence for a design decision**: a look at
how SAP and Odoo handle the decimals and rounding of units of measure, used to check which parts of polhem's current
design are deliberate and which are gaps.

The decision itself and the alternatives that were rejected are in
[ADR-026](../adr/adr-026-numeric-semantics-rounding.md). **This file does not copy them**; it holds only the external
comparison and its reading.
The current state on the Polhem side is defined by the source code; the type and member names mentioned in this file
are the entry points for checking it.

> **This is the state found on 2026-09-11, not a continuously maintained comparison.** Both products keep evolving
> (Odoo 19 removed the per-unit rounding setting entirely); confirm that it still holds before citing it next time.
> The limits of the verification are in the appendix at the end; read them too.

> **Status of the gaps found (checked 2026-09-27).** The comparison below is kept as it was written; the Polhem
> column describes the code of 2026-09-11. Since then:
>
> - **The company-level fallback for quantities and weights is gone** (ADR-026 revision of 2026-09-11, "quantity /
>   weight must be bound to a unit of measure"): a `Quantity` / `Weight` field must bind a `UnitField`, and
>   `FormExpressionCalculator` throws `InvalidOperationException` when it rounds a computed one that does not. The
>   places below that describe the fallback (the conclusion, question 3, the "Company" and "not bound" rows of the
>   levels table and the reading under it) are marked as resolved.
> - **Point 4 is fixed**: the XML doc of `UnitItem.Decimals` now names `DECAN` as display and `ANDEC` as rounding.
> - **Point 2 changed shape**: an unknown unit code now resolves to the framework default of the kind (see the
>   remarks of `NumberFormatResolver` and the ADR-026 revision table), still without an error.
> - Point 1 (a manually entered quantity is neither rounded nor rejected) is unchanged: `FormExpressionCalculator`
>   is still the only caller of `RoundByKind`.

## Conclusion in brief

- **"Decimals live at the system level, set per unit" is the same in all three**; the level itself does not need to
  change.
- **The unit stores decimals** (not a rounding unit): Polhem is the same as SAP here. Odoo 16–18 stores a rounding
  unit, and **from 19 on does not even set it per unit**, leaving only one system-wide number of decimals.
- There are two real differences: **a manually entered quantity is neither rounded nor rejected** (both products have
  a mechanism for this), and **a quantity field not bound to a unit falls back to the company-level decimals** (neither
  product has that level; *resolved 2026-09-11 by the ADR-026 revision: binding a unit is now required*).

## The five questions compared

| Question | SAP (ECC / S/4HANA) | Odoo | Polhem |
|---|---|---|---|
| **1. Where the decimals are stored** | `T006` (client level), two columns: `ANDEC` rounding decimals and `DECAN` display decimals, both integers. The DB scale of the `QUAN` field itself is unrelated to the unit's decimals | 16–18: each unit's `uom.uom.rounding` (a rounding unit, default 0.01) + the global `decimal.precision` "Product Unit of Measure". saas-18.1: `rounding` becomes computed from the global decimals. From saas-19.2: the field is removed | `UnitItem.Decimals` (an integer, in the system-level define `UnitSettings`). DB scale is orthogonal to it (ADR-026 D6) |
| **2. Are the rounding unit and the decimals separate?** | Both columns are decimals; there is no rounding unit. "Round to a multiple" lives at the material / plant level (`MARC-BSTRF`, rounding profile `RDPRF`) and is a business rule | Separate in 16–18: `rounding` governs conversion, the global decimals govern ORM writes, and the only link between them is an onchange warning on screen. Merged from 19 on | One number with two uses: display (`NumberFormatResolver.ResolveFormat`) and rounding (`RoundByKind`). No rounding unit |
| **3. Who decides, and down to which level it can be overridden** | Client level. No override mechanism at company code, material or document line level could be found in official sources | One per database; `decimal.precision` has no company field. Per unit in 16–18; not per product or per document line | System level. A field bound to a unit ignores the company; an unbound one falls back to the company decimals (see "Levels of unit settings: the three compared"). *Since the 2026-09-11 revision of ADR-026 binding is required and the company no longer decides* |
| **4. When and how it rounds** | Display follows `DECAN`. Conversion uses `ROUND_SIGN` of `UNIT_CONVERSION_SIMPLE` (`+` up, `-` down, `X` commercial, blank no rounding, default blank); which `T006` column it relies on is **unconfirmed**. Input has no framework-level rounding; the application layer rejects it (for example purchasing `ME 678`) | A Float field with `digits` is `float_round`ed when it enters the cache / is written to the DB, HALF-UP by default (away from zero). Conversion `_compute_quantity` defaults to `UP`, rounding to the target unit; `stock.move.product_qty` uses HALF-UP instead. In 17.0 a done quantity that does not fit the unit precision raises `UserError` | Only calculated fields are rounded (`FormExpressionCalculator.ApplyComputedRow` → `RoundByKind`, `AwayFromZero`). `NumericEdit` writes back full precision and only formats on display. No unit conversion |
| **5. Totals when detail lines have different units** | ALV binds the unit field with `QFIELDNAME`, and the total is "displayed separately by unit" (subtotals per unit). The delivery header has `LIKP-BTGEW` + `GEWEI`; the rule for converting to the header unit could not be found in ECC | Sales / purchase line quantity fields have no total; the `stock.move` list has `sum` and does not convert (inferred from the XML, not run); `sale.report` converts to the product's base unit in SQL before summing; shipping weight uses the system's single weight unit | The framework Grid has no footer total. `AmountColumnSummary.TryComputeTotal` returns `null` for mixed units (no conversion); currently only DemoCenter's `MultiUnitModule` wires it up by hand |

### Odoo version differences (the parts that affect the table above)

| Item | 16.0 / 17.0 / 18.0 | From saas-18.1 | From saas-19.2 (master the same) |
|---|---|---|---|
| `uom.category` | Exists | Removed, replaced by a tree structure of `relative_factor` + `relative_uom_id` | — |
| `uom.uom.rounding` | Stored, set per unit, `CHECK (rounding>0)` | Computed, value `10**-precision_get('Product Unit')`; per-unit override is gone | Field removed |
| Name of the global precision | "Product Unit of Measure" (product module) | "Product Unit" (moved to the uom module) | Same as left |
| What `_compute_quantity` rounds to | The target unit's `rounding` | Same as left (but already equal to the global decimals) | The global decimals directly |
| `float_round` rounding methods | 16.0 has only `UP` / `DOWN` / `HALF-UP`; 17.0 adds `HALF-EVEN` / `HALF-DOWN`; 18.0 raises `ValueError` for an unknown method | — | — |
| Unit field name on document lines | `product_uom` | — | `product_uom_id` in 19.0 |

The default `rounding_method='UP'` of `_compute_quantity` did not change in any version checked.

## What it means for polhem

### 1. A manually entered quantity is neither rounded nor rejected: a gap (undecided)

- ADR-026 D1 says `NumberKind` decides "(b) whether to round on write", and `Quantity` / `Weight` are `Round`;
  `.claude/rules/database.md` also says the rounded kinds are "rounded `AwayFromZero` to the column's scale on write".
- In the implementation, the only caller of `RoundByKind` is `FormExpressionCalculator`; `NumericEdit` writes back
  full precision, and saving through `FormBusinessObject` does not round either.
- Consequence: with unit PCS (0 places), entering 1.5 by hand shows 2 on screen and stores 1.5 in the database; a total
  based on it will not match the detail lines on screen, which is exactly what D2's round-then-sum is meant to
  prevent. **This is not limited to units; amounts and percentages are the same.**
- Both products have a mechanism: Odoo rounds to the global decimals on ORM write; SAP rejects input with too many
  decimals in the application layer.
- **Decided on 2026-09-11: record it, do not handle it yet.** Three directions are open: round the `Round` kinds before
  saving, reject input with too many decimals, or change the ADR's "on write" to "after calculation" to match the
  current implementation.

### 2. An unknown unit code falls back to 0 places without an error: a candidate gap

- At the time, `UnitSettings.GetDecimals` returned `FallbackDecimals` (0) when it found nothing, and the XML doc of
  `FormField.UnitField` said nothing about an unknown code, so a weight field bound to a mistyped `KGS` rounded 1.234
  to 1. *Changed since*: an unknown code resolves to the framework default of the kind (Quantity 0 / Weight 3), which
  the remarks of `NumberFormatResolver` describe; it is still accepted without an error.
- ADR-026's revision of 2026-09-10 made the company's local currency required, on the grounds that "what the fallback
  buys is silently wrong decimals"; this is the same shape.
- Odoo's unit field is a Many2one pointing to `uom.uom`, so an invalid code cannot get in. The value table of SAP's
  unit domain `MEINS` is `T006` (see the limits of the verification in the appendix).
- The currency side (falling back to 0.01 when nothing is found) has the same shape and is out of scope this time.

### 3. Units store decimals, currencies store a rounding unit: aligned with SAP and reasonable, but the reason is not written down

- The industry has no convention that "a unit must store a rounding unit": SAP stores decimals, and Odoo 19 gave up
  even the per-unit setting.
  A need such as "round to 0.5 boxes" is, in SAP, a material-level business rule (`BSTRF` / `RDPRF`), not the unit's
  decimals.
- ADR-026 and the commit that introduced it, [`eb10bc0c`](https://github.com/jeff377/bee-library/commit/eb10bc0c),
  only say "decimals are stored directly", not why this differs from currencies.
  It counts as deliberate but without a recorded reason; one sentence in ADR-026 would be enough to add it.

### 4. `ANDEC` / `DECAN` merged into one column: an acceptable simplification, but the XML doc is wrong (fixed)

SAP separates rounding decimals and display decimals; Polhem uses one `Decimals` for both; Odoo 19 merged them too.
The problem is that the XML doc of `UnitItem.Decimals` says "display decimal places (SAP T006 `ANDEC`)":
`ANDEC` is the **rounding** decimals and the display decimals are `DECAN`, and this value is in fact also used for
rounding. *Fixed since: the doc now names `DECAN` as display and `ANDEC` as rounding.*

### 5. No unit conversion: out of scope, but the ADR does not record it

SAP has the SI conversion columns of `T006` (`ZAEHL` / `NENNR` / `EXP10` / `ADDKO`) and the material-level `MARM`;
Odoo has `factor` (`relative_factor` from 19 on). Polhem's `UnitItem.Dimension` is only for UI grouping,
so a mixed-unit total can only be hidden; it cannot convert to a base unit first and then sum, the way Odoo's reports
do.
ADR-026's "not done" list names TCURF, KPEIN and DIFF, but not unit conversion.

### 6. Mixed-unit totals: the direction agrees, the cookbook overstates it

"Do not add across units" agrees with SAP (SAP shows subtotals per unit; Polhem shows nothing at all). But the
cookbook's "Units of measure" section reads as if the Grid had this gate built in, when in fact the framework Grid has
no footer and only a sample wires it up by hand.

### 7. Only a row-level unit, no form-level unit: deliberate, already recorded

Commit [`eb10bc0c`](https://github.com/jeff377/bee-library/commit/eb10bc0c) and the cookbook both state that it is
per-row. SAP's `UNIT` reference and Odoo's unit field are also on the row, so there is no conflict.

## Levels of unit settings: the three compared

| Level | SAP | Odoo | Polhem | Reading |
|---|---|---|---|---|
| **System / tenant** (SAP client, Odoo database, Polhem deployment) | `T006`: per-unit decimals, SI conversion, dimension | Global precision: the write decimals of every quantity field; from 19 on the only source of decimals | `UnitSettings`: per-unit decimals | All three put it at this level; **not a problem** |
| **Company** | None (no override mechanism could be found in official sources) | None (`decimal.precision` has no company field) | **Yes** at the time: the `Quantity` / `Weight` entries of `CompanyInfo.NumberFormats`, effective only when the field is not bound to a unit. *Removed by the 2026-09-11 revision of ADR-026* | **A level neither of the others has**; see below |
| **Unit** | `ANDEC` (rounding decimals) + `DECAN` (display decimals) | 16–18: `rounding` (a rounding unit); none from 19 on | `Decimals` (decimals, shared by display and rounding) | Same as SAP; finer than Odoo 19 |
| **Material / product** | Base unit + alternative unit conversion (`MARM`); plant-level multiple rounding (`MARC-BSTRF` / `RDPRF`), which is not decimals | The product chooses a base unit (`uom_id`); no decimals field | None. The framework has no product master; the unit value on a row is supplied by the application | None of the three puts decimals at this level; in the other two, conversion and multiples land at this level, and Polhem **has no seam** |
| **Document line** | The line carries a unit; a `QUAN` field must reference a unit field; too many decimals are rejected by the application layer (`ME 678`) | The line chooses a unit (Many2one); the decimals follow the system; in 17.0 a stock move that does not fit the precision raises an exception | The value of the row's `UnitField` decides the decimals; a manually entered value is neither rounded nor rejected | The binding has the same shape; the difference is **no enforcement** (point 1 above) |
| **Quantity field not bound to a unit** | Does not exist: the ABAP Dictionary requires every `QUAN` field to name the unit field it references | Uses the system decimals as usual (the write decimals never followed the unit anyway) | At the time: falls back to the company decimals → the framework default, baked into `NumberFormat` by `NumberFormatApplier.Bake` at delivery. *Now: binding is required; calculation throws when it is missing* | **Unique to Polhem** at the time; the same thing as the "Company" row |
| **Invalid unit code** | The value table of the unit domain `MEINS` is `T006`; whether each field enforces it was not checked field by field | Cannot get in (foreign key) | At the time: falls back to 0 places without an error; now the framework default of the kind, still without an error | Point 2 above |

**Reading: the levels themselves are not misplaced; the problem is the company-level fallback** (resolved: see the
status note at the top).

- **Decimals at the system level, set per unit**: Polhem is the same as SAP and finer than Odoo 19. There is no need to
  move them to the company or product level: neither product has unit decimals at the company level, and neither puts
  decimals on the product.
- **The company-level fallback is something neither product has.** For the same "quantity" semantics, the source of
  the decimals jumps between the system level and the company level depending on whether the schema author set a
  `UnitField`. The same unbound form delivered to two companies can show quantities with different decimals, while a
  bound field cannot. SAP does not allow a quantity field without a unit reference at the ABAP Dictionary level; in
  Odoo the write decimals never follow the unit, so the question "where does it fall back to when unbound" does not
  exist.
- The table in ADR-026 D1 does say "otherwise fall back to the company", but gives no reason. By the reasoning of the
  same ADR's 2026-09-10 revision, what this fallback buys is also "decimals that nobody chose for this field".
  **Possible directions (undecided)**:
  - require `Quantity` / `Weight` to be bound to a `UnitField`, and report an error when unbound (the SAP approach);
  - when unbound, use the system-level default instead, without going through the company (the Odoo approach);
  - keep the current behavior, but add the reason to ADR-026.

  **On 2026-09-11 the user leaned towards the first** (reason: binding a `UnitField` is consistent with the
  multi-currency approach), and
  [plan-unit-field-required.md](https://github.com/jeff377/bee-library/blob/7d6cc9d9/docs/plans/archive/plan-unit-field-required.md)
  was drafted separately to evaluate it. That direction was adopted the same day as the ADR-026 revision "quantity /
  weight must be bound to a unit of measure".
- **The gap at the material level is a question of scope, not a misplaced level**: the framework has no product
  master, so if conversion and multiple rounding are ever done, the seam will land in the application layer. ADR-026's
  "not done" list does not mention it yet (point 5 above).

---

## Appendix: limits of the verification

> **Do not treat this as settled.**
>
> **Fetched again and checked in person**: Odoo 17.0 / 19.0 / master `addons/uom/models/uom_uom.py`
> (the `rounding` field, the constraint, `_compute_quantity`), the HALF-UP definition in 17.0
> `odoo/tools/float_utils.py`, and the unit field type in 17.0 / 19.0 `sale_order_line.py`; SAP's official ABAP
> Dictionary page on quantity fields and the ALV field catalog page; the descriptions of `ANDEC` / `DECAN` and the
> value table of `MEINS` on sapdatasheet.
> The other Odoo line numbers and SAP sources were read by a research agent and not fetched again one by one.
>
> **SAP sources vary in quality**: the definition of `ANDEC` could only be found on a data dictionary mirror site
> (sapdatasheet, third party); help.sap.com has no direct definition; community.sap.com always returned 403, so only
> search snippets were visible.
>
> **A value table is not a hard check**: the value table of `MEINS` is `T006`, but in SAP a value table is only the
> source of a proposed foreign key; whether each field actually checks it depends on that field's foreign key
> definition, and this was not checked field by field.
>
> **Still unchecked**: whether `UNIT_CONVERSION_SIMPLE` relies on `ANDEC` or `DECAN`; the name of the decimals field
> in S/4HANA `I_UnitOfMeasure`; the ECC header weight conversion rule; whether S/4HANA changed the semantics of
> `ANDEC` / `DECAN`; whether the Odoo front end rounds on input; the direct cross-unit sum in the `stock.move` list was
> inferred from the XML and not actually run.

## Appendix: sources

**SAP**

- ABAP Dictionary quantity fields (official): https://help.sap.com/doc/abapdocu_753_index_htm/7.53/en-US/abenddic_quantity_field.htm
- `WRITE … UNIT` (official): https://help.sap.com/doc/abapdocu_751_index_htm/7.51/en-us/abapwrite_to_options.htm
- ALV quantity / currency fields (official): https://help.sap.com/saphelp_nw73/helpdata/en/4e/bd13c61041389ee10000000a421937/content.htm
- Unit conversion function group SCV0 (official): https://help.sap.com/doc/saphelp_nw73ehp1/7.31.19/en-US/48/dfb0aaab14280fe10000000a42189c/content.htm
- `T006` (third party): https://www.sapdatasheet.org/abap/tabl/t006.html
- `ANDEC` / `DECAN` (third party): https://www.sapdatasheet.org/abap/dtel/andec.html, https://www.sapdatasheet.org/abap/dtel/decan.html
- `MEINS` domain (third party): https://www.sapdatasheet.org/abap/doma/meins.html
- `UNIT_CONVERSION_SIMPLE` parameters (third party): https://www.sapdatasheet.org/abap/func/unit_conversion_simple.html
- `BSTRF` / `RDPRF` (third party): https://www.sapdatasheet.org/abap/dtel/bstrf.html, https://www.sapdatasheet.org/abap/tabl/marc-rdprf.html
- Message `ME 678` (third party): https://www.sapdatasheet.org/abap/msag/me-678.html

**Odoo** (master line numbers as fetched on 2026-09-11)

- 17.0 `uom_uom.py` (`rounding` L66, constraint L81, `_compute_quantity` L211–239): https://github.com/odoo/odoo/blob/17.0/addons/uom/models/uom_uom.py#L66
- 19.0 `uom_uom.py` (`_compute_rounding` L62–67): https://github.com/odoo/odoo/blob/19.0/addons/uom/models/uom_uom.py#L62-L67
- master `uom_uom.py` (`_compute_quantity` L139–167): https://github.com/odoo/odoo/blob/master/addons/uom/models/uom_uom.py#L139-L167
- 17.0 `product_data.xml` (global precision L35–38): https://github.com/odoo/odoo/blob/17.0/addons/product/data/product_data.xml#L35-L38
- 17.0 `product/models/uom_uom.py` (onchange warning L10–21): https://github.com/odoo/odoo/blob/17.0/addons/product/models/uom_uom.py#L10-L21
- 17.0 `decimal_precision.py` (no company field, L21–34): https://github.com/odoo/odoo/blob/17.0/odoo/addons/base/models/decimal_precision.py#L21-L34
- 17.0 `fields.py` (Float rounding on write, L1531–1555): https://github.com/odoo/odoo/blob/17.0/odoo/fields.py#L1531-L1555
- 17.0 `float_utils.py` (`float_round` L35–111): https://github.com/odoo/odoo/blob/17.0/odoo/tools/float_utils.py#L35-L111
- 17.0 `stock_move.py` (`product_qty` L279–282, `_set_quantity` L388–403): https://github.com/odoo/odoo/blob/17.0/addons/stock/models/stock_move.py#L279-L282
- 17.0 `stock_move_views.xml` (`sum` L28, L47–48): https://github.com/odoo/odoo/blob/17.0/addons/stock/views/stock_move_views.xml#L28
- 17.0 `sale_report.py` (sum after conversion, L94–98, L176–177): https://github.com/odoo/odoo/blob/17.0/addons/sale/report/sale_report.py#L94-L98
- 17.0 `stock_delivery/models/stock_move.py` (weight L33–38): https://github.com/odoo/odoo/blob/17.0/addons/stock_delivery/models/stock_move.py#L33-L38
- 17.0 / 19.0 `sale_order_line.py` (unit field, 17.0 L120, 19.0 L132): https://github.com/odoo/odoo/blob/17.0/addons/sale/models/sale_order_line.py#L120
- 17.0 Purchase UoM documentation (explains rounding and Decimal Accuracy): https://www.odoo.com/documentation/17.0/applications/inventory_and_mrp/purchase/products/uom.html
