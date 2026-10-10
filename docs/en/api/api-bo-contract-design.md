# API Contract and BO Parameter Design Principles

[繁體中文](../../zh-TW/api/api-bo-contract-design.md) · [← Docs Index](../README.md)

This document describes the design architecture and usage of API Contracts (Request / Response) and BO Parameters (Args / Result) in the Polhem framework, intended for developers extending API methods or implementing BO logic.

---

## Core Concept

The framework separates parameter types between the API transport layer and the BO business logic layer, using **contract interfaces** to define shared properties and ensure both layers remain independent with clear separation of concerns.

```
Contract Interface (ILoginRequest / ILoginResponse)   <-- Single source of truth
     |
     +-- API Type (LoginRequest / LoginResponse)  <-- With serialization, for API transport
     |
     +-- BO Type  (LoginArgs / LoginResult)       <-- Pure POCO, for business logic
```

**Why separate layers?**

- Clients (`Polhem.Api.Client`) only interact with API types, without knowing BO implementation details
- The BO layer has no dependency on API assemblies, enabling independent testing and evolution
- BOs can add properties beyond the contract for BO-to-BO calls without changing the API types (a remote caller can still set them; see Scenario 2)

---

## Type Overview

### Contract Interfaces (Polhem.Api.Contracts)

Define the property contracts for API method inputs and outputs. Contain only read-only properties with no serialization attributes. Interfaces are split by axis into `Polhem.Api.Contracts.System` / `.Form` / `.AuditLog`; the root `Polhem.Api.Contracts` namespace holds only cross-axis types (`IExecFuncRequest` / `IExecFuncResponse`).

```csharp
namespace Polhem.Api.Contracts.System
{
    public interface ILoginRequest
    {
        string UserId { get; }
        string Password { get; }
        string ClientPublicKey { get; }
    }

    public interface ILoginResponse
    {
        Guid AccessToken { get; }
        DateTime ExpiredAt { get; }
        string ApiEncryptionKey { get; }
        string UserId { get; }
        string UserName { get; }
        string TimeZone { get; }
        string Culture { get; }
    }
}
```

### API Contract Types (Polhem.Api.Core.Messages.System)

Inherit `ApiRequest` / `ApiResponse` and implement the contract interfaces. Clients use these types to send requests and receive responses.

They carry **no serialization attributes at all** — a plain class with public read/write properties is the whole recipe:

```csharp
public sealed class LoginRequest : ApiRequest, ILoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientPublicKey { get; set; } = string.Empty;
}

public sealed class LoginResponse : ApiResponse, ILoginResponse
{
    public Guid AccessToken { get; set; } = Guid.Empty;
    public DateTime ExpiredAt { get; set; }
    public string ApiEncryptionKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
}
```

Where the wire binding lives instead:

- **JSON** — System.Text.Json binds by property name; nothing to declare.
- **MessagePack** — a hand-written contract in `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs`
  names each member explicitly. **A new message type has to be registered there**, because the
  resolver has no reflection fallback on platforms that forbid dynamic code; `WireContractDriftTests`
  (in `tests/Polhem.Api.Core.UnitTests`) fails when the wire closure and the registrations disagree.

This is why the attributes are gone: keeping them would have put a transport package on the dependency
surface of every consumer of the definition layer. See
[ADR-036](../../../maintainers/adr/adr-036-wire-serialization-externalized.md).

> **Framework repository only.** `WireContract`, `WireContracts` and `MessagePackCodec` are `internal`,
> so an application outside this repository cannot register a formatter for a message type of its own.
> Such a type reaches the MessagePack wire only through the reflection-based resolver — which works on
> desktop and server, and throws on a runtime without dynamic code. Declaring `codec: json` per request
> ([ADR-044](../../../maintainers/adr/adr-044-payload-codec-negotiation.md)) avoids the question entirely.

> **Polymorphic hierarchies** (`FilterNode` and its subtypes) need more than a member list, so they have a dedicated hand-written formatter — `FilterNodeFormatter` — that writes a discriminator alongside the members. Same file family, same registration; only the formatter is bespoke.

### BO Parameter Types (Polhem.Business)

Inherit `BusinessArgs` / `BusinessResult`, implement contract interfaces, and are pure POCOs. May include additional BO-specific properties beyond the contract (see Scenario 2 below).

```csharp
public sealed class LoginArgs : BusinessArgs, ILoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientPublicKey { get; set; } = string.Empty;
}
```

