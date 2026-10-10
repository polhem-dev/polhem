# ADR-046: API evolution policies for 1.0: a synchronous server path, growable host interfaces, process-wide configuration

## Status

**Accepted (2026-09-27)**

The renaming of `Polhem.Base` to `Polhem.Core` in 1.1.0 is a one-time exception to the compatibility rule stated
below; see [ADR-048](adr-048-rename-base-to-core-in-1-1.md). The removal of `Polhem.Api.AspNetCore` and of `ApiServiceOptions`
(one of the static configuration classes this ADR keeps) in 1.2.0 is a second one, and [ADR-049](adr-049-jsonrpc-packages-in-1-2.md) also exempts the framework pipeline types it names from
that rule.

Decision 4, added on 2026-10-06, exempts the framework's exception types from that rule from then on.

[ADR-052](adr-052-breaking-changes-in-1-x-minors.md) replaces the compatibility rule for the rest of 1.x: a 1.x minor
version may break the public API. The decisions below stay, and ADR-052 says how they apply from 2.0.

## Context

Polhem 1.0 fixes the public API baseline. From then on, the `PublicAPI.Shipped.txt` file of each package records the
surface that consumers compile against, and a change that breaks it waits for the next major version.

A review of the public API before that baseline found three shapes that 1.0 would freeze as they are. Each could be
changed before the baseline, at a cost, or kept, provided the way it can still evolve is written down. Without a
written policy, each of them turns a later improvement into an argument about whether it is a breaking change:

- **The server path is synchronous from end to end.** The BO contracts (the CRUD virtuals of `FormBusinessObject`
  and `IFormBusinessObject`), the repository contracts (`IDataFormRepository` and the others in
  `src/Polhem.Repository.Abstractions/`) and the database calls they make (`DbAccess.Execute`) all block. `DbAccess`
  already has an asynchronous API (`src/Polhem.Db/DbAccess.Async.cs`), but no framework code outside `Polhem.Db`
  calls it. `JsonRpcExecutor` can await a BO method that returns a `Task`, but no framework BO method does.
- **The interfaces a host can replace are large, and they grow with the framework.** `IDefineAccess` and
  `IDefineStorage` have members for every definition type, so the next definition type needs new members on both.
  Under strict semantic versioning, every such addition breaks each host that implements the interface itself.
- **Configuration is held in process-wide statics.** Classes such as `ApiServiceOptions` and `SysInfo` are static,
  with public setters and `Initialize` methods. [ADR-011](adr-011-di-replaces-service-locator.md) already kept them
  when it replaced the static service locator with DI, on the grounds that they are written once per host. 1.0 would
  make every one of those setters permanent API.

## Decision

### 1. The server path stays synchronous in 1.0

The BO and repository contracts, and the server's database path below them, are synchronous in 1.0. This covers the
CRUD virtuals of `FormBusinessObject` (`GetList`, `GetLookup`, `GetNewData`, `GetData`, `Save`, `Delete` and the
`protected virtual` hooks they call), `IFormBusinessObject`, `IDataFormRepository` and the other repository contracts
in `src/Polhem.Repository.Abstractions/`.

Asynchronous counterparts can be added later as **new members next to the synchronous ones**:

- On a class such as `FormBusinessObject`, a new virtual member is an addition. Existing overrides keep compiling
  and keep running.
- On an interface such as `IDataFormRepository`, a new member falls under decision 2: it is allowed in a minor
  version, and an implementation derived from the framework's class receives it without change.

Adding them is therefore not a breaking change, and 1.0 does not have to settle the asynchronous shape now.

**The asynchronous `DbAccess` API stays public.** It is the cancellable path for application code that does its own
I/O, such as a report or a batch job implemented in a BO ([ADR-005](adr-005-formschema-driven.md) calls this track
AnyCode). The framework's own calls do not use it in 1.0.

**On the client side, every public asynchronous member of `Polhem.Api.Client` takes a trailing `CancellationToken`**
(with a `default` value) and passes it on to the transport. This lands in the same pre-1.0 batch as this decision. A cancelled token
stops the client from waiting, and it stops a call that has not been dispatched yet. It does not stop a BO method that
is already running: the server path below the dispatch takes no token, so a synchronous BO method runs to the end
even when the caller has gone away. `ClientAsyncSurfaceTests` in `tests/Polhem.Api.Client.UnitTests` enforces the
rule for that package by scanning every public and protected asynchronous member; no analyzer requires the parameter
elsewhere, and the UI packages' own asynchronous members are not covered by it.

