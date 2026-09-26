<!-- source: zh-TW/dependency-map.md blob: fecdfc8cc242c4ae07ce9c72eca1d187149441cb -->
# Project Dependency Map

[繁體中文](../zh-TW/dependency-map.md) · [← Docs Index](README.md)

This document visualizes the dependencies among the `src/` projects of the Polhem framework.
The diagram covers the runtime packages; `Polhem.Analyzers` is left out because nothing references it
at runtime — consumers get it as a build-time analyzer, and drawing it would add an edge that does
not exist in the assembly graph. (No project count here: it drifts, and the `src/` directory is the
authority.)

**How to read**: an arrow A → B means "A depends on B"; the diagram is laid out bottom-up, with the most foundational packages (no dependencies) at the bottom.

## Dependency Diagram

```mermaid
graph BT
  subgraph Infrastructure
    Base["Polhem.Base"]
    Expressions["Polhem.Expressions"]
    Definition["Polhem.Definition"]
    Caching["Polhem.ObjectCaching"]
  end

  subgraph DataAccess [Data Access]
    RepoAbs["Polhem.Repository.Abstractions"]
    Db["Polhem.Db"]
    Repo["Polhem.Repository"]
  end

  subgraph BusinessLogic [Business Logic]
    Business["Polhem.Business"]
  end

  subgraph SharedContracts [Shared Contracts]
    Contracts["Polhem.Api.Contracts"]
  end

  subgraph API
    Core["Polhem.Api.Core"]
    AspNet["Polhem.Api.AspNetCore"]
  end

  subgraph CompositionRoot [Composition Root]
    Hosting["Polhem.Hosting"]
  end

  subgraph ClientLayer [Client]
    Client["Polhem.Api.Client"]
  end

  subgraph CrossPlatformUI [Cross-platform UI Common]
    UICore["Polhem.UI.Core"]
    UIAvalonia["Polhem.UI.Avalonia"]
  end

  subgraph WebFrontend [Web Frontend]
    BlazorSrv["Polhem.Web.Blazor.Server"]
  end

  Definition --> Base
  Expressions --> Base
  Hosting --> Expressions
  UIAvalonia --> Expressions
  Contracts --> Definition
  Db --> Definition
  RepoAbs --> Definition
  Caching --> Definition
  Caching --> RepoAbs
  Business --> Contracts
  Business --> Definition
  Business --> RepoAbs
  Repo --> Db
  Repo --> RepoAbs
  Core --> Contracts
  Core --> Definition
  Hosting --> Core
  Hosting --> Business
  Hosting --> Db
  Hosting --> Repo
  Hosting --> Caching
  AspNet --> Hosting
  Client --> Core
  UICore --> Client
  UIAvalonia --> UICore
  UIAvalonia --> Client
  UIAvalonia --> Definition
  BlazorSrv --> Client
```

## External Package Dependencies

| Project | External Packages |
|---------|-------------------|
| Polhem.Base | *(none)* |
| Polhem.Expressions | DynamicExpresso.Core 2.x |
| Polhem.Definition | Microsoft.Extensions.Localization.Abstractions 10.x |
| Polhem.Db | *(none)* |
| Polhem.ObjectCaching | Microsoft.Extensions.Caching.Memory 10.x |
| Polhem.Api.Core | MessagePack 3.x |
| Polhem.Business | Microsoft.Extensions.Logging.Abstractions 10.x |
| Polhem.Repository | Microsoft.Extensions.DependencyInjection.Abstractions 10.x |
| Polhem.Hosting | Microsoft.Extensions.DependencyInjection 10.x, Microsoft.Extensions.Hosting.Abstractions 10.x |
| Polhem.Api.AspNetCore | `FrameworkReference: Microsoft.AspNetCore.App` |
| Polhem.Web.Blazor.Server | `FrameworkReference: Microsoft.AspNetCore.App` |
| Polhem.UI.Avalonia | Avalonia 12.0.x, Avalonia.Controls.DataGrid 12.0.x |
| Polhem.Api.Contracts / Polhem.Api.Client / Polhem.Repository.Abstractions / Polhem.UI.Core | *(none)* |

