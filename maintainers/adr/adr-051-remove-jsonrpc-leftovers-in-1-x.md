# ADR-051: Remove the types the move to Polhem.JsonRpc left behind, within 1.x

## Status

**Accepted (2026-10-06)**

Amends [ADR-049](adr-049-jsonrpc-packages-in-1-2.md), which kept `Polhem.Api.Core.Messages.PayloadFormat`, and is a
fourth exception to semantic versioning within 1.x, after [ADR-048](adr-048-rename-base-to-core-in-1-1.md), ADR-049 and
[ADR-050](adr-050-jsonrpc-1-1-wire-break-in-1-3.md).

## Context

Since 1.2.0 the JSON-RPC protocol, the payload envelope and its pipeline come from the Polhem.JsonRpc packages
(ADR-049). Some of the types that served the old pipeline stayed in Polhem without a job:

- **`Polhem.Api.Core.Messages.PayloadFormat`** has the same members and values as `Polhem.JsonRpc.Payload.PayloadFormat`
  (`Plain = 0`, `Encoded = 1`, `Encrypted = 2`), which the package's `PayloadConnector` and `PayloadEnvelope` use.
  Polhem kept its own and converted it to the package's type at the two places where they meet
  (`ApiConnector.EffectiveFormat` and `PolhemAccessFilter`). ADR-049 kept it so that applications passing it to
  connectors would not break.
- **`Polhem.Core.Serialization.Gzip`** compressed payload bodies. The package's `GzipPayloadCompressor` has done that
  since 1.2.0, and `Gzip` had no caller left in the framework.
- **`MethodNotFoundException`** (internal) was never thrown after the move: the dispatcher of `Polhem.JsonRpc.Server`
  answers an unknown method on its own. Removing it is not a breaking change and needs no ADR; it is listed here so the
  clean-up is recorded in one place.

The connector method that takes a `PayloadFormat` is `ExecuteAsync`, which applications call for the actions of
their own business objects. Neither `Gzip` nor the format enum carries behavior that an application would lose: the
enum is the same set of values under another namespace, and `Gzip` is a thin wrapper of `GZipStream`.

## Decision

1. **Remove `Polhem.Api.Core.Messages.PayloadFormat`.** Every public member that used it (`ApiConnector.ExecuteAsync`,
   the `ExecuteAsync` of `FormApiConnector`, `SystemApiConnector` and `AuditLogApiConnector`, and `ApiCallContext`)
   takes `Polhem.JsonRpc.Payload.PayloadFormat` instead. Polhem then has one format type, the one on the wire.
2. **Remove `Polhem.Core.Serialization.Gzip`.**
3. **Remove `MethodNotFoundException`** and its row in `JsonRpcErrorContract`.
4. **The exceptions stay.** `JsonRpcException` is no longer thrown by the framework, but an application's business
   objects may throw it and the error contract still maps it; it is outside this decision.
   *Amended 2026-10-06:* decision 4 of [ADR-046](adr-046-api-evolution-policies-for-1-0.md) lets exception types change
   in a minor version, and `JsonRpcException` is removed under it in the same release.
5. **Released in 1.x**, like ADR-048, ADR-049 and ADR-050. A later breaking change within 1.x still needs an ADR of its
   own.

## Consequences

- **Source break, small and mechanical.** Code that names `PayloadFormat` with `using Polhem.Api.Core.Messages;`
  adds `using Polhem.JsonRpc.Payload;` (`Polhem.Api.Client` already references that package). The member names and
  values are the same, so nothing else changes. Code that imported both namespaces and named one type in full can
  drop the qualification.
- **Binary break.** An assembly compiled against an earlier 1.x that calls `ExecuteAsync`, or uses `Gzip`, fails with
  `MissingMethodException` or `TypeLoadException` until it is recompiled.
- **No wire change.** The format travels as the same number, and `wire-contracts/` and `wire-fixtures/` do not change.
- Code that used `Gzip` writes the two `GZipStream` calls itself. `Gzip.Decompress` refused output over 50 MB; a
  caller that relied on that limit keeps its own.
- The `PublicAPI.Shipped.txt` baselines of `Polhem.Core`, `Polhem.Api.Core` and `Polhem.Api.Client` lose entries, which
  ADR-050 had said would not happen in 1.x.
