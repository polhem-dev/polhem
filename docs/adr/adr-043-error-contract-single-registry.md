# ADR-043: The error contract is expressed as a single registry that both ends consume from one declaration

[繁體中文](adr-043-error-contract-single-registry.zh-TW.md)

## Status

**Accepted (2026-09-02)**

## Context

When an API call fails, the server maps the exception to a JSON-RPC error code, and the caller maps the error code
back to an exception, so that callers can `catch` a type instead of comparing integers. The two directions are inverses
of each other, yet each was implemented as a hand-written chain of `if`s in **two assemblies**
(`JsonRpcExecutor.MapException` in `Polhem.Api.Core` and `ApiConnector.FinalizeResponse` in `Polhem.Api.Client`), and
**no mechanism kept the two in sync**.

The compiler does not look at this, and tests do not catch it either: if the server recognizes one more code and the
caller does not keep up, that code quietly falls into the caller's generic branch. The `catch` promised by the
exception type's own documentation is then never entered, while the program compiles and every existing test stays
green.

**This is not a hypothetical risk; it has already happened.** When [ADR-042](adr-042-api-replay-protection.md)
introduced `ReplayRejectedException` and error code `-32005`, the server threw it in four places and mapped the code,
but the caller was missing a whole rebuild branch. The type's documentation says the reason it exists is "to let
callers tell 'retrying will not help' from 'credentials were rejected'", yet callers received an
`InvalidOperationException`; that capability never really existed. The same review found three further cases of drift
in public documents (the error code table was missing `-32005`, the description of `-32001` did not match reality, and
the development constraints document wrongly stated that only one code is rebuilt).

The four defects share one shape: **they must change together, and nothing reminds you to change them together**.
By the `single-source` criterion, this is a structural problem, not a discipline problem, and "remember to change
both" does not work.

## Decision

### 1. The mapping is declared once, and both ends consume the same declaration

A new [`JsonRpcErrorContract`](../../src/Polhem.Api.Core/JsonRpc/JsonRpcErrorContract.cs) declares, in an ordered
table, "which exception type goes to which error code, and who rebuilds that code". The server looks up the outbound
code through `TryGetCode`, and the caller looks up the inbound type through `TryRebuild`.

**Adding a new kind of error is now one edit, not one edit in each of two assemblies.** The caller therefore does not
need to know any concrete exception type at all: `ApiConnector` no longer even needs a `using` for
`Polhem.Base.Exceptions`.

The registry lives in `Polhem.Api.Core`: `ReplayRejectedException` already lives in that assembly, the other exception
types are in `Polhem.Base` (below it), and `Polhem.Api.Client` depends on `Polhem.Api.Core`. It is the only place that
can see every participant.

Only `TryRebuild` is public API. The outbound direction currently has a single consumer, the executor, which is in the
same assembly as the registry, so it stays `internal`; widening it later is an addition, not a breaking change.

### 2. Rejected: "make these exceptions inherit `UserMessageException` directly"

The proposal was to change types such as `CompanyNotEnteredException` to inherit `UserMessageException`, on the idea
that this would be "easier to decide, and saves writing a pile of `if`s in the future". **It was rejected after
examination, for more than one reason, but the key one is that it does not save a single `if`**:

- Not one of the named branches in `MapException` can go: each type needs a **different** code, and `is Base` cannot
  tell them apart.
- Not one allowlist check goes away either: those four types **were never on the allowlist**; they are intercepted by
  the named branches placed before it.
- The caller goes `int → type`, and inheritance is of no use at all in that direction: an integer cannot be resolved to
  a subclass with `is`.

The cost, on the other hand, is real. What the documentation of the four types has in common is not "whether the
message can be shown" but **"what the caller should do"** (direct the user to pick a company, go back to picking a
company, degrade the UI, stop retrying); showing the message is only a small part of that, and using it as the axis of
classification picks the wrong dimension. The documentation of `CompanyNotEnteredException` even states explicitly
"it must not be shown to the user verbatim", which directly contradicts `UserMessageException`'s "the message is
meant to reach the user as-is".

The item that would hurt external users most: the catch order the framework documentation currently teaches starts
with `catch (UserMessageException)`. After the inheritance change, any application that adds a subclass `catch` after
it would fail to compile outright with **CS0160**; one that does not add it would compile without error, but would
start showing the users the messages above that "must not be shown verbatim".