### 2. Host-replaceable interfaces may gain members in minor versions

The interfaces below may gain members in a **minor** version. That is not treated as a breaking change, and the
release notes list each added member.

| Interface | Framework implementation |
|-----------|--------------------------|
| `IDefineAccess` | `CacheDefineAccess` |
| `IDefineStorage` | `FileDefineStorage`, `DbDefineStorage` |
| `ICustomizeDefineReader` | `CustomizeDefineReader`, `DbDefineStorage` |
| `ICustomizeDefineWriter` | `CustomizeDefineWriter`, `DbDefineStorage` |
| `ICacheDataSourceProvider` | `CacheDataSourceProvider` |
| `ICacheProvider` | `MemoryCacheProvider` |
| `IAccessTokenValidator` | `AccessTokenValidator` |
| `IApiEncryptionKeyProvider` | `DerivedApiEncryptionKeyProvider`, `DynamicApiEncryptionKeyProvider`, `StaticApiEncryptionKeyProvider` |
| `ISessionInfoService` | `SessionInfoService` |
| `ICompanyInfoService` | `CompanyInfoService` |
| `IRepositoryFactory` | `RepositoryFactory` |
| `IDataFormRepository` and the other repository contracts in `src/Polhem.Repository.Abstractions/` | The matching classes in `src/Polhem.Repository/` (`DataFormRepository` and the others) |

Most of them are selected by a type name in `BackendComponents` (`SystemSettings.xml`). The customization reader and
writer come with the definition storage, and the repository contracts are supplied through the repository factory or
the DI registration.

**A consumer tells whether an interface is covered by finding it in this table.** The table is the authority: an
interface that is not named here, and is not a repository contract in `src/Polhem.Repository.Abstractions/`, follows
ordinary semantic versioning, and adding a member to it waits for a major version. Extending the table is itself a
decision, recorded in an "Implementation evolution" section of this ADR.

**Implementers derive from the framework implementation** where it is not sealed, and override only what they
change. A member added in a minor version then arrives with the framework's implementation, and the host keeps
compiling. A host that implements one of these interfaces from scratch, or wraps a sealed implementation, accepts that
a minor upgrade can require it to implement new members. The type declaration says which implementations are sealed;
this ADR does not copy it.

### 3. Process-wide static configuration is kept in 1.0: one host per process

The static configuration classes stay as they are in 1.0: `ApiServiceOptions` (`Polhem.Api.Core`), `ApiClientInfo`
(`Polhem.Api.Client`), `SysInfo` (`Polhem.Base`), `CacheInfo` (`Polhem.ObjectCaching`), `ClientInfo`
(`Polhem.UI.Core`) and `GlobalEvents` (`Polhem.Definition`). Their members are in each type's declaration; this ADR
does not list them.

What this means:

- **The supported model is one host per process.** Two hosts in the same process with different settings, such as two
  backends with different payload options, or a client and an in-process server that disagree, are not supported.
  The last writer wins for the whole process.
- **Tests serialize on them.** A test class that writes one of these classes shares an xUnit `[Collection]` with every
  other class that writes the same one, so that parallel test classes do not race on it.
- **Per-session state does not belong here.** A head that serves several users in one process keeps each user's
  state in a scoped object, as the Blazor head does with `ApiSessionContext`, and not in these statics.

**Moving to DI options later is additive.** A later version can register options through `AddPolhemFramework` and
the head registration methods, and have the framework read them from DI. The statics would then be kept for
compatibility or marked `[Obsolete]`, and removed only in a major version. The move does not have to be decided now,
and 1.0 does not block it.

### 4. Exception types may change in minor versions

*Added 2026-10-06.*

The framework's exception types, every public class in `src/` that derives from `Exception`, may be added, removed,
renamed or changed in a **minor** version, members and constructors included. That is not treated as a breaking change,
needs no ADR of its own, and the release notes list each change.

