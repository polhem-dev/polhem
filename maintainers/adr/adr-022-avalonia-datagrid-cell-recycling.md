# ADR-022: Avalonia DataGrid list cells do not enable template recycling

## Status

Accepted (2026-06-14)

## Context

[ADR-020](adr-020-avalonia-datagrid-binding-strategy.md) established the cell strategy of `GridControl` (which at the
time inherited `Avalonia.Controls.DataGrid` directly, and has since been refactored into a `ContentControl` composite
whose inner `DataGrid` is exposed as `InnerGrid`): use `DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>`
and fetch values explicitly in code inside the template (`row.Row[fieldName]`) instead of through Avalonia binding,
because the Avalonia binding engine does not dispatch to the `ICustomTypeDescriptor` string indexer of `DataRowView`.

The example in ADR-020 at the time (and the implementation) used `supportsRecycling: true` for **plain-text cells in
read-only lists**:

```csharp
templateColumn.CellTemplate = new FuncDataTemplate<DataRowView>(
    (row, _) => new TextBlock { Text = FormatCell(row, fieldName, ...) },
    supportsRecycling: true);   // <- conflicts with "Text is computed once, not bound"
```

The key conflict: this `Text` is a **fixed string** computed from the `row` of that moment **when the template is
built**; it is **not** a value bound to the cell's DataContext.

For performance, the Avalonia `DataGrid` keeps a pool of presenters, and when scrolling / re-realizing it **reuses the
same cell visual for different rows**, replacing only the `DataContext`. With `supportsRecycling: true`, reuse
**does not rerun** the build delegate of the `FuncDataTemplate`; it expects the content to be bindings that follow
the DataContext by themselves.

The result of the two colliding: when a presenter is recycled to another row, the underlying `DataRowView` changes,
but the precomputed `Text` stays at the text of the **old row**. So **the text shown on screen comes apart from the
actual underlying row**.

The bug is most visible in the lookup picker: the user sees a cell showing "SALES" and clicks it, the framework takes
**the actual underlying** `DataRowView` of that visual row (`SelectedItem.Row` was always correct), and what comes
back is the data of **another row**; that is, "the data shown in the lookup window does not match the data actually
returned".

> Why it stayed latent: with little data, in order, and each cell built once at the first realization (before pool
> reuse kicks in), the display is correct; it only goes out of line once recycling happens (opening the window
> several times, scrolling, rebinding). It was forced out by careful testing of the Employee→Department lookup in
> stage 3 of the Polhem.Northwind demo.

## Decision

**The plain-text list cell template of `GridControl` switches to `supportsRecycling: false`.**

```csharp
templateColumn.CellTemplate = new FuncDataTemplate<DataRowView>(
    (row, _) => new TextBlock { Text = FormatCell(row, fieldName, ...) },
    supportsRecycling: false);   // each row builds its own cell, so the text always matches that row
```

This is also a **return to consistency**: every other cell template in `GridControl` (the lookup display cell, the
interactive cells (ComboBox/DatePicker), `CellEditingTemplate`) **was already `false`**; only this read-only
plain-text list cell was `true`, the one that slipped through.

## Consequences

### Positive

- **The display always matches the underlying row**: each row gets its own newly built cell, whose `Text` is computed
  on the spot from that row; presenters are not reused across rows → no more "see A, get B back"
- **Fixed in the framework, everyone benefits**: every place that uses the `GridControl` list display (the
  `ListView` list, the lookup picker, the detail grid of master-detail) is fixed at once
- **Consistent with the other template strategies in the control**: every cell template is "fresh per row"

### Negative

- **Gives up the visual reuse that recycling brings**: each row allocates its own `TextBlock`. For this framework's
  lists / pickers (small data volumes, mostly a single page) the cost is negligible; if large lists become a
  bottleneck later, the right fix is to move to "real binding cells" (see below), not to turn recycling back on

### Neutral

- **The other right fix: use binding instead of a precomputed string**, which would make `supportsRecycling: true`
  safe too. But every other cell in this control uses the "fresh per row (false)" strategy, and ADR-020 already
  explains why Avalonia binding is not used (the `DataRowView` string indexer is not supported by the binding
  engine); switching to `false` is the most consistent with the existing strategy, the smallest change, and the
  lowest risk
- **Selection / write-back correctness is unaffected**: selection has always been an object reference
  (`SelectedItem.Row`), and write-back (`ApplyLookupSelection`) has always accessed columns by field name, so both
  were already correct; this ADR only fixes the **display layer** so that the text the user sees matches the correct
  row

## Related links

- [ADR-020: How the Avalonia DataGrid binds to DataTable rows](adr-020-avalonia-datagrid-binding-strategy.md): this
  ADR corrects the `supportsRecycling: true` in its example
- [ADR-021: Avalonia DataGrid in-cell / EditForm editing strategy](adr-021-avalonia-datagrid-editing-strategy.md)
- `src/Polhem.UI.Avalonia/Controls/GridControl.Columns.cs`: the plain-text list cell template in `BuildColumn`

## Out of scope

- **Virtualization / recycling performance for large lists**: if needed later, move to real binding cells (which
  first requires solving binding support for the `DataRowView` indexer, an Avalonia upstream issue); not part of this
  ADR
