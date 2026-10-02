# ADR-049: Polhem's JSON-RPC runs on the `Polhem.JsonRpc` packages, and `Polhem.Api.AspNetCore` is removed in 1.2.0

## Status

**Accepted (2026-10-02)**

Amends decision 5 of [ADR-048](adr-048-rename-base-to-core-in-1-1.md) and narrows the scope of the compatibility rule
stated in the context of [ADR-046](adr-046-api-evolution-policies-for-1-0.md).

## Context

Up to 1.1.0, Polhem carried its own JSON-RPC 2.0 implementation: `JsonRpcExecutor` and the message types it read and
wrote (`JsonRpcRequest`, `JsonRpcResponse`, `JsonRpcError`) in `Polhem.Api.Core`, an ASP.NET Core controller
(`ApiServiceController`) in `Polhem.Api.AspNetCore`, and two client providers behind `IJsonRpcProvider` in
`Polhem.Api.Client`. Nothing in that implementation was specific to Polhem except the parts that sat on top of it:
the payload envelope, its encryption and body codec, the replay frame, API key and access token checks, method-level
access control, the error contract and the anomaly records.

The protocol half was extracted into its own packages, maintained in
[polhem-dev/polhem-jsonrpc](https://github.com/polhem-dev/polhem-jsonrpc): `Polhem.JsonRpc` (the messages),
`Polhem.JsonRpc.Server` (the dispatcher), `Polhem.JsonRpc.AspNetCore` (the HTTP endpoint) and
`Polhem.JsonRpc.Client` (the connector). Their own design is recorded in that repository's ADRs and is not repeated
here. The packages follow Polhem's existing mechanism rather than inventing one: a method is `ProgId.Action`, an
application object factory creates the object for the ProgId, the action resolves by name under the same rule the
executor used, and parameters and results follow the `{Action}Request` / `{Action}Response` convention, with nothing
registered per method.

What this ADR settles is how Polhem builds on them, and what that does to Polhem's own public surface.

## Decision

### 1. Polhem depends on the packages, never the other way round

| Polhem package | Depends on |
|----------------|------------|
| `Polhem.Api.Core` | `Polhem.JsonRpc.Server` |
| `Polhem.Api.Client` | `Polhem.JsonRpc.Client` |
| HTTP hosts | `Polhem.JsonRpc.AspNetCore` |

The packages know nothing about Polhem. Everything Polhem-specific stays in Polhem and attaches through the
packages' extension points, in `Polhem.Api.Core.Dispatch`:

- `PolhemObjectFactory` creates the business object. For a call that arrives over HTTP it first checks the
  `X-Api-Key` header and the `Authorization` header, so a call that fails them never gets a business object, the
  order the controller ran them in. An in-process call takes its access token from the transport's items.
- `PolhemMethodPolicy` admits only methods that carry an access declaration.
- `PolhemAccessFilter` runs the method's `[ApiAccessControl]` check and records slow calls.
- `PolhemPayloadFilter` restores the payload envelope (decryption, decompression, the body codec, the replay frame)
  and writes the result back in the same envelope.
- `PolhemParameterBinder` binds the parameter as the executor did, through the wire message type.
- `PolhemExceptionMapper` maps an exception through `JsonRpcErrorContract`, translates the message, logs a masked
  failure and records the anomaly.

`PolhemJsonRpc.CreateServerOptions` assembles them, and `AddPolhemFramework` registers the options and the
dispatcher. A host adds the HTTP endpoint with `AddJsonRpcServer()` and `MapJsonRpc("/api")`; `AddJsonRpcServer`
builds on the options the framework registered, so an application's own filters run inside the framework's.

Whether a call is local is decided by the transport alone: `InProcessTransport` marks it local, the HTTP endpoint
never does, and no header or parameter can change that. `HttpAuthorizationTests` pins it.

The payload envelope stays in Polhem for now. It may later move into an optional package of its own; until then
`PolhemPayloadFilter` is the only place it is applied on the server.

### 2. Framework pipeline types are outside the 1.x compatibility promise

ADR-046 states that a change breaking `PublicAPI.Shipped.txt` waits for the next major version. Some public types in
Polhem are public only because one `Polhem.*` package calls another, not because an application is meant to use
them. A change to them affects no application, so treating it as breaking only freezes the framework's internals.

From 1.2.0, a type is a **framework pipeline type** when it exists for `Polhem.*` packages to cooperate and an
application has no reason to construct, call or implement it. Pipeline types are public but not covered by the
ADR-046 promise. A type is a pipeline type only when it is named in this ADR, so a reader can tell in advance; no
attribute marks them in the code.

Pipeline types in 1.2.0:

- In `Polhem.Api.Core.JsonRpc`: `JsonRpcParams`, `JsonRpcResult`, `ApiPayload` and `ApiPayloadConverter`, and the
  removed `JsonRpcExecutor`, `JsonRpcRequest`, `JsonRpcResponse` and `JsonRpcError`.
- Everything in `Polhem.Api.Core.Dispatch`.
- In `Polhem.Api.Client.Providers`: the removed `IJsonRpcProvider`, and the transport members of
  `RemoteApiProvider` and `LocalApiProvider`. `ApiConnector.Provider`, typed as the package's
  `IJsonRpcTransport` from 1.2.0.

Not pipeline types, and still covered by ADR-046: `JsonRpcException` (business objects throw it), `JsonRpcErrorCode`
(clients compare against it), the connector methods of `Polhem.Api.Client`, and the constructors of
`RemoteApiProvider` and `LocalApiProvider`.

### 3. `Polhem.Api.AspNetCore` is removed: a breaking change, made as a design correction

`ApiServiceController` read the request, checked the API key and the `Authorization` header, called the executor and
wrote the response. Hosts derived an empty class from it to publish the endpoint, and its checks were `protected
virtual` so a deployment could override them. With the protocol in `Polhem.JsonRpc.AspNetCore` and the checks in
`PolhemObjectFactory`, the controller had nothing left to do, and a minimal API endpoint needs no controller.
`UsePolhemFramework()` only ran a startup check.

These are not pipeline types: hosts referenced the package and derived from the controller. Removing them is a
**breaking change within 1.x**, and it is recorded as one. It is made in 1.2.0 because keeping a controller whose
only remaining job is to call the endpoint would leave a second way to host the API, which every later change would
have to keep in step with the first.

- **The package `Polhem.Api.AspNetCore` is removed**, including `ApiServiceController` and `UsePolhemFramework()`.
  On NuGet it is marked deprecated with `Polhem.JsonRpc.AspNetCore` as the alternate.
- **The startup check moves to `Polhem.Hosting`.** A host that serves the API over HTTP calls
  `services.AddPolhemApiKeyGateCheck()`. It is opt-in because an in-process host has no HTTP endpoint for an API key
  to guard, and would otherwise log a false alarm.

To migrate a host:

1. Replace the package reference `Polhem.Api.AspNetCore` with `Polhem.JsonRpc.AspNetCore`.
2. Delete the controller derived from `ApiServiceController`, and the `AddControllers()` and `MapControllers()` calls
   that served it.
3. After `AddPolhemFramework(...)`, add `services.AddJsonRpcServer()`; after building the app, add
   `app.MapJsonRpc("/api")`.
4. Replace `app.UsePolhemFramework()` with `services.AddPolhemApiKeyGateCheck()`.
5. Move a check that overrode a controller member into a filter, added through `AddJsonRpcServer(options =>
   options.Filters.Add(...))`.

### 4. The wire keeps its payload and drops two departures from JSON-RPC 2.0

Requests carry the same parameters, and a successful response carries the same `result` envelope. Two things that
were Polhem's own are aligned with the specification in the same release:

- **An internal error is -32603**, the code JSON-RPC 2.0 defines for it. Polhem sent -32000, from the range the
  specification leaves to the server. `JsonRpcErrorCode.InternalError` changes its value; it is not a pipeline type,
  so this is part of the breaking change of this release.
- **A response no longer echoes the method name** in a `method` member, which JSON-RPC 2.0 does not define.

Neither broke the specification, which reserves -32000 to -32099 for the server and does not forbid extra members.
They are aligned because a client written against the specification should not need to know about them, and the
release that already changes Polhem's hosts is the cheapest one to change them in.

`WireShapeTests` pins the answer to a plain Ping. Before the two changes, the executor and the dispatcher answered it
identically; the test now pins it without the `method` member. A client that compares the error code against -32000
or reads `method` has to be updated with the server: for [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js),
that is the release that moves its framework tag to 1.2.0.

### 5. Released as 1.2.0, and ADR-048 is amended

Decision 5 of ADR-048 said the rename to `Polhem.Core` was the only exception in 1.x. This ADR is a second one, for
decisions 3 and 4. Decision 5 of ADR-048 now points here. Further breaking changes within 1.x need an ADR of their
own; this one is not a precedent for skipping that.

## Consequences

- The `Polhem.Api.Core.Dispatch` components are the server half of the API. A change to how Polhem checks, decodes or
  answers a call is made there.
- Behaviour that differs from 1.1.0:
  - A method name of the wrong shape, or an action that does not exist, is answered by the dispatcher itself with
    `MethodNotFound` (-32601) and a fixed message, also in debug mode, and leaves no anomaly record. The executor
    answered a malformed name with `UserMessage`.
  - A rejected API key or `Authorization` header is answered with HTTP status 200 and a JSON-RPC error
    (`InvalidRequest`), not with 401. The .NET client therefore throws the exception the error contract rebuilds,
    not `HttpRequestException`. A body that is not JSON gets 415, and one larger than the endpoint accepts gets 413.
  - When the client disconnects, the endpoint passes the cancellation to the dispatcher; the controller answered 499
    without running the call.
  - In-process calls serialize their parameters like remote ones, so the server never holds the caller's objects.
    A filter value in a `Plain` body arrives as text, as it always did over HTTP.
  - Masked failures are logged under the category `Polhem.Api.Core.Dispatch.PolhemExceptionMapper` instead of the
    executor's, and the startup check logs under `Polhem.Hosting.ApiKeys.ApiKeyGateWarningService` instead of
    `Polhem.Api.AspNetCore`. A logging configuration that filtered the old categories must be updated.
- Polhem now releases against versions of packages from another repository. The version it depends on is set in
  `PolhemJsonRpc.props` at the repository root, which also describes the local switch for building against a
  checkout of that repository.

## Alternatives considered

- **Keep Polhem's own implementation.** No dependency on another repository. Rejected: the protocol half is not
  specific to Polhem, and an application that needs JSON-RPC without the rest of the framework could not use it.
- **Keep `Polhem.Api.AspNetCore` as a thin controller over the new endpoint.** No breaking change for hosts. Rejected
  for the reason in decision 3: two ways to host one API, kept in step by hand.
- **Release as 2.0.0.** Strictly correct under semantic versioning. Rejected for the same reason ADR-048 gives: the
  change for an application is a few lines in its host, which does not justify the signal of a major version.