The reason: an application meets these types in two ways, and neither ties it to a compiled surface the way an
interface or a connector method does. A business object throws one to send a message or a code to the caller, and a
client catches the type that `JsonRpcErrorContract` rebuilds from a code. What both rely on is the code on the wire and
the message it carries, not the type that produced it. Since the framework moved to the Polhem.JsonRpc packages, its
own exception types have also been the part of the surface that the move left stranded (`JsonRpcException` and
`MethodNotFoundException` stopped being thrown in 1.2.0), and keeping each one until 2.0 would preserve types whose
only role is to be caught for a failure that no longer happens.

What stays under ordinary semantic versioning:

- **The JSON-RPC error codes** (`JsonRpcErrorCode`) and the code each failure travels as. Clients, polhem-connector-js
  among them, compare against the numbers, so changing the code of a failure waits for a major version even when the
  exception type behind it changes.
- **Every other public type**, including a type that only uses an exception type, such as a method that declares it in
  its signature.

## Consequences

- **Known cost of decision 1: blocked threads.** Every database round trip on the ASP.NET Core path holds a
  thread-pool thread. Under bursty load, the pool grows at its injection rate once it passes its minimum, and requests
  queue behind it. This is the usual scalability ceiling of a server that blocks on I/O, and 1.0 accepts it.
  A deployment that hits it raises the thread pool's minimum or scales out.
- **Known cost of decision 1: the caller's thread in local mode.** `JsonRpcExecutor` never actually awaits when the BO
  method is synchronous, so the in-process `LocalApiProvider` runs the whole call, database round trip included, on
  the caller's thread. For a desktop head in local mode, that is the UI thread; for Blazor Server, it is the circuit's
  thread. At the time of this decision, `LocalApiProvider` does not move the call to another thread, so a head that
  must keep its UI thread free does that itself.
- **Cancellation is partial.** A client can stop waiting and can stop a call before dispatch; it cannot stop a
  database operation that a BO method has started. Asynchronous server members added later can take a token and close
  that gap without changing the client API, because the client members already take one.
- **Decision 2 changes what a minor version may contain.** A host that follows the recommendation (derive from the
  framework implementation) is not affected. A host that implements a listed interface from scratch reads the release
  notes on every minor upgrade.
- **The contract of `ICacheProvider` still carries file-based invalidation** (`CacheItemPolicy` can name files to
  watch), which a distributed cache cannot honor. Decision 2 does not fix that; it only means that a later change to
  the interface can be made in a minor version.
- **Decision 3 keeps the test cost.** Test classes that write the static configuration keep running serially in their
  collections, which [ADR-011](adr-011-di-replaces-service-locator.md) already recorded as the remaining cost of
  these statics.

## Alternatives considered

### Make the BO and repository contracts asynchronous before 1.0

Change the CRUD virtuals of `FormBusinessObject`, `IFormBusinessObject` and the repository contracts to return
`Task`, and route them onto the existing asynchronous `DbAccess` API. This removes the blocked threads and gives the
server real cancellation.

Not taken for 1.0:

- The effort is large. The change runs through every BO, every repository, the write pipeline, the audit trail and the
  permission checks, and every sample and application override must change with it, in the same release.
- A later addition is not a breaking change (decision 1 and decision 2), so taking the cost now buys the shape early,
  not the option.
- The cost it removes is a throughput ceiling under bursty load. The deployments this framework targets have not
  reached it, and raising the thread pool's minimum or scaling out covers it until they do.

The cost of adding the members later is also recorded: the BO surface then has a synchronous and an asynchronous
member for each operation, and a rule for which one the framework calls.

### Default interface methods, or abstract base classes, for the host interfaces

- **Default interface methods for every future member.** A new member would carry a body in the interface, so no
  implementation breaks. Not adopted as the policy: many new members have no sensible default (a new definition type
  needs real storage), and a default that throws only moves the break from compile time to run time. A particular
  new member may still use one where a real default exists.
- **New abstract base classes (such as a `DefineStorageBase`) for hosts to derive from.** This adds a second type
  next to each interface for the same job. The framework implementations already serve as base classes, so decision
  2 points implementers at them instead.

### Move the static configuration to DI options before 1.0

Register options through `AddPolhemFramework` and the head registration methods, and make the static classes
`internal` or bootstrap-only. This would allow several hosts per process and remove the serialized tests.

Not taken for 1.0: the change touches every host, head and sample, and the payload pipeline and cache would have to
receive their options through DI everywhere they are used. Deferring it costs nothing that cannot be recovered,
because the DI path can be added later next to the statics (decision 3).
