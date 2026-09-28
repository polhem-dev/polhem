# ADR-013: Front-end API connection strategy — separate `Polhem.UI.*` and `Polhem.Web.*` families

[繁體中文](adr-013-frontend-api-connection-strategy.zh-TW.md)

## Status

Accepted (2026-05-22)

## Context

At the v4.4 stage Polhem had three kinds of front-end host at the same time:

| Front-end kind | Representative packages | Deployment / runtime environment |
|---------|---------|----------------|
| **Desktop / native UI** | `Polhem.UI.Core` (shared), `Polhem.UI.Avalonia`, the then `Bee.UI.Maui` (since removed), `Polhem.UI.WinForms` (future, separate repository) | iOS / Android / macOS / Windows / Linux / desktop OS native |
| **Blazor Server** | `Polhem.Web.Blazor.Server` | ASP.NET Core server-rendered, with SignalR circuit |
| **Blazor WASM** | the then `Bee.Web.Blazor.Wasm` (since removed) | Browser sandbox (WebAssembly) |

These three kinds of front end **have structurally different needs for "how to obtain / persist the API connection
state"**:

| Dimension | Desktop | Blazor Server | Blazor WASM |
|------|--------|---------------|-------------|
| Where connection information is kept | Local file (`{ExeName}.Settings.xml`) | Server-side DI scope / circuit state | Browser memory / localStorage |
| Endpoint setup flow | Read the file at startup → if unreachable, pop up a dialog for the user to enter it | Injected at host startup or read from appsettings | Injected at host startup or read through JS interop |
| Token management | **single-user** static singleton (`ClientInfo._accessToken` is a `private static Guid`) | **multi-user per circuit**: each SignalR circuit has its own token | **multi-user per app instance**: each browser tab / WASM heap has its own token |
| Users per token holder | 1 (one process = one user) | N (one server process serves several SignalR connections at once) | 1 per browser instance, but the same server faces N tabs |
| UI interaction needs | Needs a dialog service (`IUIViewService.ShowApiConnectAsync()`) | Goes through the Razor component flow, with no dialog abstraction | Same as Server |
| Connection mode | Local or Remote (both modes possible) | Local (in-process) or Remote (HTTP) | **Remote (HTTP) only** — the browser cannot load backend assemblies |

Forcing a **single connection abstraction** to cover all three kinds of front end produces structural contradictions:

1. **The `IUIViewService.ShowApiConnectAsync()` dialog abstraction the desktop needs has no counterpart in a Blazor
   environment** (the Razor component model is completely different), so the abstraction would become an empty shell
   or carry misplaced semantics
2. **`ClientInfo` keeping state in a static singleton fits the desktop, but on the web it is a cross-user security
   bug**:
   - `Polhem.UI.Core.ClientInfo._accessToken` is a `private static Guid` — one process **can hold only one user's
     AccessToken**
   - On the desktop this is fine (one app process = one logged-in user)
   - For Blazor Server it is **completely wrong**: the same ASP.NET Core process serves N SignalR circuit connections
     at once, with N users working in parallel. If they all read and write `ClientInfo.AccessToken`, **a user who logs
     in later overwrites the earlier token**, and every later API call of the earlier user goes out with the wrong
     identity — not just "the state is wrong" but a serious cross-user data leak
   - Although in WASM each browser tab has its own heap (N tabs = N WASM instances), static state is still out of step
     with Blazor's DI scope / component lifecycle and hard to maintain
3. **The desktop's file IO persistence** (`{ExeName}.Settings.xml`) is **not available at all** inside the browser
   WASM sandbox; and if several users in Blazor Server shared the same file, writes would race

Historically, before v4.3, `Polhem.UI.Core` was designed with only the desktop in mind, so `ClientInfo` naturally used
a static singleton. When the Blazor RCL was added in v4.4, forcing Blazor through `Polhem.UI.Core` would have hit the
problems above.

## Decision

Adopt **two separate families**:

### Family A: `Polhem.UI.*` (consumes the `Polhem.UI.Core` abstractions)

- **What it consumes**: the `ClientInfo` static singleton, `IEndpointStorage`, `IUIViewService`, `VersionInfo`
- **Applicable front ends**: desktop / native UI (MAUI, WinForms, WPF, Avalonia and so on)
- **Connection model**:
  - `ClientInfo.InitializeAsync(uiService, supportedConnectTypes)` is called at app startup
  - `ClientInfo.SetEndpointAsync(endpoint)` sets the endpoint (a Local path or a Remote URL) and internally calls
    `SystemApiConnector.InitializeAsync()`
  - `ClientInfo.ApplyLoginResult(loginResponse)` applies the login result
  - Connectors are obtained through `ClientInfo.SystemApiConnector` / `ClientInfo.CreateFormApiConnector(progId)`
  - Persistence goes through `IEndpointStorage` (default implementation: a file); the UI dialog flow is provided by
    `IUIViewService`
- **Current members** (for the current state see Implementation evolution at the end):
  - `Polhem.UI.Core` (shared)
  - `Polhem.UI.Avalonia` (desktop — Windows / macOS / Linux, Avalonia 12.x; mobile iOS / Android is covered by it
    too. For the DataGrid binding strategy see [ADR-020](adr-020-avalonia-datagrid-binding-strategy.md))
  - Future: `Polhem.UI.WinForms`, `Polhem.UI.Wpf` and the like follow the same pattern

### Family B: `Polhem.Web.*` (a separate family that does **not** consume `Polhem.UI.Core`)

- **Does not consume `Polhem.UI.Core`**: a Blazor environment has no concept of file IO or a dialog service, so the
  shared abstractions have no counterpart
- **Applicable front ends**: Blazor Server, Blazor WASM, and other web frameworks in the future
  (`Polhem.Web.React.*` and so on)
- **Connection model**:
  - `IJsonRpcProvider` is set up through the host's `IServiceCollection.AddPolhemFramework(...)` or custom DI
    configuration
  - `LocalApiProvider` (in-process, optional for Blazor Server) or `RemoteApiProvider` (HTTP, mandatory for WASM)
  - `SystemApiConnector` / `FormApiConnector` are injected into Razor components from the DI scope
  - State management is handled by components / `CascadingValue` / Razor scoped services
  - **WASM must never depend on any backend assembly** (Repository / Business / Hosting and so on); this is enforced
    by the dependency chain
- **Current members**: `Polhem.Web.Blazor.Server` (for the current state see Implementation evolution at the end)

### Criterion for choosing the family (apply it when adding a package)

> **Does it consume the `Polhem.UI.Core` abstractions (`ClientInfo` / `IEndpointStorage` / `IUIViewService` and so
> on)?**
>
> - **It consumes them** → it belongs to the `Polhem.UI.*` family
> - **It does not, and has its own state management / dialog model** → it takes a separate family prefix (such as
>   `Polhem.Web.*`)

This criterion is "**matching reality**" rather than "**an ideal classification**": the `Polhem.UI.Core`
abstractions were designed for the desktop, and web / Blazor environments are structurally different, so **they
should not be forced onto them**.

## Consequences

### Positive

- **Desktop and web are each kept simple**: there is no shared abstraction that "compromises both sides"
- **WASM security is protected automatically**: since `Polhem.Web.*` does not depend on `Polhem.UI.Core`, it does not
  depend on any server-only assembly either
- **The family criterion is clear**: adding a new front-end package in the future does not need another round of
  debate
- **Independent evolution**: `Polhem.UI.Core` can be optimized for the desktop (for example making `ClientInfo`
  async) without affecting Blazor, and vice versa

### Negative

- **Looks like "duplication"**: both families have their own `SystemApiConnector` wrapping and connection state
  management, and a reader's first question is "why not share them?". This ADR is the document that answers that
  question
- **Cost of components shared across families**: if shared logic that "both families need" really appears in the
  future, it has to go into a lower layer (for example, `Polhem.Api.Client` is already the lowest layer shared by
  both families)
