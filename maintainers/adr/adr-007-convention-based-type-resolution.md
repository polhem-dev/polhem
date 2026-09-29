# ADR-007: Derive API types automatically by naming convention

## Status

Accepted (2026-04-16)

## Context

The framework separates types into an API layer and a BO layer (see
[API Contract and BO Parameter Design Principles](../../docs/en/api/api-bo-contract-design.md)):

- **BO layer**: `{Action}Args` / `{Action}Result` (plain POCOs)
- **API layer**: `{Action}Request` / `{Action}Response` (with MessagePack serialization attributes)

After `JsonRpcExecutor` runs a BO method, it must convert the `{Action}Result` returned by the BO into the matching
API `{Action}Response`, so that the client can deserialize it correctly.

### The previous approach

Previously the mapping from a contract interface to an API type was registered statically through
`ApiContractRegistry.Register<TContract, TApi>()`:

```csharp
// Must be called by hand at application startup for every API method
ApiContractRegistry.Register<ILoginResponse, LoginResponse>();
ApiContractRegistry.Register<IPingResponse, PingResponse>();
// ...
```

### Problems

1. **Easy to miss**: if a new API method is not registered, the BO's return value throws an `InvalidCastException`
   straight up, and the error message does not point directly at the root cause.
2. **Tedious extra step**: every new action needs an extra change to the registration code, which goes against the
   design philosophy of "the naming convention is the contract".
3. **Actual usage**: a review of the source code and startup flow in the repository found no call to
   `ApiContractRegistry.Register` anywhere, which shows that the manual registration mechanism is hard to maintain in
   practice.

## Decision

Derive the API response type automatically with **reflection + naming convention** instead, handled in one place by
`ApiOutputConverter`:

```
BO returns: {Action}Result   ──reflection search of the Polhem.Api.Core assembly──▶   API response: {Action}Response
```

### Implementation points

- Add [`ApiOutputConverter`](../../src/Polhem.Api.Core/Conversion/ApiOutputConverter.cs), which converts the type
  right after `JsonRpcExecutor.ExecuteAsyncCore` finishes the BO call
- Reflection results are cached in a `ConcurrentDictionary<Type, Type>`, so each BO type is scanned only once
- `typeof(void)` is used as a sentinel meaning "no matching type found" (because `ConcurrentDictionary` does not
  accept null values)
- When no matching type is found, the original value is returned and the flow is not interrupted (backward
  compatible)

### Naming convention (mandatory)

Automatic derivation relies on the following naming convention; types that violate it cannot be converted
automatically:

| Layer | Input | Output |
|-------|-------|--------|
| BO (`Polhem.Business`) | `{Action}Args` | `{Action}Result` |
| API (`Polhem.Api.Core`) | `{Action}Request` | `{Action}Response` |
| Contract (`Polhem.Api.Contracts`) | `I{Action}Request` | `I{Action}Response` |

For example: `PingResult` → `PingResponse`, `LoginResult` → `LoginResponse`.

## Trade-offs

### Advantages

- **Zero boilerplate**: adding an API method does not touch the startup code; following the naming is enough for
  the mapping to happen automatically
- **Errors surface earlier**: a naming mismatch fails visibly in the first test, instead of running silently with a
  missing registration
- **Centralized code**: the type conversion logic is gathered in `ApiOutputConverter`, symmetric with
  `ApiInputConverter` (the input side)

### Costs

- **Reflection cost on the first call**: `Assembly.GetTypes()` has to scan once; the cache removes the impact after
  that
- **Naming deviations cannot be handled automatically**: types that break the `{Action}Result` / `{Action}Response`
  pattern need individual handling (there are currently no exceptions)
- **Limited to one assembly**: currently only the `Polhem.Api.Core` assembly is searched; if API types are spread
  across several assemblies in the future, the search scope has to be widened

## Consequences

### Code

- **Added**: `src/Polhem.Api.Core/ApiOutputConverter.cs` (later moved into the `Conversion/` subfolder)
- **Changed**: `src/Polhem.Api.Core/JsonRpc/JsonRpcExecutor.cs` (one added line of calling code)
- **Changed**: `src/Polhem.Api.Core/ApiInputConverter.cs` (strengthened the `JsonElement` deserialization path;
  later moved into the `Conversion/` subfolder)
- **Kept**: `ApiContractRegistry` (used for the MessagePack serialization conversion of the Encoded/Encrypted formats)
  — **this type has since been removed**; see the note in [ADR-004](adr-004-messagepack-payload.md).

### Documents

- This ADR
- Updated [API Contract and BO Parameter Design Principles](../../docs/en/api/api-bo-contract-design.md) to remove the manual
  registration step
- Updated the [End-to-End Development Cookbook](../../docs/en/guides/development-cookbook.md) to explain the role of
  `ApiOutputConverter`
- Updated the API contract section of [Development Constraints and Anti-Patterns](../../docs/en/architecture/development-constraints.md)

### What it means for developers

- When adding an API method, the framework completes the type conversion automatically as long as the naming
  convention is followed
- A name that deviates from the convention lets the BO's return value flow straight to the client (which may cause
  type errors), and should be checked strictly in code review

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-08-09: API types carry no serialization attributes.** "Context" describes `{Action}Request` /
  `{Action}Response` as carrying MessagePack serialization attributes. Since
  [ADR-036](adr-036-wire-serialization-externalized.md) they are plain classes, and their wire binding is a formatter
  registered explicitly in `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs`
  ([ADR-037](adr-037-wire-explicit-registration.md)). The naming convention of this ADR is unchanged.
- **2026-09-27: where the conversion runs.** `ApiOutputConverter.Convert` is now called from
  `JsonRpcExecutor.ExecuteAsync`; the `ExecuteAsyncCore` method named under "Implementation points" no longer exists.
