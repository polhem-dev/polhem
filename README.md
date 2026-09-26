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
| **Polhem.Api.AspNetCore.dll** | JSON-RPC 2.0 API controller for ASP.NET Core (`UsePolhemFramework` middleware + `ApiServiceController`). |

### Frontend

| Assembly Name | Description |
|---|---|
| **Polhem.Api.Client.dll** | Connector for local or remote invocation of backend Business Objects (`LocalApiProvider` / `RemoteApiProvider`). |
| **Polhem.UI.Core.dll** | Cross-platform UI common layer (`ClientInfo` / `IEndpointStorage` / `IUIViewService` / `VersionInfo`); shared by native UI hosts for client-side connection state and endpoint persistence. |
| **Polhem.UI.Avalonia.dll** | Avalonia desktop control library (Windows / macOS / Linux); ships FormSchema-driven controls (`FormView` / `ListView` / `GridControl` plus a field-editor family with `FormScope` ambient binding, all backed by `FormDataObject`) plus a file-backed `FileEndpointStorage`. Single `net10.0` TFM; Avalonia 12.0.0 + DataGrid 12.0.0 as lower bound. |
| **Polhem.Web.Blazor.Server.dll** | Razor Class Library (RCL) for Blazor Server hosts; provides DI-scoped connectors and Blazor components (`DynamicForm`, `FormDataObject`). |

### Tooling (dotnet tool)

| Package | Install | Description |
|---|---|---|
| **Polhem.Cli** | `dotnet tool install -g Polhem.Cli` <br/>Upgrade: `dotnet tool update -g Polhem.Cli` | Framework CLI invoked as `dotnet polhem`. Currently ships the `defines` subcommand group for materialising / listing the framework default define files (`st_*` TableSchema, framework-shipped FormSchema / FormLayout / Language, SystemSettings / DatabaseSettings templates). Use to bootstrap a new consumer's `DefinePath` from the embedded resources in `Polhem.Definition.dll`. |


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