- **Naming burden for a new family**: if a front end that is "neither native UI nor web" appears in the future (such
  as a CLI tool or a background worker UI), a prefix has to be decided again (possibly `Polhem.Console.*` or the like)

### Neutral

- **`Polhem.Api.Client` is the lowest layer shared by both families**: it is outside the split and stays a pure
  communication / serialization / encryption layer that both families consume (Blazor consumes it directly;
  `Polhem.UI.Core` wraps it for the desktop to consume)

## Related

- Visualization of the dependencies: `docs/en/dependency-map.md`
- Working examples for each front end: `docs/en/development-cookbook.md` § "Frontend API Connection Patterns"
- Backend DI replaces the static Service Locator (affects how a Blazor host registers):
  [ADR-011](adr-011-di-replaces-service-locator.md)

## Out of scope

- **A future hybrid mode that "offers both ClientInfo and DI"**: not needed now; to be evaluated when a real use case
  appears
- **Moving the static state of `Polhem.UI.Core` itself to DI**: a refactoring inside the desktop family that does not
  affect the web family; left for a separate decision later
- **Blazor Hybrid (Blazor embedded in MAUI)**: may need to span both families; a new ADR will evaluate it at that time

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

### 2026-07-31: Member roster update

**The decision of this ADR is unchanged**: the split into two families, and the criterion "does it consume the
`Polhem.UI.Core` abstractions", still hold today. Only the roster has changed — on 2026-07-28 the UI converged on
**two tracks, Avalonia + Blazor.Server**, and the two packages of the time, `Bee.UI.Maui` and
`Bee.Web.Blazor.Wasm`, were removed (before the project was renamed Polhem):

| Original member | Current state | Reason |
|--------|------|------|
| `Bee.UI.Maui` | **Removed** | The `net10.0-ios` / `net10.0-android` heads of `Polhem.UI.Avalonia` already cover mobile, so a second native family is not needed |
| `Bee.Web.Blazor.Wasm` | **Removed** | Squeezed between Avalonia (offline / native experience) and Blazor Server (SEO, embedding in an existing website, screen readers, no runtime download), it has no range of use of its own |

So the current members of Family A are `Polhem.UI.Core` + `Polhem.UI.Avalonia`, and Family B is
`Polhem.Web.Blazor.Server`. The table of three front-end kinds in the "Context" section above describes the state at
v4.4 and is kept to show the context of the decision.

### 2026-09-27: Connection plumbing

- **Default endpoint storage.** `ClientInfo.EndpointStorage` and `ClientInfo.ApiKeyStorage` default to
  `FileEndpointStorage` (`src/Polhem.UI.Core/FileEndpointStorage.cs`), which keeps the endpoint and the API key in
  the per-user local application data folder instead of a file beside the assembly (read-only on iOS). A browser
  host, which has no persistent file system, replaces both. The static access-token field of `ClientInfo` is now
  named `s_accessToken`.
- **Local mode takes the host's service provider.** `LocalApiProvider` and the local connector constructors take an
  `IServiceProvider`; the process-wide `ApiClientInfo.LocalServiceProvider` was removed. A Blazor Server host passes
  its own container through `PolhemApiConnectorFactory`, and the native family keeps the provider on
  `ClientInfo.LocalServiceProvider`.
- **No ambient session in the Blazor connector factory.** The `PolhemApiConnectorFactory` constructor that fell back
  to process-wide session state was removed; the factory is scoped per circuit and always receives that circuit's
  `ApiSessionContext` (`src/Polhem.Web.Blazor.Server/DependencyInjection/PolhemApiConnectorFactory.cs`).
- **Shared logic moved down to `Polhem.Api.Client`.** The permission capability resolver
  (`ElementCapabilityResolver`, `src/Polhem.Api.Client/Permissions/`) moved there from `Polhem.UI.Core`, so both
  families can use it, as the Negative consequences above anticipated.
