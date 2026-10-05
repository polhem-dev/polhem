# ADR-050: Polhem 1.3.0 moves to Polhem.JsonRpc 1.1.0, whose payload wire format is incompatible

## Status

**Accepted (2026-10-05)**

Amends decision 6 of [ADR-049](adr-049-jsonrpc-packages-in-1-2.md): a third exception to semantic versioning within
1.x, after [ADR-048](adr-048-rename-base-to-core-in-1-1.md) and ADR-049. Unlike those two, it leaves the compatibility
rule stated in the context of [ADR-046](adr-046-api-evolution-policies-for-1-0.md) intact: the
`PublicAPI.Shipped.txt` baselines only grow. The break is on the wire, and in the Polhem.JsonRpc packages Polhem
depends on.

## Context

Since 1.2.0, Polhem's JSON-RPC runs on the `Polhem.JsonRpc` packages, and the payload envelope, its encryption and its
replay frame on their optional payload packages (ADR-049). Polhem.JsonRpc 1.1.0 changes that payload in two ways that
every party to a call can see:

- **The HMAC of an Encrypted payload also covers the direction and the JSON-RPC method of the call.** Before, a
  captured encrypted request could be sent to another method of the same session, and a result sent back as a
  request; the binding closes both. The layout of the bytes does not change, but a payload written without the
  binding fails its HMAC on a reader that expects it, and the reverse. The design and the bytes that are bound are
  recorded in
  [ADR-003 of Polhem.JsonRpc](https://github.com/polhem-dev/polhem-jsonrpc/blob/main/maintainers/adr/adr-003-bind-method-into-payload-hmac.md)
  and are not repeated here.
- **A result is answered in the format of its request** (decision 6 of the same ADR), a `null` result included, and a
  reader refuses a result in another format. Otherwise a result could be swapped on the way for a plain one.

Polhem.JsonRpc offers no compatibility mode for either, because a reader that accepted both forms could be
downgraded to the unbound one. It also renumbers `JsonRpcTransportKind`, which breaks binary compatibility with code
compiled against `Polhem.JsonRpc.Server` 1.0: its dispatcher refuses to start when such an assembly is loaded. The
Polhem 1.2.0 assemblies are such code, so 1.2.0 cannot run on 1.1.0. Polhem has to move to it in a release of its
own, and the remote clients have to move with it.

The change is in Polhem's own code too: `ApiConnector` seals requests and opens results with the method
([#60](https://github.com/polhem-dev/polhem/pull/60)), and two adjustments the new packages call for are made in the
same release.

## Decision

### 1. Adopt the binding with no compatibility mode

Polhem 1.3.0 binds every Encrypted call to its method and its direction, and answers every result in its request's
format, as Polhem.JsonRpc 1.1.0 does. Nothing in Polhem accepts the 1.2.0 form, and no setting brings it back.

**The server and every remote client upgrade together**: .NET clients built on `Polhem.Api.Client`, and
[polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js) in its release that supports Polhem 1.3.0.
A mismatched pair, an old client against a new server or the reverse, fails every Encrypted call, and the server
answers each one with `-32603 Internal error`. The answer does not say which check failed, by design of the payload
packages. Plain and Encoded calls carry no HMAC, so a mismatched pair can look healthy until its first Encrypted call;
it is not a state to run in. The result format rule can fail across the pair as well, for example a `null` result
that an old server answers to an Encoded request as a plain `null`.

### 2. In-process calls get no replay scope

`PolhemPayloadPolicy.GetReplayScope` returns no scope for an in-process call, so a method that declares
`ReplayProtection = UniqueSequence` is guarded on remote calls only. An in-process call never crossed a network, so
there is nothing to replay. With a scope it would also be refused once the wire frame is required, because
Polhem.JsonRpc 1.1.0 refuses a Plain or Encoded call where unique sequence numbers are checked, and the local
provider sends Plain outside debug mode. Whether a call is local is still decided by the transport alone (ADR-049,
decision 1).

The other side of the same rule is visible on the wire: while the frame is required, a remote Plain or Encoded call
from a signed-in session to such a method is answered `-32602 Invalid params`. It used to be accepted without a
sequence check.

### 3. A call with no value to bind answers `-32602`

A request with no `params`, or with a payload envelope that has no `value` or a `null` one, is answered
`-32602 Invalid params` before the method runs. Before, the method ran with a `null` argument and failed with
whatever it threw (`-32099` for most framework methods, which check their argument, and `-32603` for the rest). This
is what the default binder of Polhem.JsonRpc answers, and Polhem's binder now agrees with it. A request with
`"params": null` is still refused by the dispatcher with `-32600`, as JSON-RPC 2.0 asks.

### 4. Depend on `[1.1.0, 2.0.0)`

The packages depend on Polhem.JsonRpc `[1.1.0, 2.0.0)` instead of `1.0.0` or later. The lower bound keeps NuGet from
resolving the 1.0 packages, which do not bind. The upper bound keeps a later major version of Polhem.JsonRpc out
until Polhem has been built and tested against it; the 1.0 range had no upper bound, which is how an application on
Polhem 1.2.0 that updates its transitive packages can end up with a Polhem.JsonRpc it cannot run on.

### 5. Released as 1.3.0, the third exception in 1.x

Semantic versioning asks for 2.0.0. This is released as 1.3.0, a third exception within 1.x, for these reasons:

- **Polhem's own public .NET API changes only by additions.** The `PublicAPI.Shipped.txt` baselines gain
  `DefinitionNotFoundException` and lose nothing. An application that calls Polhem through its connectors, its
  business objects and its hosting extensions compiles against 1.3.0 unchanged.
- **The break is on the wire, and a version number does not change who can talk to whom.** A client of 1.2.0 cannot
  call a server of 1.3.0 whether the server is called 1.3.0 or 2.0.0, and polhem-connector-js, which does not depend on
  the .NET packages at all, would need its own release either way. A major version would signal the break, but would
  not help an existing client interoperate. The signal is given instead where an upgrading reader looks: the
  changelog entry for 1.3.0 opens with it, and so does its detailed note.

The first reason does not cover everything. What an application may have to change because of Polhem.JsonRpc 1.1.0,
rather than because of Polhem's surface:

- A host that serves the API over HTTP references `Polhem.JsonRpc.AspNetCore` itself (ADR-049, decision 3), and has
  to raise that reference to 1.1.0. Version 1.0 of it was compiled against `Polhem.JsonRpc.Server` 1.0, so the
  dispatcher refuses to start next to it.
- Its own code compiled against `Polhem.JsonRpc.Server` 1.0, such as a filter added through `AddJsonRpcServer`, must
  be recompiled, or the dispatcher refuses to start and names the assembly.
- An `IPayloadEncryptor` of its own must implement the overloads that authenticate associated data; until then every
  Encrypted call through it fails.

None of this shows up in Polhem's baselines. The changelog says it, and points to Polhem.JsonRpc's changelog for the
rest of its own changes.

As in ADR-049, this is not a precedent: each further breaking change within 1.x needs an ADR of its own.

### 6. 1.2.0 is deprecated on nuget.org once 1.3.0 is published

Every `Polhem.*` package of 1.2.0 is marked deprecated on nuget.org, with the 1.3.0 package of the same name as the
alternate, after 1.3.0 is published. Polhem 1.2.0 accepts any later Polhem.JsonRpc, so an application that updates
its transitive packages gets one its dispatcher refuses to start on, and its clients cannot call a 1.3.0 server.
Deprecation keeps 1.2.0 restorable for anyone who pinned it, and warns everyone else.

## Consequences

- Upgrading is a coordinated step for a deployment that has remote clients: the server and every client move in the
  same window. A deployment whose clients are all in-process, or that never uses Encrypted, is not affected by the
  binding itself.
- Every Encrypted call carries its method into the HMAC. A client in another language that speaks the payload has to
  implement ADR-003 of Polhem.JsonRpc; the cross-language test vectors of that repository are the reference.
- A remote call to a `UniqueSequence` method while the frame is required needs a session key, because only Encrypted
  is accepted there. A session without a key, whose Encrypted calls the connector downgrades to Encoded, is refused.
- A client that relied on reading a `null` result as a plain `null` from an Encoded or Encrypted request now reads it
  from an envelope in the request's format. The .NET connector, and polhem-connector-js in its release that supports
  Polhem 1.3.0, do this for the caller.

## Alternatives considered

- **A compatibility switch that accepts unbound payloads.** It would let a deployment upgrade the server first and
  the clients later. Rejected: a server that accepts both forms accepts the unbound one from an attacker as well, so
  the switch would keep open exactly what the binding closes, for as long as anyone left it on. Polhem.JsonRpc
  rejected it for the same reason.
- **Release as 2.0.0.** Strictly correct under semantic versioning. Rejected for the reasons in decision 5: Polhem's
  public API only grows, and the wire break is the same whatever the version is called.
- **Stay on Polhem.JsonRpc 1.0.** No break. Rejected: 1.0 leaves the redirection and the reflection of encrypted
  calls open, which is the reason the binding exists.
