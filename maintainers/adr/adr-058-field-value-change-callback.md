# ADR-058: A field change can call back its own form's BO, which returns the values for the rest of the row

## Status

**Accepted (2026-10-27)**

## Context

When the user picks a row in a lookup window, the UI writes each `RelationFieldMappings` destination from the
picked row. The lookup query returns only the related form's `LookupFields`, so a mapping whose source is not listed
there cannot be written; until now the front end threw for it. A leave request that should carry the employee's
department along with the employee, or an order line that should bring back a part's warehouse, stock and price, hit
this as soon as the value needed is not one the lookup window shows.

Two ways of widening the lookup were considered and rejected:

- **Let the opener pass the extra fields.** The client would name fields of another form to read, so the
  `LookupFields` boundary that `GetLookup` relies on (it does not check the `Read` permission, because the lookup
  fields are what it exposes) would be decided by the caller.
- **Let the server work out the extra fields from the opener** (the client says "opened from
  `Customer.Customer.employee_rowid`"). This keeps the field list on the server, but it only serves lookups, and it
  still makes the lookup query return values the lookup window never shows.

A field typed by hand needs the same thing: a part number entered without the window should still bring back its
price, and should be checked against the database while the user is still on the row.

## Decision

### 1. A flag on the field and one method on the form's own BO

`FormField.ValueChangeCallback` marks a field whose changes call back the server. The front end then calls
`FormBusinessObject.FieldValueChange` (`<ProgId>.FieldValueChange`) of the form being edited, with the table, the
field, the new value and a copy of the current row. The answer carries the values to write into the row and the
errors and warnings to show.

The callback goes to the form being edited, not to the related form: that form knows what the field means on this
document, and its BO is the place for document logic. The framework handles relation fields itself
(`ResolveRelationValues`, which reads the related row through the related form's BO); a BO overrides
`FieldValueChange`, calls the base method and adds values for its own fields, keyed on the field name. There is one
method rather than a method name per field, so the API surface does not grow with each form.

### 2. The server decides what is read, from the definitions

The request names a field, never the fields to return: the destinations come from the field's
`RelationFieldMappings` on the server. The related row is read under the related form's lookup rules
(`GetLookupFilter` and the `Read` record scope, through `ResolveRelationRow`), so a row id the user could not have
picked returns nothing. The call requires `Create` or `Update` on the form being edited, since it only serves editing.

A BO that overrides `GetLookup` to narrow what can be picked must override `ResolveRelationRow` the same way; the
remarks of both methods say so.

### 3. No chained callbacks

Only the change the user made calls back. The values a callback writes do not call back again, even when their
fields carry the flag, and neither do the writes of a lookup pick or the events a committed row edit republishes.
Each front end stamps every write with a `FieldValueOrigin`, and `FieldValueChangeCoordinator` acts only on a user
change; a lookup pick calls back once, after all of its writes, so the server sees the whole pick. Clearing a lookup
clears the destinations on the client and does not call back.

Chaining was rejected because the order and the number of round trips would depend on the data, a pair of fields
could call each other forever, and a BO that needs several fields at once can return them all from the first call.

### 4. The callback logic lives once in `Polhem.UI.Core`

`FieldValueChangeCoordinator` decides whether to call, sends a copy of the row, discards an answer that arrives after
the field changed again or the row was deleted, and writes the answer through the head's own setter. The Avalonia and
Blazor `FormDataObject`s only stamp origins and show the messages. `LookupFieldRules.ApplySelection` skips, instead of
rejecting, a source the lookup does not return on a field that calls back.

### 5. A separate connector interface

`IFormApiConnector` is not on the list of interfaces ADR-046 lets a minor version extend, so the callback is
`IFieldValueChangeConnector`, which `FormApiConnector` also implements. A front end whose connector lacks it makes no
call and keeps the earlier lookup rule.

### 6. DefineEditor turns the flag on where a pick would need it

The relation dialog has a checkbox for the flag. Until the user changes it, it is on whenever an included source is
missing from the related form's `LookupFields`, and it stays on for a field that already had it. The dialog's warning
about such a source now covers regular mappings as well as lookup-only ones, since a pick writes both.

## Consequences

- Every marked change costs a round trip, and its values arrive after the edit. The coordinator drops answers that
  no longer match the row, so a fast typist does not see an old value land on a newer one.
- The wire gained `FieldValueChangeRequest` and `FieldValueChangeResponse`; `polhem-connector-js` follows in the same
  release.
- A mapping source is no longer required to be in `LookupFields` when the field calls back, so `LookupFields` stays
  the set of columns the lookup window shows.
- Fields marked as sensitive are treated like any other field: a mapping that names one returns it, as the decision
  to map it was the form designer's.
- Only BO overrides extend the callback; there is no plug-in hook for it.
