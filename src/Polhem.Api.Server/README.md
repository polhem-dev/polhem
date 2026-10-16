# Polhem.Api.Server

> The server side of the Polhem JSON-RPC API: the dispatcher setup, the access and payload filters, API key and authorization checks, and the exception mapping.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: API Layer (server side). The wire layer it shares with the client is
  [`Polhem.Api.Core`](../Polhem.Api.Core/README.md); see
  [ADR-055](../../maintainers/adr/adr-055-api-core-split-from-server.md).
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md).
- An application usually does not reference this package directly: `Polhem.Hosting` references it, and
  `AddPolhemFramework` registers everything below.
- It reads internals of `Polhem.Api.Core`, so it depends on that package at the exact same version.

## Target Framework

- `net10.0`

## Key Features

### JSON-RPC Execution

- The protocol is [`Polhem.JsonRpc.Server`](https://github.com/polhem-dev/polhem-jsonrpc): its dispatcher parses
  `ProgId.Action` method identifiers and invokes the target method. Which methods an action name can reach is decided
  by `JsonRpcMethod.IsResolvableAction` there.
- `Dispatch/` plugs Polhem into it: `PolhemObjectFactory` checks the API key and the `Authorization` header of an HTTP
  call and obtains the business object from `IBusinessObjectFactory`; `PolhemAccessFilter` applies
  `[ApiAccessControl]`; `PolhemPayloadFilter` opens and writes the payload envelope through
  `Polhem.JsonRpc.Payload.Server`; `PolhemParameterBinder` binds the argument; `PolhemExceptionMapper` maps errors.
  `PolhemJsonRpc.CreateServerOptions` assembles them.
- Error responses -- a framework exception meant for the end user (`UserMessageException` and related types)
  reaches the caller with its message; other exceptions answer a fixed message per error code, and the real
  message is logged by `PolhemExceptionMapper`. The codes are the error contract in `Polhem.Api.Core`.
- `InProcessTransportFactory` -- the transport an in-process client (`PolhemApiClient.CreateLocal`) reaches the
  dispatcher through; it marks each call as local and carries the access token.

### Authorization & Access Control

- `IApiAuthorizationValidator` / `ApiAuthorizationValidator` -- validates authorization context for incoming requests.
  `AddPolhemFramework` registers the default; a host replaces it by registering its own.
- `ApiAuthorizationContext` / `ApiAuthorizationResult` -- authorization input and outcome types.
- `ApiAccessValidator` -- enforces method-level protection via `ApiAccessControlAttribute`.
- `ApiCallContext` -- per-call metadata (token, protection level, caller identity).

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `PolhemJsonRpc` | Creates the JSON-RPC server options the framework serves its API with |
| `IApiAuthorizationValidator` | Request authorization; replace it to change which calls need a key or a token |
| `ApiAccessValidator` | Method-level protection via `ApiAccessControlAttribute` |
| `InProcessTransportFactory` | The transport of an in-process client |

## Design Conventions

- **Reflection-based dispatch** -- the dispatcher resolves and invokes business object methods by name, decoupling the transport layer from concrete BO types.
- **Protection levels** -- `ApiAccessControlAttribute` declares a method's `ApiProtectionLevel` and `ApiAccessRequirement`; the members and their meaning are in the XML documentation of those enums (`Polhem.Definition.Security`).

## Directory Structure

- `Authorization/` -- `IApiAuthorizationValidator`, `ApiAuthorizationValidator`, `ApiAuthorizationContext`, `ApiAuthorizationResult`
- `Dispatch/` -- the components that plug Polhem into the `Polhem.JsonRpc.Server` dispatcher, and `InProcessTransportFactory`
- `Validator/` -- `ApiAccessValidator`, `ApiCallContext`
