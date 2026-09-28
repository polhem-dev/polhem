# ADR-020: How the Avalonia DataGrid binds to DataTable rows

[繁體中文](adr-020-avalonia-datagrid-binding-strategy.zh-TW.md)

## Status

Accepted (2026-06-09)

## Context

[ADR-001](adr-001-dataset-as-dto.md) established `DataSet` / `DataTable` as the framework's cross-layer DTO: server
side BOs return a `DataTable`, and the client renders it directly without projecting it onto typed POCOs.
The `DynamicGrid` of the then `Bee.UI.Maui` project (since removed) got this data flow working by "hand-building
every cell with Grid + Label + TapGestureRecognizer".

When `Polhem.UI.Avalonia` was added in Phase 3, the natural choice was to use Avalonia's built-in
`Avalonia.Controls.DataGrid`, which should provide more complete basics such as selection, scrolling and column
sizing than the hand-built Grid on the MAUI side. The idiomatic WPF equivalent is:

```csharp
new DataGridTextColumn
{
    Header = column.Caption,
    Binding = new Binding($"[{column.FieldName}]") { Mode = BindingMode.OneWay },
}
```

With `DataGrid.ItemsSource = DataTable.DefaultView`, each row is a `DataRowView`, and the binding path `[FieldName]`
gets the value through the string-key indexer of `DataRowView`. The WPF binding engine dispatches to
`ICustomTypeDescriptor`, obtains each column's `PropertyDescriptor` from the `DataRowView` and then reads the value,
so the cells display fine.

Measured on Avalonia 12, this approach gives:

- **The DataGrid iterates the rows correctly (the row count is right)**
- **Every cell is an empty string**

Investigation showed that the Avalonia 12 binding engine (which resolves paths through `ExpressionObserver`)
recognizes only these two kinds of data source:

1. CLR properties (the PropertyInfo is obtained directly through reflection)
2. Typed indexers: the integer-key indexer of `IList<T>` / `IReadOnlyList<T>`, or an indexer declared as
   `this[T key]` where `T` is a type that can be inferred at binding time

`DataRowView.this[string columnName]` is a third kind outside both: it is a PropertyDescriptor-based string-key
indexer, which has to go through `ICustomTypeDescriptor` to get the column descriptor before reading the value.
**The Avalonia binding engine does not do this dispatch**, so resolving the path `[FieldName]` fails, the cell
receives a `BindingNotification`, and in the end an empty string is rendered.

WPF / MAUI get the same literal binding syntax to work because the WPF binding engine has built-in awareness of
`ICustomTypeDescriptor`. This is a difference in how the two frameworks implement their binding engines, not
something that "some wrong setting in Avalonia" can fix.

## Decision

**`Polhem.UI.Avalonia.Controls.DynamicGrid` does not use `DataGridTextColumn` + `Binding "[FieldName]"`. It uses
`DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>` instead, and the cell template explicitly calls
`row.Row[fieldName]` in code to get the value.**

```csharp
private static DataGridTemplateColumn BuildColumn(LayoutColumn column)
{
    var fieldName = column.FieldName;
    var displayFormat = column.DisplayFormat;
    var numberFormat = column.NumberFormat;

    return new DataGridTemplateColumn
    {
        Header = column.Caption,
        CellTemplate = new FuncDataTemplate<DataRowView>(
            (row, _) => new TextBlock
            {
                Text = FormatCell(row, fieldName, displayFormat, numberFormat),
                Margin = new Thickness(8, 4),
            },
            supportsRecycling: true),
    };
}
```

`row.Row` gets the underlying `DataRow`, and then goes through `DataRow.this[string]` (plain ADO.NET, reachable by
reflection). The Avalonia binding engine never touches it, because `FuncDataTemplate` reduces its role to "given an
item, return a control".

Along with this, the field formatting logic (`DisplayFormat` / `NumberFormat` / `DateTime` ISO 8601 / `IFormattable`
invariant culture) is encapsulated in one static method, `FormatCell`, which behaves symmetrically with
`DynamicGrid.FormatCell` of the then `Bee.UI.Maui` project (since removed).

## Consequences

### Positive

- **No projection onto typed POCOs**: the DataTable remains the only end-to-end representation of row data,
  consistent with [ADR-001](adr-001-dataset-as-dto.md)
- **`Polhem.UI.Avalonia.DynamicGrid` and the `DynamicGrid` of the then `Bee.UI.Maui` (since removed) behave
  alike**: both use code-based formatting,
  and differ only in the host control (Avalonia `DataGrid` vs MAUI `Grid` + `Label`)
- **The fact that the Avalonia binding engine "does not dispatch to ICustomTypeDescriptor" only has to be handled in
  this one adapter**: the rest of the framework can still use Avalonia binding normally (binding to CLR properties,
  ViewModels, `IList` and so on)
- **Later readers are kept from repeating the mistake**: this ADR plus the `<remarks>` comment in `DynamicGrid.cs`
  state explicitly "do not change this back to `Binding "[FieldName]"`"

### Negative