---

## Naming Conventions

| Purpose | Pattern | Example | Assembly |
|---------|---------|---------|----------|
| Contract interface (input) | `IXxxRequest` | `ILoginRequest` | Polhem.Api.Contracts |
| Contract interface (output) | `IXxxResponse` | `ILoginResponse` | Polhem.Api.Contracts |
| API input | `XxxRequest` | `LoginRequest` | Polhem.Api.Core |
| API output | `XxxResponse` | `LoginResponse` | Polhem.Api.Core |
| BO input | `XxxArgs` | `LoginArgs` | Polhem.Business |
| BO output | `XxxResult` | `LoginResult` | Polhem.Business |
| BO pipeline state | `XxxContext` | `SaveContext` | Polhem.Business |

**Which one to reach for: `Args` / `Result` cross a layer boundary, `Context` is shared within one
operation.** An `XxxArgs` is deserialized from the caller and an `XxxResult` is serialized back, so
both are pure POCOs that only ever hold data a client may see. An `XxxContext` never leaves the
server: it carries what the steps of a single call need in common — the resolved repository and form
schema, a pre-delete snapshot, the outputs one step produces for the next — including server objects
that have no business on the wire.

Two consequences worth knowing before choosing. Anything stored on `Args` **could have been set by
the caller**, so intermediate state placed there is caller-influenced by construction. And because a
context is one object rather than a signature, giving the steps one more shared value is a new
property rather than a signature change that breaks every existing override.

---

## Three Usage Scenarios

An API method always has all three layers: the BO method takes the concrete `XxxArgs` and returns the
concrete `XxxResult`, and the executor converts the API request into the args and the result into the
API response by copying the properties they share. The scenarios differ in what the BO types carry.

### Scenario 1: API Method, BO Needs No Extra Properties (Most Common)

API and BO parameter properties are identical: the `XxxArgs` / `XxxResult` types implement the same
contract interfaces as the API types and add nothing.

**Types to create:** Contract interfaces + API contract types + BO parameter types that mirror them
**BO method signature:** Use the concrete `XxxArgs` / `XxxResult` types for parameters and return types

```csharp
public GetOrderResult GetOrder(GetOrderArgs args)
{
    // The executor passes a GetOrderArgs; BO-to-BO calls also pass GetOrderArgs directly.
    return new GetOrderResult { OrderId = args.OrderId };
}
```

### Scenario 2: API Method, BO Needs Extra Properties

BO-to-BO calls require internal properties not visible to the API.

**Types to create:** Contract interfaces + API contract types + BO parameter types with the extra properties

```csharp
// BO parameter with extra properties
public sealed class GetOrderArgs : BusinessArgs, IGetOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public bool IncludeCancelledLines { get; set; }  // BO-specific
}

// BO method takes the concrete args type, so the extra property is directly available
public GetOrderResult GetOrder(GetOrderArgs args)
{
    bool includeCancelled = args.IncludeCancelledLines;
    // ...
}
```

> **WARNING:** a BO-specific property is not in the API types, but that does not keep it out of a
> remote caller's reach. A Plain request body is bound to the method's parameter type — the `XxxArgs`
> itself — so any public setter on it can be set by the caller. Treat such a property as caller input
> like the rest of the args (see *Naming Conventions* above), and check `IsLocalCall` where only a
> BO-to-BO call may set it.

### Scenario 3: BO-Only Method (Not Exposed as API)

Internal methods used only within the BO layer, not published as JSON-RPC APIs.

**Types to create:** BO parameter types only (no contract interfaces, no API types)

```csharp
public sealed class RecalcArgs : BusinessArgs
{
    public string OrderId { get; set; } = string.Empty;
    public bool ForceRecalc { get; set; }
}
```

---

## Serialization Rules

| Layer | Serialization attributes | Wire registration |
|-------|:---:|:---:|
| Contract interface | None | — |
| API type | **None** | `WireContracts.*.cs` (framework repository) |
| BO type | None | — |

No layer carries MessagePack attributes. XML annotations belong to the definition types that are
persisted as files, not to these wire messages.

---

## Client Development

When calling APIs through `SystemApiConnector`, always use `Request` / `Response` types:

```csharp
var connector = PolhemApiClient.CreateRemote(endpoint, apiKey).System;

// Use API types, not BO types
LoginResponse response = await connector.LoginAsync("admin", "password");
Console.WriteLine(response.AccessToken);
```

