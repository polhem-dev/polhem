# ADR-021: In-cell editing strategy for the Avalonia DataGrid

[繁體中文](adr-021-avalonia-datagrid-editing-strategy.zh-TW.md)

## Status

Accepted (2026-06-11)

## Context

[ADR-020](adr-020-avalonia-datagrid-binding-strategy.md) established that `GridControl` (which at the time inherited
`Avalonia.Controls.DataGrid` directly, and has since been refactored into a `ContentControl` composite whose inner
`DataGrid` is exposed as `InnerGrid`) presents `DataTable` rows with `DataGridTemplateColumn` +
`FuncDataTemplate<DataRowView>`. In-cell editing was then added for detail tables: `CellEditingTemplate` supplies the
matching editing control according to `LayoutColumn.ControlType` (`TextBox` / `CheckBox` / `DatePicker` /
`ComboBox`), and write-back goes straight to the `DataRow`.

Measured in the Gallery: **text columns (`TextEdit`) edit normally, and every popup-style editor misbehaves**. The
root cause is inherent in the design of the Avalonia `DataGrid` editing pipeline:

- The editing pipeline assumes "focus stays inside the cell while editing", and treats focus leaving as the signal to
  commit / end editing
- The drop-down list of `ComboBox` and the picker panel of `DatePicker` are both **popups**: as soon as one opens,
  focus leaves the cell, `DataGrid` decides editing has ended and tears down the `CellEditingTemplate`, and the popup
  is closed along with it or the interaction is cut off
- `CheckBox` has no popup, but "double-click to enter edit mode → click once more to check it" is an unnecessary
  ritual for a boolean column

This is not an implementation bug but a structural conflict between the `DataGrid` template-column editing pipeline
and popup-style controls.

## Options considered

1. **Built-in bound columns** (`DataGridTextColumn` / `DataGridCheckBoxColumn`): they integrate well with the editing
   pipeline but need a bindable row object, and `DataRowView` is exactly the type ADR-020 confirmed the Avalonia
   binding engine cannot resolve. Working around it means building a wrapper VM per row, which violates the
   zero-projection principle of DataSet-as-DTO.
2. **Intercepting editing pipeline events** (detecting in `CellEditEnding` that a popup is open and cancelling the
   end): the Avalonia `DataGrid` does not expose enough hooks to tell that "focus moved into this cell's own popup";
   the hack is fragile and version-sensitive.
3. **Always-on editors**: the interactive control of a popup-style column is put straight into the `CellTemplate`
   (the display template), bypassing the editing pipeline entirely. There is no edit session to tear down, so popups
   behave normally, and a click takes effect immediately. The idiomatic pattern in LOB applications.
4. **A row-level editing panel**: the grid is fully read-only, and after selecting a row you edit it outside the grid
   with a set of field editor controls. The most consistent experience, but it takes more steps and does not address
   the need for "quick cell-by-cell changes".
5. **Switching grids** (the official `TreeDataGrid`, commercial Actipro / DevExpress): they bring their own editing
   model, but the refactoring is large and the theme ecosystem is a separate matter; a medium- to long-term option.

## Decision

Adopt a **hybrid strategy (mainly option 3)**, dispatching on `LayoutColumn.ControlType`:

| ControlType | Cell presentation | How it is edited |
|-------------|----------|---------|
| `TextEdit` / `ButtonEdit` / `Auto` (text kinds) | `TextBlock` | `CellEditingTemplate` (enter with double-click / F2, edit in a `TextBox`); focus stays in the cell, so the editing pipeline works normally |
| `CheckEdit` | An always-on centered `CheckBox` (disabled when read-only) | Click to check directly; bypasses the editing pipeline |
| `DropDownEdit` / `DateEdit` / `YearMonthEdit` | **At rest**: `TextBlock` formatted text (exactly the same as a read-only cell); **on click**: swapped for an editing control, managed by the `CellTemplate` itself | A single click on the cell → swaps in the editor (the drop-down opens automatically); after the value is written back or editing ends it **swaps back to the text presentation**. The swap is managed by the control itself, not by the editing pipeline, so the popup is not torn down |

An alternative version of click-to-swap that was evaluated was "keep the interactive control in the cell all the
time": measured, it caused a chain of styling problems (ComboBox width, `DatePicker` truncation, visual inconsistency
with text cells), and required constantly fighting the theme with local values. Returning to a `TextBlock` at rest
solves all of the visual consistency problems at once, and the editor exists only at the moment of editing.

Supporting rules:

- Popup-style columns are marked `IsReadOnly = true` so the DataGrid editing pipeline never steps in. Lifecycle of the
  swap: swapped in on `PointerPressed` → the end condition depends on the control, and swapping back **re-reads the
  `DataRow`** to present the value that was written back
  - `ComboBox`: after swapping in, the automatic opening is **deferred through the Dispatcher** (later events of the
    same click would close a drop-down that opened immediately); only a close after it has "really been open" counts
    as the end of editing
  - `DatePicker`: **swaps back only when a value is confirmed (`SelectedDate` changes)**. `LostFocus` must not be
    used (the spinner flyout takes focus and would tear the editor down early); an editor whose selection was
    abandoned stays in place and is cleaned up by "the start of the next inline edit" or by `EndEdit()`
  - The grid allows only one inline editor at a time (opening a new one first closes the old one; it is also reset
    when rows are re-realized)
- Whether a cell is editable is decided when the template is built; when `SetControlState` switches read-only it
  **re-realizes the rows** (resets `ItemsSource`)
- The read-only presentation (list mode, `View` mode, `LayoutColumn.ReadOnly`) is a `TextBlock`; **exception: a
  boolean column is presented as a centered `CheckBox` in every state** (disabled when read-only), because a checkbox
  is easier to read than "True"/"False" text
- Date editing keeps the **three-part `DatePicker`** (`DayVisible` distinguishes Date / YearMonth): the editor appears
  only at the moment of editing, so width truncation is no longer a constant problem, and the spinner experience
  matches the form-side `DateEdit`
- Write-back still goes straight to the `DataRow` (ADR-020's limitation applies equally to controls inside the display
  template), and dirtiness is reflected through `FormDataObject.MarkDirty()`
- Change listening on the controls always hooks **property changed** (`TextProperty` / `SelectedDateProperty`)
  rather than relying on the `TextChanged` / `SelectedDateChanged` events, which are not guaranteed to fire when the
  value is set programmatically

Option 4 (row-level editing) has landed as the **EditForm mode** (2026-06-11):

- The editing mode (`GridEditMode`: `InCell` / `EditForm`) is a **UI-layer property** (`GridControl.EditMode` /
  `DynamicForm.DetailEditMode`) and does not go into the shared definition layer: `LayoutGrid` is shared across UI
  families, and the editing model is each framework's presentation decision
- It is presented as a **popup dialog** (`RowEditDialog` wrapping `RowEditPanel`) rather than an inline panel below
  the grid: detail rows in batch work can be numerous, and an inline panel has two structural problems, "the editing
  area is too far from the selected row" and "expanding / collapsing plus new rows landing at the end of the table
  make the screen jump". A dialog causes zero layout shift and does not depend on the row count
- Buffered semantics go through the row-edit protocol of `FormDataObject` (`BeginRowEdit` / `CommitRowEdit` /
  `CancelRowEdit`, wrapping the ADO.NET `DataRow.BeginEdit` family). Measurements pinned down two ADO.NET behaviors:
  **`BeginEdit` does not suppress `ColumnChanged`, and an `EndEdit` with no changes still raises `RowChanged`**. So a
  "set of rows being edited" silences the event bridge, a commit diffs Proposed vs Current and raises the events in
  one go, and a commit with no changes goes through `CancelEdit` instead; cancelling raises zero events, and
  confirming raises the complete set
- After confirmation the grid re-realizes and `ScrollIntoView` scrolls back to that row; `Add` opens the dialog to
  edit a new row, and the empty row is removed on cancel

## Consequences

- Popup-style columns are edited on click, dropping the ritual of "double-click into edit mode first"; text columns
  keep the standard DataGrid editing feel. Having both feels side by side is a deliberate trade-off of this strategy
- The popup-style columns of an editable detail table rest as a `TextBlock` and build an editor only for the cell
  being edited, so at rest they cost the same as a read-only cell. Only boolean columns keep a `CheckBox` on every
  row, which has a rendering cost when there are very many rows; in the detail table scenario (within a few dozen
  rows) it is not noticeable, and list mode is unaffected (always read-only)
- If the Avalonia `DataGrid` editing pipeline supports popup-style editors in the future, or `TreeDataGrid` is
  adopted, this strategy can be rolled back column by column without affecting the caller-facing API

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: dirtiness is tracked without a call.** `FormDataObject.MarkDirty()` no longer exists. `IsDirty` has a
  private setter and is set by the bridge from the ADO.NET `ColumnChanged` / `RowChanged` events, so a write straight
  to the `DataRow` from a cell editor marks the form dirty with no extra call
  (`src/Polhem.UI.Avalonia/DataObjects/FormDataObject.Events.cs`).
- **2026-09-27: `DynamicForm` was folded into `FormView`.** There is no separate `DynamicForm` control; the detail
  editing mode is `FormView.DetailEditMode` (`src/Polhem.UI.Avalonia/Views/FormView.cs`), next to
  `GridControl.EditMode`.
