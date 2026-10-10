# ADR-053: `PolhemApiClient` owns the connection and the signed-in identity

## Status

**Accepted (2026-10-10)**

A breaking change within 1.x under [ADR-052](adr-052-breaking-changes-in-1-x-minors.md). Amends
[ADR-013](adr-013-frontend-api-connection-strategy.md) (connection plumbing) and [ADR-046](adr-046-api-evolution-policies-for-1-0.md)
decision 3, which kept `ApiClientInfo` as process-wide static configuration.

## Context

Before this decision the state a .NET client calls with was spread over three owners:

- **`ApiClientInfo`**, a static class: the endpoint, the connection type, the API key, the payload options and the
  deployment's default language, one set per process.
- **`ApiSessionContext`**: the transmission key, the user's time zone and the replay sequence. A connector created
  without one shared `ApiSessionContext.Ambient`.
- **Each connector**: the access token and the transport (in-process or HTTP), both fixed when it was constructed.

Every caller that created a connector therefore chose between the in-process and the HTTP constructor itself, and
passed the token and the session along. That branch was written out in `ClientInfo`, in the Blazor connector factory
and in three load-test scenarios. The split also allowed two failures nothing reported clearly:

- **Token and key from different sign-ins.** The key lived in the shared session and the token in each connector. After
  a new sign-in, a connector still held by some caller sent the old token sealed with the new key, and the server
  refused it. `ClientInfo` avoided this only by discarding its cached connectors in the same lock as the token.
- **A forgotten session.** A multi-user host that created a connector without a session fell back to `Ambient`; the
  symptom was intermittent `ReplayRejected` on legitimate traffic, not an error naming the cause.

The API key and the payload options being process-wide also meant one process could not call two servers with
different keys. polhem-connector-js already had one object, `PolhemClient`, that owned its transport and handed out
connectors over it.

## Decision

### 1. One client owns the connection and the identity

`PolhemApiClient` (`src/Polhem.Api.Client/PolhemApiClient.cs`) is created with `CreateLocal` (in process, over the
backend's service provider) or `CreateRemote` (over HTTP, with the endpoint and the API key). It owns the payload
options, the deployment's default language and an `ApiSessionContext`, and hands out the connectors: `System` and
`AuditLog` as one instance each, `Form(progId)` as a new connector per call, since a connector holds no identity of
its own.

The choice between in process and HTTP is made once, when the client is created. Every connector has a single
constructor that takes the client.

### 2. The identity is one immutable snapshot, read once per call

`ApiSessionContext` holds an `ApiSessionCredentials`: the access token, the transmission key and the user's time
zone. `SignIn` and `SignOut` replace the whole instance. `SystemApiConnector.LoginAsync` signs the session in with all
three from the login response; `LogoutAsync` signs it out.

`ApiConnector.ExecuteAsync` reads the credentials once at the start of a call and uses that instance for the token it
sends, the key it seals and opens with and the zone it converts in. Because a sign-in replaces the instance rather
than its fields, a call cannot pair one sign-in's token with another's key. The transport of a call is created for
that call's token; `PolhemApiClientTests.ExecuteAsync_SignInDuringCall_KeepsTokenOfCallStart` pins it.

### 3. A connector follows its client's identity

A connector taken from a client before a new sign-in calls with the new identity afterwards. The identity belongs to
the client, and a host that serves several users from one process gives each user a client of its own, as the Blazor
Server package does with a scoped client per circuit.

### 4. What was removed

`ApiClientInfo`, `ApiSessionContext.Ambient`, the connector constructors that took an endpoint or a service provider
with a token, `ApiConnector.AccessToken`, `ApiConnector.Provider`, and the Blazor `PolhemApiConnectorFactory`. The
transports (`LocalApiProvider`, `RemoteApiProvider`) became internal: the client creates one per call. Where each
member of `ApiClientInfo` went is listed in the release notes. Desktop heads keep `ClientInfo`, which now holds one
client (`ClientInfo.ApiClient`) and keeps its own signatures.

## Consequences

- **Callers write less, and cannot pair the wrong token and key.** Signing in, entering a company and reading a form
  all go through one client, with no token to carry.
- **One process can call several servers**, each client with its own endpoint, key and identity.
- **A transport is created per call.** The HTTP transport is a new `HttpClient` over the shared handler, as a new
  connector was before; the in-process one is a small object. The load tests measure any difference.
- **Blazor component tests stand in for the backend at the transport.** With the factory gone, a test cannot hand a
  component a fake connector; it gives the component a client whose calls go to a fake server
  (`tests/Polhem.Web.Blazor.Server.UnitTests/FakeApiServer.cs`). The tests now exercise the real connector path.
- **Breaking for every caller that created connectors itself.** The migration steps are in the release notes.

## Alternatives considered

### Add the client next to the old constructors

Keep every existing constructor and `ApiClientInfo` until 2.0, and add the client as a second path. Rejected after
ADR-052: it would leave four legacy constructors on each connector and a compatibility shell that every reader has to
learn is not the way, for adopters who are not known to exist.

### Let connectors read a shared, mutable token

As polhem-connector-js does: the transport holds the token and the key as two fields, and every connector reads them.
Rejected for .NET: reading the two fields separately is exactly what allows one sign-in's token with another's key
when calls run concurrently with a sign-in, which a desktop UI thread and a Blazor circuit both do. The snapshot keeps
the simplicity of a shared owner without that window.

### Keep the Blazor factory as an override seam

The factory's virtual methods were the seam the component tests used. Rejected in favor of fewer types: the seam
existed for tests, and a fake server at the transport tests the same components through the real connectors.
