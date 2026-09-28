# Wire contracts

TypeScript declarations generated from the Polhem message types, published so a client in another
language describes the API with the same shapes the server does.

## Why generated

A hand-written type table in another repository is a second authority for the same API contract.
When the server renames a property, that copy does not find out — and the symptom is a field
silently missing, not an error. Generating it makes the table a derivative rather than a claim.

## What is here

| File | Contents |
|------|----------|
| `messages.d.ts` | The message types as TypeScript interfaces |
| `type-names.ts` | Assembly-qualified type names. An encoded payload carries one in its envelope, and it must name the type the addressed method takes |

## What these describe

The **JSON shape on the wire**, not the CLR declarations:

- `Guid` and `DateTime` are `string`, because that is what they are in JSON.
- Enums are string literal unions — the server writes them with `JsonStringEnumConverter`.
- An `object`-typed member is `WireValueEnvelope`, the discriminated `[code, value]` envelope.
- A member is optional (`?`) when the JSON wires may leave it out, and absent means the CLR default.
  A value-typed member is required only where the server always writes it, because its initial
  value in .NET is not the CLR default.
- `DataSet` and `DataTable` follow their custom converters, which reflection cannot see; those
  few shapes are hand-written in the generator's preamble.

## Regenerating

Generated and verified by `WireContractGeneratorTests` in `tests/Polhem.Api.Core.UnitTests`. The test
fails when the message types stop matching this file, which is the point: a diff here is an API
contract change, and a renamed or removed property breaks clients that do not ship with the
framework.

```bash
POLHEM_REGENERATE_WIRE_CONTRACTS=1 dotnet test tests/Polhem.Api.Core.UnitTests/Polhem.Api.Core.UnitTests.csproj
```

Read the resulting diff before committing it.

## Who consumes this

[`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) — the TypeScript client — syncs
this file from a framework release tag rather than keeping its own version, and its CI compares
what it fetched against that release.

That has a consequence worth expecting rather than discovering: **a wire change made here reaches
that repository only when it is released and connector-js moves to the new tag, and its CI turns
red then**. Nothing turns red when the change lands on `main`, so plan the connector-js follow-up
for the same release. Releasing a change to these declarations or to `wire-fixtures/` without it
leaves the two halves of one contract disagreeing, with the TypeScript side reading the old shape.