**This section is recorded here because it is an idea that looks very natural.** If it is not written down, the next
person will ask again, and the fact that it "saves no `if` at all" only becomes visible after actually reading those
three places in the code.

### 3. Order sensitivity is not pretended away; it becomes a verifiable invariant

Type matching must be assignable rather than exact, otherwise `ArgumentNullException` cannot be classified under
`ArgumentException`. Assignable matching is inherently ordered: if a base type comes before one of its own subclasses,
it swallows that subclass's row, which then can never match.

The registry therefore **does not claim to be order-independent**. Instead it declares an invariant, **a derived type
must come before all of its base types**, which `ErrorContractDriftTests` verifies pair by pair, naming the two rows
that shadow each other when it is violated.

This point is worth spelling out: the order in the old implementation mattered just as much (the named branches had to
come before the allowlist), but it was kept correct by comments and memory. The difference is not whether there is an
order, but **whether anyone notices when the order is wrong**.

### 4. The fallbacks stay out of the registry

Two things are deliberately left where they are: whether the server reveals the original message when it falls
through to `InternalError` is an **information disclosure policy** that reads `SysInfo.IsDebugMode`; the caller's
generic branch `"API error: {code} - {message}"` is a **message format**. Neither is "a type mapped to a code", and
putting them into the registry would only make it carry things that do not belong to it.

## Consequences

- Adding an error type goes from two edits to one, and a missing half is named by the tests.
- The error contract becomes data that can be inspected, rather than control flow scattered across two assemblies.
- The public API surface gains one type and one method (`JsonRpcErrorContract.TryRebuild`).
- The registry is **ordered**; this is complexity the decision does not remove, and the invariant test carries the
  cost.
- Three members of `JsonRpcErrorCode` (`MethodNotFound`, `InvalidParams`, `Unauthorized`) have **zero producers in
  the whole repository**. `Unauthorized` is especially misleading: an authentication failure actually returns
  `InvalidRequest` plus HTTP 401. This decision does not settle whether they stay or go, but the tests now require
  every member to be classified, so they are no longer invisible.
- Bringing the transitional rows of `UserMessage` in line is still to do: the BCL exceptions
  `UnauthorizedAccessException`, `ArgumentException`, `InvalidOperationException`, `NotSupportedException` and
  `FormatException`, and the framework's own `JsonRpcException`. It is orthogonal to this decision: afterwards the
  number of branches on both ends stays the same, because those types were never in the part that caused the
  divergence.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: BCL exceptions no longer reach remote callers verbatim.** The BCL rows of the registry keep their
  error code but travel with a fixed message per row, and the real message is logged on the server
  (`JsonRpcExecutor.Logger`). Only the framework's own exception types carry their message to the caller; a throw site
  whose text is meant for the end user throws `UserMessageException`. `JsonRpcExecutorUserMessageExceptionTests`
  (`tests/Polhem.Api.Core.UnitTests/JsonRpc/`) pins both halves, and the rows are listed by name in
  `src/Polhem.Api.Core/JsonRpc/JsonRpcErrorContract.cs`.
- **2026-09-27: `Unauthorized` has a producer.** An authentication failure (a missing, invalid or expired access
  token) now answers `JsonRpcErrorCode.Unauthorized` (-32001) through `AuthenticationRequiredException`, which the
  registry lists ahead of the `UnauthorizedAccessException` row it derives from; the client restores it as an
  `UnauthorizedAccessException`. The Consequences' "authentication failure returns `InvalidRequest` plus HTTP 401" no
  longer holds, and a request without an `Authorization` header is treated as an anonymous call.
- **2026-09-27: `UserMessageException` can carry a message key and arguments**, resolved with the session culture
  when the message is localized (`src/Polhem.Base/Exceptions/UserMessageException.cs`).
- **2026-09-28: `MethodNotFound` and `InvalidParams` have producers.** An action name that resolves to no method throws
  the internal `MethodNotFoundException`, and a `Plain` body that cannot be read into the method's type throws the
  internal `InvalidParamsException` (both in `src/Polhem.Api.Core/JsonRpc/`). The registry maps them to -32601 and
  -32602 with a fixed message and rebuilds no type on the client.