- **Cell-level binding loses the `OneWayToSource` / `TwoWay` modes**: the `TextBlock` inside the `FuncDataTemplate`
  does not write back to the `DataRowView` automatically. This is not a problem for this `DynamicGrid`: it is
  `IsReadOnly = true` anyway, and cell-level editing is event-driven in the `DynamicForm` of the master area
  (`TextChanged` / `IsCheckedChanged` / `SelectionChanged`); this idiom is the same across the Avalonia / MAUI /
  Blazor families
- **Each cell template formats itself**: the `DisplayFormat` / `NumberFormat` handling is concentrated in the static
  `FormatCell` method (an inline 5-line switch), which is manageable; but it no longer benefits from framework-level
  reuse through Avalonia column-level `IValueConverter`s
- **`DataGrid.AutoGenerateColumns` stays `false`**: columns were already generated by hand to match
  `LayoutGrid.Columns`, and this ADR does not change that; but it means that if Avalonia later offers smarter
  automatic schema inference, we are still opted out

### Neutral

- **Avalonia `Binding "[X]"` still works for other data shapes**: `IList<T>` (integer key),
  `IReadOnlyDictionary<string,T>` (string key but typed), and objects that declare their own `this[T] { get; }` all
  still go through the binding engine. **Do not generalize this ADR into "Avalonia indexer binding never works"**;
  this ADR is limited to the single case of "the PropertyDescriptor-based string indexer of `DataRowView`"
- **If Avalonia upstream adds `ICustomTypeDescriptor` support in the future**, going back to `DataGridTextColumn` +
  `Binding "[FieldName]"` can be reconsidered, but there is no plan to go back until there is a need

## Related links

- [ADR-001: DataSet as the cross-layer DTO](adr-001-dataset-as-dto.md): why the DataTable is the unit the client
  renders directly
- [ADR-013: Frontend API connection strategy](adr-013-frontend-api-connection-strategy.md): `Polhem.UI.Avalonia` is a
  member of the `Polhem.UI.*` family
- `src/Polhem.UI.Avalonia/Controls/GridControl.cs` (later renamed from `DynamicGrid`, and split by responsibility into
  files such as `GridControl.Columns` / `.Cells` / `.Rows` / `.Binding`): the implementation plus a detailed
  `<remarks>` comment
- `docs/en/development-cookbook.md` section "Avalonia desktop (Polhem.UI.Avalonia)": explains the binding strategy
  from the user's point of view

## Out of scope

- **Cell-level editing**: `DynamicGrid` is currently read-only; if inline editing is needed later, it can be decided
  then whether "writing our own two-way binding mechanism" or "projecting onto ViewModel POCOs" costs less
- **Avalonia CompiledBinding support for `DataRowView`**: an Avalonia upstream issue, not handled at the Polhem level
- **Extracting `FormatCell` into `Polhem.UI.Core` to share it with the `DynamicGrid` of the then `Bee.UI.Maui`
  (since removed)**: the behavior is
  symmetric but the carrier types differ (Avalonia's `DataRowView` uses `row.Row[name]`, MAUI takes a `DataRow`
  directly). Sharing it would first require extracting a helper signature, which is orthogonal to this ADR's
  decision; this ADR does not cover it

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

### 2026-06-11: The implementation moved to `GridControl`

The implementation of this ADR has moved from `DynamicGrid` (a `UserControl` wrapper, now removed) to `GridControl`
(`src/Polhem.UI.Avalonia/Controls/GridControl.cs`; it first inherited `DataGrid` directly, and was later refactored
into a `ContentControl` composite whose inner `DataGrid` is exposed as `InnerGrid`); the binding strategy of
`DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>` + fetching in code is unchanged. The follow-up decision on
the in-cell / EditForm editing strategy is in [ADR-021](adr-021-avalonia-datagrid-editing-strategy.md).

### 2026-06-14: Fixing `supportsRecycling` for list cells

The "Decision" example above used `supportsRecycling: true` for plain-text cells in read-only lists. That conflicts
with "`Text` is computed once, not bound": when the DataGrid recycles presenters across rows it does not rerun the
build delegate, so the displayed text comes apart from the underlying row (in the lookup picker this showed up as
"you see one row and get another one back"). It has been changed to `supportsRecycling: false`; see
[ADR-022](adr-022-avalonia-datagrid-cell-recycling.md) for details.

### 2026-08-07: `Bee.UI.Maui` has been removed

This ADR uses the `DynamicGrid` of the then `Bee.UI.Maui` project in several places as a point of comparison or a
sharing target (the Context, the symmetric behavior of `FormatCell`, the sharing idea under "Out of scope").
**`Bee.UI.Maui` was removed on 2026-07-28** (before the project was renamed Polhem), and the UI family has
converged on two tracks: Avalonia (covering desktop / iOS / Android / WASM) and Blazor.Server.

So the items about "aligning with MAUI / sharing code" no longer have a counterpart and are no longer to-dos; the
behavior requirement of `FormatCell` itself still holds, there is simply no second carrier to align with any more.
The Context section keeps its original text to preserve the context of the decision.
