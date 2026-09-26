# ADR-023: A definition-driven lookup relation mechanism

[繁體中文](adr-023-lookup-relation-mechanism.zh-TW.md)

## Status

Accepted (2026-06-15)

## Context

[ADR-005](adr-005-formschema-driven.md) established FormSchema as the definition hub; [ADR-001](adr-001-dataset-as-dto.md)
carries master-detail data in a DataSet without projection. FormSchema has long had "relation fields" to express
foreign keys: a relation field carries `RelationProgId` (pointing to the referenced form) + `RelationFieldMappings`
(source field → local `ref_*` destination field). But until now a relation field could only **display** an existing
value; there was no UI mechanism to "open a window, pick a related record, and write back the foreign key together
with the display fields". Real ERP forms (an order picking a customer, a product picking a supplier, detail rows each
picking a product) depend heavily on this action.

Breaking down the requirements:

- **Which record to pick**: a searchable, pageable list of related records for the user to choose from.
- **What to write back**: the `sys_rowid` of the selected record (the relation key) + the display fields brought
  over according to `RelationFieldMappings` (`ref_*`).
- **What to display**: a relation field should normally show a human-readable "code - name", not a bare Guid.
- **Who produces the editor**: the layout generator should be able to decide from "this is a relation field" that a
  lookup-window editor is used, so the definition side does not have to write the control type field by field.
- **Two entry points**: master-table fields (a single relation) and each row of a detail grid (an InCell relation).

## Options considered

1. **Callers wire up lookups themselves**: each form writes its own "open window + write back" code. The most
   flexible, but it completely contradicts the goal of "definition-driven, zero CRUD code", and every form repeats
   the same boilerplate.
2. **Describe it with a separate lookup definition node** (a new `<Lookup>` element alongside the existing relation
   field): expressive, but it overlaps semantically with the existing `RelationProgId` / `RelationFieldMappings`,
   creating the cognitive load of "two sets of relation definitions" and a risk of inconsistency.
3. **Reuse the existing relation field semantics + automatic resolution by the layout generator (adopted)**: a
   relation field already holds the complete information of "which form it points to and which fields it brings
   back"; all that is missing is the "display field" and the "lookup window UI". Add two definition properties,
   `DisplayField` / `LookupFields`, and have `FormLayoutGenerator` produce a lookup-window editor for relation fields
   automatically, without adding a parallel relation definition.

## Decision

Adopt **option 3**, in three layers:

### Definition layer (`Polhem.Definition`)

- Relation fields keep `RelationProgId` + `RelationFieldMappings` (source → `ref_*`) as the **single source of truth
  for the relation**.
- Add `DisplayField` / `DisplayFields`: the lookup editor and list show a composite "code - name" (the separator is
  " - ", to avoid confusion with names that contain spaces).
- Add `FormSchema.LookupFields`: the set of fields the lookup window's list presents.
- Coverage rule of `FormLayoutGenerator`: a relation field is resolved automatically to a `ButtonEdit` (a
  lookup-window editor); the matching `ref_*` destination fields are displayed through the relation field and **do
  not get separate editors of their own** (so the same relation does not appear twice in the layout).

### Backend (`Polhem.Api` / `Polhem.Business`)

- `FormBusinessObject.GetLookup` (including the overridable `GetLookupFilter()`) is the data fetch dedicated to the
  "lookup window list": in essence a lookup variant of `GetList`, which a BO can narrow by context (for example,
  listing only active suppliers).
- After a relation field is saved, the `ref_*` display fields are recomputed by the JOIN when the server reloads (what
  the client writes is only for immediate display, not the authoritative value).

### Frontend (`Polhem.UI.Avalonia`, Desktop first)

- `LookupPanel` (search + list + selection) / `LookupDialog` (a window wrapper opened with `Window.ShowDialog`).
- `ButtonEdit` has the lookup flow built in: display binding, click the icon to open the window, write back
  `sys_rowid` + the mapped `ref_*` after selection, and clearing.
- `GridControl` detail InCell: clicking a relation cell opens the same `LookupDialog`, picking row by row.

Accompanying fix: the `GetNewData` skeleton now includes the `RelationField` fields; otherwise, in the add flow, the
display value has no column to land in after selection and cannot be brought back.

## Consequences

- A form with several relations, master-detail and per-row selection in the details can be **achieved purely by
  definition** (relation fields + `RelationFieldMappings` + `DisplayFields` + `LookupFields`), without writing UI /
  CRUD code; this is the basis of the order example in `apps/Polhem.Northwind`.
- `ref_*` fields are derived display fields and the authoritative value comes from the server JOIN: what the client
  writes only serves immediate presentation, and after a reload the server's result wins, avoiding "the frontend
  brought a wrong display value and it got stored".
- **Desktop-only**: `LookupDialog` depends on `Window.ShowDialog` (multiple windows); Avalonia WASM / Mobile are
  single-view and have no `Window`, so going cross-platform requires `Polhem.UI.Avalonia` to change to a dialog
  abstraction based on a single-view overlay (`IDialogPresenter`, or a community overlay solution). That is
  framework work for a separate plan; the scope of this decision stops at Desktop.
- Once relation fields automatically become `ButtonEdit`, the need for a display-only (not selectable) relation has
  to be expressed with a read-only field; it is currently covered by `FormField.ReadOnly` (see CHANGELOG 4.10.0).
- Consistent with the in-cell editing strategy of [ADR-021](adr-021-avalonia-datagrid-editing-strategy.md): relation
  fields in details take the "click to swap in the editor" path, so the lookup window is not torn down by the
  DataGrid editing pipeline.
