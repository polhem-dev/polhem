# Polhem Framework

[繁體中文](https://github.com/polhem-dev/polhem/blob/main/README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)

Polhem Framework is an **N-Tier + Clean Architecture + MVVM** hybrid designed to accelerate the development of enterprise information systems. It adopts a **Definition-Driven Architecture**, using `FormSchema` as the single source of truth to drive UI layout, database schema, and business validation in a unified way.

> 📌 *N-tier* means the architecture is divided into more than three logical layers. In Polhem, the system is separated into at least five layers: presentation, API communication, business logic, data access, and database — each with a clearly defined responsibility.

All packages target **`net10.0`**.

## ✨ Features

- **Definition-Driven Architecture**: `FormSchema` serves as the single source of truth, automatically deriving UI layout (`FormLayout`), database schema (`TableSchema`), and validation rules — define once, sync everywhere.
- **N-Tier + Clean Architecture + MVVM**: Clear separation of presentation, API, business logic (BO), and data access layers, borrowing the best concepts from each pattern for enterprise information systems.
- **Cross-platform compatibility**: All packages target `net10.0` for modern .NET runtime support.
- **Multi-database support**: Built-in dialects for SQL Server, PostgreSQL, SQLite, MySQL, and Oracle; host applications register only what they use.
- **Modular components**: Decoupled libraries for core utilities, data, caching, business logic, and API hosting.
- **Rapid development**: Reusable base classes and FormSchema-driven CRUD reduce repetitive boilerplate.
- **Conventions enforced at build time**: Roslyn analyzers ship with the packages and register automatically, turning framework conventions — database scope selection, cross-file definition consistency, wire contract shape — into build diagnostics that name both the cause and the fix. See [Analyzer Rules](https://github.com/polhem-dev/polhem/blob/main/docs/en/analyzer-rules.md).

## 📐 Architecture

For an in-depth look at the layered architecture, data flow, and design decisions behind Polhem, see the [Architecture Overview](https://github.com/polhem-dev/polhem/blob/main/docs/en/architecture-overview.md).

For guidelines on API Contract and BO Parameter design (Request/Response vs Args/Result), see the [API/BO Contract Design Principles](https://github.com/polhem-dev/polhem/blob/main/docs/en/api-bo-contract-design.md). The full catalog of public API methods, with each method's `[ApiAccessControl]` settings, lives in the [API Method Reference](https://github.com/polhem-dev/polhem/blob/main/docs/en/api-method-reference.md).

For calling the JSON-RPC API from a JavaScript / TypeScript frontend (React, Vue, Angular, vanilla — no .NET on the client), see the [JSON-RPC Frontend Integration Guide](https://github.com/polhem-dev/polhem/blob/main/docs/en/jsonrpc-frontend-integration.md).

For the full developer documentation index, see [docs/en/README.md](https://github.com/polhem-dev/polhem/blob/main/docs/en/README.md).

## 📦 Assembly

Each assembly below ships as a NuGet package of the same name. A JSON-RPC server starts with two of them:

```bash
dotnet add package Polhem.Api.AspNetCore
dotnet add package Polhem.Db
```

[Getting Started](https://github.com/polhem-dev/polhem/blob/main/docs/en/getting-started.md) continues from there.

### Shared (Frontend / Backend)

| Assembly Name | Description |
|---|---|
| **Polhem.Base.dll** | Core utilities such as serialization, encryption, and general-purpose helpers. |
| **Polhem.Definition.dll** | Defines system-wide structured types including FormSchema, field schemas, and layout configurations. |
| **Polhem.Expressions.dll** | Portable, sandboxed expression evaluator (DynamicExpresso-backed) for computed fields and validation rules; shared by backend save and Avalonia client live preview so both sides compute identically. |
| **Polhem.Api.Contracts.dll** | Shared data contracts (request/response models) used by both frontend and backend. |
| **Polhem.Api.Core.dll** | Encapsulates API support such as model definitions, payload encryption, and serialization pipeline. |

### Backend

| Assembly Name | Description |
|---|---|
| **Polhem.Repository.Abstractions.dll** | Interface contracts for the business layer to access the data layer; boundary between Business Object and Repository. |
| **Polhem.ObjectCaching.dll** | Runtime caching of FormSchema definitions and derived system data to improve performance. |
| **Polhem.Db.dll** | Database abstraction with dynamic SQL command generation and connection binding; ships dialects for SQL Server, PostgreSQL, SQLite, MySQL, and Oracle. |
| **Polhem.Repository.dll** | Common repository base classes and FormSchema-driven data access mechanisms. |
| **Polhem.Business.dll** | Core business logic (Business Object / BO) implementing use-case workflows. |
| **Polhem.Hosting.dll** | Composition root — `AddPolhemFramework` extension registering all backend services into any `IServiceCollection` (no ASP.NET Core dependency). Used by ASP.NET Core, WinForms, Console, and Worker Service hosts. |
| **Polhem.Api.AspNetCore.dll** | JSON-RPC 2.0 API controller for ASP.NET Core (`ApiServiceController`), plus `UsePolhemFramework` for the host's startup checks. |

### Frontend

| Assembly Name | Description |
|---|---|
| **Polhem.Api.Client.dll** | Connector for local or remote invocation of backend Business Objects (`LocalApiProvider` / `RemoteApiProvider`). |
| **Polhem.UI.Core.dll** | Cross-platform UI common layer (`ClientInfo` / `IEndpointStorage` / `FileEndpointStorage` / `IUIViewService`); shared by native UI hosts for client-side connection state and endpoint persistence. |
| **Polhem.UI.Avalonia.dll** | Avalonia control library for desktop (Windows / macOS / Linux), browser (WebAssembly), iOS and Android heads; ships FormSchema-driven controls (`FormView` / `ListView` / `GridControl` plus a field-editor family with `FormScope` ambient binding, all backed by `FormDataObject`). Single `net10.0` TFM; Avalonia 12.0.0 + DataGrid 12.0.0 as lower bound. |
| **Polhem.Web.Blazor.Server.dll** | Razor Class Library (RCL) for Blazor Server hosts; provides DI-scoped connectors and Blazor components (`DynamicForm`, `FormDataObject`). |

### Tooling (dotnet tool)

| Package | Install | Description |
|---|---|---|
| **Polhem.Cli** | `dotnet tool install -g Polhem.Cli` <br/>Upgrade: `dotnet tool update -g Polhem.Cli` | Framework CLI invoked as `dotnet polhem`. The `defines` commands materialise and list the framework default define files embedded in `Polhem.Definition.dll` (to bootstrap a new consumer's `DefinePath`) and split a menu out of an old `ProgramSettings.xml`; the `keys` commands generate protected keys for `SystemSettings.xml`. See the [Polhem.Cli README](https://github.com/polhem-dev/polhem/blob/main/tools/Polhem.Cli/README.md); `dotnet polhem --help` lists the options. |


## 🚀 Quick Start

Want to see Polhem running in 30 seconds?

```bash
# Terminal 1 — start the JSON-RPC API host
cd samples/QuickStart.Server
dotnet run

# Terminal 2 — connect and call the Echo BO
cd samples/QuickStart.Console
dotnet run
```

The console will print `System.Ping` status and an echoed message returned from a custom BO. See [`samples/README.md`](https://github.com/polhem-dev/polhem/blob/main/samples/README.md) for the full demo list and what each one shows.

Ready to build your own? [Getting Started](https://github.com/polhem-dev/polhem/blob/main/docs/en/getting-started.md) walks through the same thing from an empty folder — packages, `DefinePath`, DI wiring, your first business object, and calling it from a client.

## 🌟 Featured demo — Polhem.Northwind

[`apps/Polhem.Northwind`](https://github.com/polhem-dev/polhem/blob/main/apps/Polhem.Northwind/README.md) is the flagship demo: the classic Northwind inventory case built almost entirely from definitions (master files, master-detail orders with lookups, exactly one hand-written business object — everything else is XML). The same shared `Polhem.Northwind.UI` runs on **four Avalonia heads** — Desktop, Browser (WASM), iOS, and Android — against one JSON-RPC server.

The same Order form rendered by each head — same definitions, same controls, only the platform shell differs:

| Desktop | Browser (WASM) |
|---|---|
| <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/desktop-order-detail.png" alt="Desktop — order detail" width="420"> | <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/browser-order-detail.png" alt="Browser — order detail" width="420"> |

| iOS | Android |
|---|---|
| <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/ios-order-detail.png" alt="iOS — order detail" width="200"> | <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/android-order-detail.png" alt="Android — order detail" width="200"> |

More screens, the form catalog, and how to run it: [`apps/Polhem.Northwind/README.md`](https://github.com/polhem-dev/polhem/blob/main/apps/Polhem.Northwind/README.md).

## 💡 Sample Projects

All demos live in-repo under [`samples/`](https://github.com/polhem-dev/polhem/blob/main/samples/README.md). They're minimal, focused, and evolve alongside the framework. Build them with `dotnet build samples/Polhem.Samples.slnx` (kept separate from the main `Polhem.slnx`, so the main CI/build stays unaffected).

| Category | Demo | Shows |
|----------|------|-------|
| QuickStart | [`QuickStart.Server`](https://github.com/polhem-dev/polhem/blob/main/samples/QuickStart.Server/README.md) + [`QuickStart.Console`](https://github.com/polhem-dev/polhem/blob/main/samples/QuickStart.Console/README.md) | Minimal JSON-RPC end-to-end with a custom anonymous BO |
| Blazor Server | [`Blazor.Server.Demo`](https://github.com/polhem-dev/polhem/blob/main/samples/Blazor.Server.Demo/README.md) | `PolhemLoginPanel` + `FormPage` + Staff CRUD, dispatched in-process via `LocalApiProvider` |
| Avalonia | [`Avalonia.DemoCenter`](https://github.com/polhem-dev/polhem/blob/main/samples/Avalonia.DemoCenter/README.md) | Theme-oriented control demo center (DevExpress-style): nav tree (theme → case) + Demo/Source tabs + theme/FormMode toolbar; covers data binding, read-only/required, FormMode, layout, grid, native-vs-inherited parity (Semi.Avalonia, no backend) |
| Pure JS | [`Web.Js.Demo`](https://github.com/polhem-dev/polhem/blob/main/samples/Web.Js.Demo/README.md) | Calling the JSON-RPC API from vanilla JavaScript in a browser — no .NET on the client, no npm |


## Migrating from Bee.NET

Polhem continues the [Bee.NET](https://github.com/jeff377/bee-library) framework (`Bee.*` packages, last released as 4.33.0) under a new name. Polhem 1.0.0 is Bee.NET 4.33.0
renamed, plus the changes of a pre-release review that the
[CHANGELOG](https://github.com/polhem-dev/polhem/blob/main/CHANGELOG.md) lists. Every name that contained `Bee` was
renamed, and the old names are not recognized: there is no compatibility layer. This section covers what an upgrade has
to change.

### Packages, namespaces and types

- Every `Bee.<Name>` package becomes `Polhem.<Name>`, and the split into packages is unchanged: `Bee.Hosting` becomes
  `Polhem.Hosting`, and so on for each package in the tables above. Namespaces follow the same pattern:
  `Bee.Definition.Forms` becomes `Polhem.Definition.Forms`.
- `Bee` in a type or member name becomes `Polhem`: `AddBeeFramework` becomes `AddPolhemFramework`, `UseBeeFramework`
  becomes `UsePolhemFramework`, `BeeLoginPanel` becomes `PolhemLoginPanel`.
- The command-line tool `Bee.Cli` (`dotnet bee`) becomes `Polhem.Cli` (`dotnet polhem`). Uninstall the old tool and
  install the new one with `dotnet tool install -g Polhem.Cli`.

The review also renamed, moved or removed public types. These are the ones a Bee.NET application is most likely to use:

| Bee.NET | Polhem |
|---------|--------|
| `IBeeContext`, `BeeContext` | `IBusinessObjectContext`, `BusinessObjectContext` |
| `BeeStringLocalizer<T>` | `LanguageResourceStringLocalizer<T>` |
| `LogBusinessObject`, `LogListResult`, `LogAggregateResult`, `LogApiConnector`, `LogActions`, `LogListResponse`, `LogAggregateResponse` | `AuditLogBusinessObject`, `AuditLogListResult`, `AuditLogAggregateResult`, `AuditLogApiConnector`, `AuditLogActions`, `AuditLogListResponse`, `AuditLogAggregateResponse` |
| `PermissionAction` | `PermissionActions` |
| `NullAuditLogWriter` | `NullLogWriter` |
| `UserID` (`SessionUser`, `CreateSessionArgs`) | `UserId` |
| `AuditEntry.AccessToken` | `AuditEntry.TokenFingerprint` |
| `ApiClientInfo.ApiEncryptionKey`, `ApiClientInfo.UserTimeZoneId` | The same members on `ApiSessionContext` |
| `ApiClientInfo.LocalServiceProvider` | Pass the `IServiceProvider` to `LocalApiProvider` or to the local connector constructor |
| `Bee.UI.Avalonia.Storage.FileEndpointStorage` | `Polhem.UI.Core.FileEndpointStorage` |
| `ElementCapabilityResolver`, `IElementCapabilityResolver`, `FieldCapability` in `Bee.UI.Core.Permissions` | The same types in `Polhem.Api.Client.Permissions` |
| `DeploymentAuthorizationService`, `EmployeeContextResolver` in `Bee.ObjectCaching.Services` | `Polhem.Business.Security.DeploymentAuthorizationService`, `Polhem.Business.Session.EmployeeContextResolver` |
| `JsonRpcExecutor.Execute` | `JsonRpcExecutor.ExecuteAsync` |
| `BusinessObject.SessionInfo` | `SessionInfoService.Get(AccessToken)` inside the business object |
| `Bee.Base.Tracing` | Removed |

Classes that are not extension points are sealed, the framework repository implementations are internal (use the
`I*Repository` interfaces), and public async client members take a trailing `CancellationToken`. The CHANGELOG lists
every change.

The compiler reports every place in your code that still uses these names.

### Names the compiler does not check

These are strings. A build with the old names succeeds, and the problem only shows at run time or not at all.

| What | Bee.NET | Polhem | With the old name |
|------|---------|--------|-------------------|
| Type names in definition files: `BusinessObject` and `Repository` in `ProgramSettings.xml`, the elements under `BackendConfiguration/Components` in `SystemSettings.xml`, and type names in your own code | `Bee.Business.AuditLog.LogBusinessObject, Bee.Business` | `Polhem.Business.AuditLog.AuditLogBusinessObject, Polhem.Business` | The type is not found at run time; the error says that the name looks like a Bee.NET name |
| Default environment variable of the master key | `BEE_MASTER_KEY` | `POLHEM_MASTER_KEY` | Only a `SystemSettings.xml` that leaves the variable name to the default is affected, and the error mentions `BEE_MASTER_KEY` when that variable is set. A `MasterKeySource` whose `Value` names the variable keeps using that name; the defaults that `dotnet bee defines materialize` wrote name `BEE_MASTER_KEY` |
| Analyzer diagnostic IDs in `.editorconfig`, `#pragma warning`, `NoWarn` and `[SuppressMessage]` | `BEE1001` | `POLHEM1001` (same numbers) | The setting is silently ignored |
| MSBuild properties for definition file checks | `BeeDefinitionFilesGlob`, `BeeRequireDefinitionFiles`, `BeeAnalyzeDefinitionFiles` | `PolhemDefinitionFilesGlob`, `PolhemRequireDefinitionFiles`, `PolhemAnalyzeDefinitionFiles` | The setting is silently ignored and the default applies |
| CSS classes of the Blazor components | `bee-dynamic-form`, `bee-dynamic-grid`, `bee-form-page`, `bee-login-panel` | `polhem-dynamic-form`, `polhem-dynamic-grid`, `polhem-form-page`, `polhem-login-panel` | Your own style rules no longer apply |
| Logging categories, such as filters under `Logging:LogLevel` | `Bee.Api.AspNetCore` | `Polhem.Api.AspNetCore` | The filter no longer matches |
| The `Exception.Data` key of `SerializationErrorData.FilePath` | `Bee.FilePath` | `Polhem.FilePath` | Code that reads the key finds nothing |

To find them, run this in the root of your repository:

```bash
grep -rnE "Bee\.|Bee[A-Z]|BEE_|BEE[0-9]{4}|dotnet[- ]bee|bee-(dynamic|form|login)" --include="*.cs" --include="*.razor" --include="*.css" --include="*.xml" --include="*.json" --include="*.csproj" --include="*.props" --include="*.targets" --include=".editorconfig" --include="*.yml" --include="*.yaml" --include="*.sh" --include="*.js" --include="*.ts" --include="Dockerfile" .
```

### Settings

- **Components**: an entry under `BackendConfiguration/Components` in `SystemSettings.xml` may be left blank, which
  selects the framework default. Clear the entries that only repeat a Bee.NET default instead of renaming them.
- **Default language**: `CommonConfiguration/DefaultLanguage` (default `zh-TW`) is the culture of users who have no
  `st_user.culture` of their own and the last language fall-back. It replaces `CommonConfiguration/DefaultLang` and
  `BackendConfiguration/DefaultLanguage`, which are no longer read.

### What changes when you switch

- **Signed-in users sign in again.** `st_session` now stores a hash of each access token, and the labels that derive
  session keys were renamed (`bee-api-*` to `polhem-api-*`), so no session created by Bee.NET works with Polhem. The
  rows Bee.NET left in `st_session` hold its tokens and match nothing any more; delete them.
- **Passwords in the old format need a reset.** A `st_user.password` value that does not start with `v2.` is a
  PBKDF2-SHA1 hash, which no longer verifies. Hashes that start with `v2.` keep working and are rehashed with more
  iterations at the next successful sign-in.
- **The log tables drop the access token.** `st_log_access`, `st_log_anomaly_api`, `st_log_change` and `st_log_login`
  record a `token_fingerprint` instead of `access_token`. The schema upgrade adds the new column but never drops a
  column, so the old `access_token` column stays with the tokens Bee.NET wrote. Clear it or drop it yourself.
- **Clients and the server are upgraded together.** Payloads carry type names such as
  `Polhem.Definition.Collections.Parameter, Polhem.Definition`, and the server only accepts types from allowed
  namespaces. A Bee.NET client sends `Bee.*` names, which a Polhem server rejects, and the other way around.
- **Anonymous and unauthenticated calls answer differently.** A request without an `Authorization` header is an
  anonymous call instead of an HTTP 401, and a method that needs a session answers the JSON-RPC error `-32001`. A
  client that checked for HTTP 401 checks the error code instead. `CreateSession` accepts only local calls, and
  `CreateApiKey`, `SetApiKeyEnabled` and `SetApiKeyExpiry` need a frame sequence when replay protection is on.
- **`GetList` without paging returns one page**, capped at `PagingOptions.MaxPageSize`.
- **Built-in text is English, with `zh-TW` translations.** Captions, UI text and messages follow the user's culture,
  which the login response now carries: `UserInfo.Culture` is empty until sign-in instead of `zh-TW`. The labels of
  `PolhemLoginPanel` and `DynamicGrid.EmptyText` are `string?`, and `null` shows the localized text.
- **`CBool` no longer reads Chinese words as true.** Only `1`, `T`, `TRUE`, `Y` and `YES` (ignoring case) are true.
- **Clients keep their endpoint in a new place.** The endpoint and the API key are stored in `endpoint.txt` and
  `apikey.txt` under the per-user local application data folder, in a subfolder named after the application
  (`FileEndpointStorage`). The `{ExeName}.Settings.xml` file beside the assembly is not read, so users enter the endpoint
  and the API key again.
- **Audit records written by Bee.NET keep their old marker.** The `changes_xml` column of `st_log_change` records the
  declared field type of each column as `msprop:Bee.FieldDbType`, and Polhem reads `Polhem.FieldDbType`. The values of
  an old record read the same; only `GetDeclaredFieldDbType()` returns `null` for its columns.
- **DefineEditor starts with fresh settings.** Its settings folder under the user's application data folder is now
  `Polhem.DefineEditor` instead of `Bee.DefineEditor`. Copy the old folder to keep recent files and preferences.
- **Table names are unchanged.** The framework tables keep their `st_` names. Apart from the log column above, no
  schema change needs a data migration.

## Design decisions

The reasons behind the design are recorded in the
[architecture decision records](https://github.com/polhem-dev/polhem/blob/main/docs/adr/README.md).

## Contributing

See [CONTRIBUTING.md](https://github.com/polhem-dev/polhem/blob/main/CONTRIBUTING.md).

## License

[MIT](https://github.com/polhem-dev/polhem/blob/main/LICENSE.txt). Copyright (c) Polhem contributors.

## 📬 Contact & Follow

Polhem is maintained by the [polhem-dev](https://github.com/polhem-dev) organisation on GitHub.

- Questions, ideas and show-and-tell: [GitHub Discussions](https://github.com/polhem-dev/polhem/discussions)
- Bug reports and feature requests: [GitHub Issues](https://github.com/polhem-dev/polhem/issues)