> `Polhem.Api.Core`'s MessagePack reference is the only transport-format package in the framework, and
> keeping it the only one is the point of [ADR-036](../adr/adr-036-wire-serialization-externalized.md).
> Build-time-only references (`PrivateAssets="all"`: SourceLink, the public API analyzers, the
> repository's own analyzers) are omitted — they reach no consumer.

## Target Framework Summary

All runtime packages target `net10.0`. The exception is `Polhem.Analyzers`, which targets
`netstandard2.0` — that is what Roslyn loads analyzers as, so it is a requirement of the analyzer
host rather than a choice.

## Tooling Packages (separately distributed)

Not part of the `src/` library graph above — these ship as `dotnet tool` global tools on NuGet:

| Package | Command | Description |
|---------|---------|-------------|
| **Polhem.Cli** (`tools/Polhem.Cli/`) | `dotnet polhem` | Framework CLI. Currently ships the `defines` subcommand group. References `Polhem.Definition` to call its public `Defaults` API for materialise / list operations on embedded framework defaults. Version-locked to the framework. |

Also under `tools/` but not on NuGet:

- **Polhem.DefineEditor** (`tools/DefineEditor/`) — Avalonia desktop tool for visually editing the define types. Distributed as a downloadable `.app` / `.exe` rather than as a library or dotnet tool. Calls `Polhem.Definition.Defaults.MaterializeTo(...)` in-process on folder open.

## Architectural Notes

- **Polhem.Base** is the lowest-level foundation package with no internal dependencies.
- **Polhem.Expressions** holds `DynamicExpressoEvaluator`, the DynamicExpresso-backed implementation of the expression engine. The *abstraction* — `IExpressionEvaluator`, `ExpressionPolicy`, `ExpressionEvaluationException` — lives in `Polhem.Base.Expressions`, so `Polhem.Definition` (the `FormExpressionCalculator`) and `Polhem.Business` (the rule processor) consume the engine without taking a dependency on DynamicExpresso; only the composition roots that pick an implementation (`Polhem.Hosting` for DI registration, `Polhem.UI.Avalonia` for client-side live preview) reference this package. That split keeps the definition layer free of third-party packages while a field computed on the client still matches what the server writes on save. See [adr-028](../adr/adr-028-expression-rule-engine.md) and [adr-038](../adr/adr-038-definition-dependency-boundary.md).
- **Polhem.Definition** is the most depended-on project, with 7 direct dependents (Contracts, Db, RepoAbs, Caching, Business, Api.Core, UI.Avalonia).
- **Polhem.Api.Contracts** is a shared contract/abstraction layer, not an application-level API project. Despite the "API" name, both `Polhem.Business` and `Polhem.Api.Core` depend on it (`Business → Contracts`, `Core → Contracts`), so it sits *below* them — the diagram groups it under **Shared Contracts** rather than the API application layer.
- **Polhem.Hosting** is the composition root: it consolidates the backend services (`Polhem.Api.Core`, `Polhem.Business`, `Polhem.Db`, `Polhem.Repository`, `Polhem.ObjectCaching`) behind a single `AddPolhemFramework` extension on `IServiceCollection`, with no ASP.NET Core dependency. Non-web hosts (WinForms, Console, Worker Service) reference it directly. It is shown in its own **Composition Root** group rather than under API: reaching across every layer is what a composition root does, so the "API layer must not reference the Repository layer" constraint does not apply to it. What *does* apply is that it holds no data access of its own — statements live in `Polhem.Db` / `Polhem.Repository`, and Hosting keeps only the hosted-service shells and DI wiring.
- **Polhem.Api.AspNetCore** is the ASP.NET Core integration layer (`UsePolhemFramework` middleware + `ApiServiceController`); it pulls in `Polhem.Hosting` transitively, so web hosts get DI registration plus middleware in one package reference.
- Both the client (Polhem.Api.Client) and the server (Polhem.Api.AspNetCore) share protocol logic via **Polhem.Api.Core**, ensuring consistent serialization and encryption behavior.
- **Polhem.UI.Core** is the cross-platform UI common layer (`ClientInfo` / `IEndpointStorage` / `IUIViewService` / `VersionInfo`), shared by every native-UI family (currently Avalonia, which covers desktop / iOS / Android / WASM from one project; future WinForms / WPF) for client-side connection state and endpoint persistence. It contains no platform-specific UI code and depends only on `Polhem.Api.Client`.
- **Polhem.UI.Avalonia** is the Avalonia desktop control library (Windows / macOS / Linux). Ships FormSchema-driven controls (`FormView` for a single record, `ListView` for the list, `GridControl` for grids, plus a field-editor family with `FormScope` ambient binding, all backed by `FormDataObject`) plus a file-backed `FileEndpointStorage` over a single `net10.0` TFM. Lower bound is `Avalonia 12.0.0` + `Avalonia.Controls.DataGrid 12.0.0` (latest stable for DataGrid); hosts may bring a newer `Avalonia 12.0.x` transitively. See [adr-020](../adr/adr-020-avalonia-datagrid-binding-strategy.md) for the DataGrid binding strategy and [adr-021](../adr/adr-021-avalonia-datagrid-editing-strategy.md) for the editing strategy.
- **`Polhem.UI.*` family criterion**: whether the package consumes the `Polhem.UI.Core` abstractions (`ClientInfo` / `IEndpointStorage` / `IUIViewService`, etc.).
  - Consumes → `Polhem.UI.*` (current: `Polhem.UI.Core`, `Polhem.UI.Avalonia`; future: `Polhem.UI.WinForms`, `Polhem.UI.Wpf`, etc.)
  - Does not consume, has its own state management → independent family prefix (e.g. `Polhem.Web.Blazor.*`: a Blazor circuit has no file IO and no dialog service concept, so an independent path is appropriate).
- The **Web frontend layer** (`Polhem.Web.Blazor.Server`) is a Razor Class Library (RCL). It depends only on `Polhem.Api.Client`; the host application decides the `IJsonRpcProvider` implementation (`LocalApiProvider` / `RemoteApiProvider`) and whether to call `AddPolhemFramework`.