[`apps/Polhem.Northwind`](https://github.com/polhem-dev/polhem/blob/main/apps/Polhem.Northwind/README.md) is the flagship demo: the classic Northwind inventory case built almost entirely from definitions (eight forms, master-detail orders with lookups, exactly one hand-written business object — everything else is XML). The same shared `Polhem.Northwind.UI` runs on **four Avalonia heads** — Desktop, Browser (WASM), iOS, and Android — against one JSON-RPC server.

The same Order form rendered by each head — same definitions, same controls, only the platform shell differs:

| Desktop | Browser (WASM) |
|---|---|
| <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-desktop-order-detail.png" alt="Desktop — order detail" width="420"> | <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-browser-order-detail.png" alt="Browser — order detail" width="420"> |

| iOS | Android |
|---|---|
| <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-ios-order-detail.png" alt="iOS — order detail" width="200"> | <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-android-order-detail.png" alt="Android — order detail" width="200"> |

More screens, the form catalog, and how to run it: [`apps/Polhem.Northwind/README.md`](https://github.com/polhem-dev/polhem/blob/main/apps/Polhem.Northwind/README.md).

## 💡 Sample Projects

All demos live in-repo under [`samples/`](https://github.com/polhem-dev/polhem/blob/main/samples/README.md). They're minimal, focused, and evolve alongside the framework. Build them with `dotnet build samples/Polhem.Samples.slnx` (kept separate from the main `Polhem.slnx`, so the main CI/build stays unaffected).

| Category | Demo | Shows |
|----------|------|-------|
| QuickStart | [`QuickStart.Server`](https://github.com/polhem-dev/polhem/blob/main/samples/QuickStart.Server/README.md) + [`QuickStart.Console`](https://github.com/polhem-dev/polhem/blob/main/samples/QuickStart.Console/README.md) | Minimal JSON-RPC end-to-end with a custom anonymous BO |
| Blazor Server | [`Blazor.Server.Demo`](https://github.com/polhem-dev/polhem/blob/main/samples/Blazor.Server.Demo/README.md) | `PolhemLoginPanel` + `FormPage` + Employee CRUD, dispatched in-process via `LocalApiProvider` |
| Avalonia | [`Avalonia.DemoCenter`](https://github.com/polhem-dev/polhem/blob/main/samples/Avalonia.DemoCenter/README.md) | Theme-oriented control demo center (DevExpress-style): nav tree (theme → case) + Demo/Source tabs + theme/FormMode toolbar; covers data binding, read-only/required, FormMode, layout, grid, native-vs-inherited parity (Semi.Avalonia, no backend) |
| Pure JS | [`Web.Js.Demo`](https://github.com/polhem-dev/polhem/blob/main/samples/Web.Js.Demo/README.md) | Calling the JSON-RPC API from vanilla JavaScript in a browser — no .NET on the client, no npm |


## Migrating from Bee.NET

Polhem continues the [Bee.NET](https://github.com/jeff377/bee-library) framework (`Bee.*` packages, last released as 4.33.0) under a new name. Apart from the
renaming, the code of Polhem 1.0.0 is that of Bee.NET 4.33.0. Every name that contained `Bee` was renamed, and the old
names are not recognized: there is no compatibility layer.

### Packages, namespaces and types

- Every `Bee.<Name>` package becomes `Polhem.<Name>`, and the split into packages is unchanged: `Bee.Hosting` becomes
  `Polhem.Hosting`, and so on for each package in the tables above. Namespaces follow the same pattern:
  `Bee.Definition.Forms` becomes `Polhem.Definition.Forms`.
- `Bee` in a type or member name becomes `Polhem`: `AddBeeFramework` becomes `AddPolhemFramework`, `UseBeeFramework`
  becomes `UsePolhemFramework`, `IBeeContext` becomes `IPolhemContext`, `BeeLoginPanel` becomes `PolhemLoginPanel`.
- The command-line tool `Bee.Cli` (`dotnet bee`) becomes `Polhem.Cli` (`dotnet polhem`). Uninstall the old tool and
  install the new one with `dotnet tool install -g Polhem.Cli`.

The compiler reports every place in your code that still uses these names.

### Names the compiler does not check

These are strings. A build with the old names succeeds, and the problem only shows at run time or not at all.

| What | Bee.NET | Polhem | With the old name |
|------|---------|--------|-------------------|
| Type names in definition files: `BusinessObject` and `Repository` in `ProgramSettings.xml`, the `BackendComponents` elements in `SystemSettings.xml`, and type names in your own code | `Bee.Business.AuditLog.LogBusinessObject, Bee.Business` | `Polhem.Business.AuditLog.LogBusinessObject, Polhem.Business` | The type is not found at run time |
| Default environment variable of the master key | `BEE_MASTER_KEY` | `POLHEM_MASTER_KEY` | Only a `SystemSettings.xml` that leaves the variable name to the default is affected. A `MasterKeySource` whose `Value` names the variable keeps using that name; the defaults that `dotnet bee defines materialize` wrote name `BEE_MASTER_KEY` |
| Analyzer diagnostic IDs in `.editorconfig`, `#pragma warning`, `NoWarn` and `[SuppressMessage]` | `BEE1001` | `POLHEM1001` (same numbers) | The setting is silently ignored |
| MSBuild properties for definition file checks | `BeeDefinitionFilesGlob`, `BeeRequireDefinitionFiles`, `BeeAnalyzeDefinitionFiles` | `PolhemDefinitionFilesGlob`, `PolhemRequireDefinitionFiles`, `PolhemAnalyzeDefinitionFiles` | The setting is silently ignored and the default applies |
| CSS classes of the Blazor components | `bee-dynamic-form`, `bee-dynamic-grid`, `bee-form-page`, `bee-login-panel` | `polhem-dynamic-form`, `polhem-dynamic-grid`, `polhem-form-page`, `polhem-login-panel` | Your own style rules no longer apply |
| Logging categories, such as filters under `Logging:LogLevel` | `Bee.Api.AspNetCore` | `Polhem.Api.AspNetCore` | The filter no longer matches |

To find them, run this in the root of your repository:

```bash
grep -rnE "Bee\.|Bee[A-Z]|BEE_|BEE[0-9]{4}|dotnet[- ]bee|bee-(dynamic|form|login)" --include="*.cs" --include="*.razor" --include="*.css" --include="*.xml" --include="*.json" --include="*.csproj" --include="*.props" --include="*.targets" --include=".editorconfig" --include="*.yml" --include="*.yaml" --include="*.sh" --include="Dockerfile" .
```

### What changes when you switch

- **Signed-in users sign in again.** The default `DerivedApiEncryptionKeyProvider` derives each session's encryption
  key with labels that were renamed (`bee-api-*` to `polhem-api-*`), so a session created by Bee.NET no longer works
  with Polhem. The derived keys are never stored, so no stored data depends on the labels.
- **Clients and the server are upgraded together.** Payloads carry type names such as
  `Polhem.Definition.Collections.Parameter, Polhem.Definition`, and the server only accepts types from allowed
  namespaces. A Bee.NET client sends `Bee.*` names, which a Polhem server rejects, and the other way around.
- **Audit records written by Bee.NET keep their old marker.** The `changes_xml` column of `st_log_change` records the
  declared field type of each column as `msprop:Bee.FieldDbType`, and Polhem reads `Polhem.FieldDbType`. The values of
  an old record read the same; only `GetDeclaredFieldDbType()` returns `null` for its columns.
- **DefineEditor starts with fresh settings.** Its settings folder under the user's application data folder is now
  `Polhem.DefineEditor` instead of `Bee.DefineEditor`. Copy the old folder to keep recent files and preferences.
- **Tables and columns are unchanged.** The framework tables keep their `st_` names, and no data migration is needed.

## Design decisions

The reasons behind the design are recorded in the
[architecture decision records](https://github.com/polhem-dev/polhem/blob/main/docs/adr/README.md).

## Contributing

See [CONTRIBUTING.md](https://github.com/polhem-dev/polhem/blob/main/CONTRIBUTING.md).

## License

[MIT](https://github.com/polhem-dev/polhem/blob/main/LICENSE.txt). Copyright (c) Polhem contributors.

## 📬 Contact & Follow
You're welcome to follow my technical notes and hands-on experience sharing

[Facebook](https://www.facebook.com/profile.php?id=61574839666569) | [HackMD](https://hackmd.io/@jeff377) | [GitHub](https://github.com/jeff377) | [NuGet](https://www.nuget.org/profiles/jeff377)
