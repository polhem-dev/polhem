# ADR-001: Use DataSet as the cross-layer DTO

## Status

Accepted

## Context

The framework needs a cross-layer data transfer object (DTO) to carry Master-Detail form data between Business
Objects, Repositories and the API. The common options are:

1. Strongly typed POCO / entity classes
2. ADO.NET DataSet / DataTable
3. Dictionary / dynamic objects

## Decision

Use the ADO.NET `DataSet` as the cross-layer DTO.

## Rationale

- **Dynamic structure**: FormSchema defines fields at runtime, so the structure of a form is not known at compile
  time. DataSet naturally supports dynamic fields, and no POCO class has to be generated for each form.
- **BPM electronic forms**: besides ERP / HRM systems designed in advance, the framework supports BPM (Business
  Process Management) at runtime: users can build electronic forms dynamically and link them to ERP / BPM processes.
  The structure of these forms is entirely unknown at compile time, so strongly typed POCOs cannot be used. DataSet
  is the only DTO that can carry both predefined forms and dynamic BPM forms.
- **Native Master-Detail support**: a DataSet contains several DataTables, which map naturally onto the master and
  detail tables of a form (Master-Detail) with no extra wrapping.
- **Change tracking**: DataRow has a built-in `RowState` (Added / Modified / Deleted / Unchanged). The Repository
  layer can generate the matching INSERT / UPDATE / DELETE directly from the state, without a separate change
  tracker.
- **Mature serialization**: XML / binary serialization of DataSet is highly mature in the .NET ecosystem, and with a
  custom MessagePack formatter it can also be transmitted efficiently.
- **Cross-framework compatibility**: netstandard2.0 supports DataSet natively, with no dependency on newer APIs.

## Trade-offs

- **Less type safety**: fields are accessed by string index, so misspelled field names cannot be caught at compile
  time.
- **Weaker IntelliSense support**: there are no property hints as with strongly typed POCOs.
- **Not in line with modern .NET conventions**: most new frameworks prefer strongly typed POCOs + EF Core, and
  DataSet is seen as the "older" approach.

## Consequences

- Form data (master and detail records) crosses layers as a DataSet; no POCO DTO is generated per form. The
  parameters and results of business object methods are plain POCOs (`{Action}Args` / `{Action}Result`, see
  [ADR-007](adr-007-convention-based-type-resolution.md)), and they carry a DataSet where form data is involved
- FormSchema-driven CRUD operations rely on DataRow.RowState to decide the kind of operation
- Custom MessagePack formatters (in `Polhem.Api.Core/MessagePack/`) handle efficient serialization of DataSet
- `Polhem.Base/Data/` provides DataTable / DataSet / DataRow extension methods to simplify common operations