Clients **should not reference** and **do not need** `BusinessArgs`, `BusinessResult`, or any `XxxArgs` / `XxxResult` types.

---

## BO Development

### Method Signatures

BO method parameters and return types use the **concrete `XxxArgs` / `XxxResult` types**, including in the BO interface declarations (`ISystemBusinessObject`, `IFormBusinessObject`):

```csharp
// BO method (and its interface declaration) use concrete types
public LoginResult Login(LoginArgs args) { ... }
```

The contract interfaces (`ILoginRequest` / `ILoginResponse`, etc.) still exist and
are **implemented** by the `XxxArgs` / `XxxResult` types (and the API `XxxRequest` /
`XxxResponse` types) — e.g. `LoginArgs : BusinessArgs, ILoginRequest`. They provide
the shared property contract and cross-layer independence, but the interfaces are
**not used in method signatures**; the signatures bind to the concrete types.

### Response Mapping

When a BO method returns a pure POCO (e.g., `LoginResult`), the framework's `ApiOutputConverter` automatically converts it to the corresponding API type (`LoginResponse`) by **naming convention** — no registration is required.

Convention:

```
{Action}Result  ──reflection lookup in Polhem.Api.Core──▶  {Action}Response
```

For example, `PingResult` is automatically mapped to `PingResponse`. The lookup is cached per result type, so each type is resolved only once.

> Nothing checks the convention. A result type whose name does not end in `Result`, or that has no
> `{Action}Response` in `Polhem.Api.Core`, is not converted: `ApiOutputConverter` returns the BO result
> itself, which then travels as it is. See [ADR-007](../../../maintainers/adr/adr-007-convention-based-type-resolution.md) for background.

### ExecFunc Pattern

`ExecFunc` has the same layers (`IExecFuncRequest`, `ExecFuncRequest`, `ExecFuncArgs` and their
response counterparts), but its only typed member is `FuncId`. The data of each function travels in
the `Parameters` collection (`ParameterCollection`) that every Args / Result and Request / Response
inherits, so adding a function needs no new type.

---

## Steps to Add a New API Method

Using `GetOrder` as an example:

1. **Define contract interfaces** (`src/Polhem.Api.Contracts/<Axis>/`, namespace `Polhem.Api.Contracts.<Axis>`)
   - `IGetOrderRequest.cs` — input properties
   - `IGetOrderResponse.cs` — output properties

2. **Create API contract types** (`src/Polhem.Api.Core/Messages/<Axis>/`; namespace is `Polhem.Api.Core.Messages.<Axis>`)
   - `GetOrderRequest.cs` — inherits `ApiRequest`, implements `IGetOrderRequest`; no attributes
   - `GetOrderResponse.cs` — inherits `ApiResponse`, implements `IGetOrderResponse`; no attributes
   - Register both in `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs` — `WireContractDriftTests`
     fails if you forget

3. **Implement BO method**
   - Method signature uses the concrete `GetOrderArgs` / `GetOrderResult` types
   - Naming must follow the `{Action}Args` / `{Action}Result` convention so that `ApiOutputConverter` can auto-map `GetOrderResult` → `GetOrderResponse`
   - The args / result types implement the contract interfaces (`GetOrderArgs : BusinessArgs, IGetOrderRequest`, etc.) for cross-layer property sharing

4. **Update client Connector** (if needed)
   - Add a corresponding method using `GetOrderRequest` / `GetOrderResponse`

> Apart from the wire registration in step 2, nothing is registered: response mapping is resolved by naming convention (see [ADR-007](../../../maintainers/adr/adr-007-convention-based-type-resolution.md)).

---

## Assembly Dependency Direction

```
Polhem.Api.Contracts           <-- Contract interfaces (shared by API & BO)
    |
    +-- Polhem.Api.Core        <-- API types (with serialization)
    |       |
    |       +-- Polhem.Api.Client  <-- Client (uses Request / Response only)
    |
    +-- Polhem.Business        <-- BO types (pure POCO) + BO interfaces
```

**Principle:** Arrows indicate dependency direction. `Polhem.Api.Core` and `Polhem.Business` do not depend on each other; the contracts they share live in `Polhem.Api.Contracts`. Both have other dependencies as well (such as `Polhem.Definition`); the full graph is in the [Project Dependency Map](../architecture/dependency-map.md).
