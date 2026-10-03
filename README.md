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
- **Conventions enforced at build time**: Roslyn analyzers ship with the packages and register automatically, turning framework conventions — database scope selection, cross-file definition consistency, wire contract shape — into build diagnostics that name both the cause and the fix. See [Analyzer Rules](https://github.com/polhem-dev/polhem/blob/main/docs/en/reference/analyzer-rules.md).

## 📐 Architecture

For an in-depth look at the layered architecture, data flow, and design decisions behind Polhem, see the [Architecture Overview](https://github.com/polhem-dev/polhem/blob/main/docs/en/architecture/architecture-overview.md).

For guidelines on API Contract and BO Parameter design (Request/Response vs Args/Result), see the [API/BO Contract Design Principles](https://github.com/polhem-dev/polhem/blob/main/docs/en/api/api-bo-contract-design.md). The full catalog of public API methods, with each method's `[ApiAccessControl]` settings, lives in the [API Method Reference](https://github.com/polhem-dev/polhem/blob/main/docs/en/api/api-method-reference.md).

For calling the JSON-RPC API from a JavaScript / TypeScript frontend (React, Vue, Angular, vanilla — no .NET on the client), see the [JSON-RPC Frontend Integration Guide](https://github.com/polhem-dev/polhem/blob/main/docs/en/api/jsonrpc-frontend-integration.md).

For the full developer documentation index, see [docs/en/README.md](https://github.com/polhem-dev/polhem/blob/main/docs/en/README.md).

## 📦 Assembly

Each assembly below ships as a NuGet package of the same name. A JSON-RPC server starts with two of them, plus `Polhem.JsonRpc.AspNetCore` for the HTTP endpoint:

```bash
dotnet add package Polhem.Hosting
dotnet add package Polhem.JsonRpc.AspNetCore
dotnet add package Polhem.Db
```

[Getting Started](https://github.com/polhem-dev/polhem/blob/main/docs/en/getting-started/getting-started.md) continues from there.

### Shared (Frontend / Backend)

| Assembly Name | Description |
|---|---|
| **Polhem.Core.dll** | Core utilities such as serialization, encryption, and general-purpose helpers. |
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
| **Polhem.Cli** | `dotnet tool install -g Polhem.Cli` <br/>Upgrade: `dotnet tool update -g Polhem.Cli` | Framework CLI invoked as `dotnet polhem`. The `defines` commands materialise and list the framework default define files embedded in `Polhem.Definition.dll` (to bootstrap a new consumer's `DefinePath`); the `keys` commands generate protected keys for `SystemSettings.xml`. See the [Polhem.Cli README](https://github.com/polhem-dev/polhem/blob/main/tools/Polhem.Cli/README.md); `dotnet polhem --help` lists the options. |


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

Ready to build your own? [Getting Started](https://github.com/polhem-dev/polhem/blob/main/docs/en/getting-started/getting-started.md) walks through the same thing from an empty folder — packages, `DefinePath`, DI wiring, your first business object, and calling it from a client.

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


## Migrating from Bee.NET

Polhem continues the [Bee.NET](https://github.com/jeff377/bee-library) framework (`Bee.*` packages) under a new name,
with no compatibility layer. What an upgrade has to change is in
[Migrating from Bee.NET](https://github.com/polhem-dev/polhem/blob/main/docs/en/guides/migrating-from-bee-net.md).

## Design decisions

The reasons behind the design are recorded in the
[architecture decision records](https://github.com/polhem-dev/polhem/blob/main/maintainers/adr/README.md).

## Contributing

See [CONTRIBUTING.md](https://github.com/polhem-dev/polhem/blob/main/CONTRIBUTING.md).

## License

[MIT](https://github.com/polhem-dev/polhem/blob/main/LICENSE.txt). Copyright (c) Polhem contributors.

## 📬 Contact & Follow

Polhem is maintained by the [polhem-dev](https://github.com/polhem-dev) organisation on GitHub.

- Questions, ideas and show-and-tell: [GitHub Discussions](https://github.com/polhem-dev/polhem/discussions)
- Bug reports and feature requests: [GitHub Issues](https://github.com/polhem-dev/polhem/issues)
