# Changelog

[繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem packages, a few lines per version. Each version links to its release notes under
[`docs/en/changelogs/`](docs/en/changelogs/), which describe every change in full and, for each breaking change, who
it affects and how to migrate. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

> **1.x minor versions may contain breaking changes**, marked **Breaking:** here and described with their migration
> steps in the version's release notes. Patch versions do not. Versions follow [Semantic Versioning](https://semver.org/)
> from 2.0.0. Reference the packages with a range that stays within one minor version, for example `[1.6,1.7)`, rather
> than `1.*`. The policy and its reasons are in [ADR-052](maintainers/adr/adr-052-breaking-changes-in-1-x-minors.md).

## [Unreleased]

Details and migration steps: [docs/en/changelogs/1.8.0.md](docs/en/changelogs/1.8.0.md)

- `FormLayoutPresenter` renders a form layout without data, so a design tool can preview a layout as `FormView` draws it.
- DefineEditor previews the FormLayout being edited, live, under the property grid.
- DefineEditor takes its colours from VS Code's Dark (Visual Studio) and Light (Visual Studio) themes.
- DefineEditor has a VS Code style activity bar that switches the sidebar between the files, the programs and the database tables.
- DefineEditor shows the CREATE TABLE statement of the TableSchema being edited, for each supported database.
- `dotnet polhem forms` and DefineEditor describe a form in an Excel workbook and import it as the definition files of a runnable form.
- A TableSchema generated from a detail table indexes `sys_master_rowid` (`fk_{0}_master`), as the hand-written ones do.
- DefineEditor is published as the dotnet tool `Polhem.DefineEditor` (`polhem-define [DefinePath]`) and opens the folder named on its command line.
- The new dotnet tool `Polhem.DbManager` (`polhem-db`) runs the `db` commands against a definition folder, for every supported database, without a project. DefineEditor runs it to test the DatabaseSettings connections and to upgrade the database, for the imported form's tables after an import.
- `db check` opens a connection to each database, and `db upgrade --table <name>` builds or upgrades only the named tables.
- The new template package `Polhem.Templates` creates a Polhem application with `dotnet new polhem-avalonia --database <engine>`: a server with the packages and the driver referenced, an Avalonia desktop client on `Polhem.UI.Avalonia.Shell`, a definition folder, the DefineEditor and DbManager tools, and VS Code tasks that initialize the database and open DefineEditor.
- The new package `Polhem.UI.Avalonia.Shell` is a ready-made Avalonia front end: connect, sign in to a company, and open the forms of the server's menu, on every head; it can package a macOS head as a `.app`.
- The Excel import stops at a String field that becomes a column without a max length; the export writes the lengths a form keeps only in its TableSchema.

## [1.7.0] - 2026-10-16

Details and migration steps: [docs/en/changelogs/1.7.0.md](docs/en/changelogs/1.7.0.md)

- **Breaking:** Front ends no longer reference the API client: `ClientInfo` and the endpoint storage move to the new
  `Polhem.UI.ApiBridge` package, and the form views take their connector and definitions through `UIServices`.
- **Breaking:** The server side of `Polhem.Api.Core` (dispatch, authorization, access control) moves to the new
  `Polhem.Api.Server` package; the wire does not change.
- **Breaking:** `FileHashValidator` and `IDisplayName` are removed.
- `FormView` and `ListView` run without a Polhem backend, on a schema, layout and connector the host supplies.

## [1.6.0] - 2026-10-11

Details and migration steps: [docs/en/changelogs/1.6.0.md](docs/en/changelogs/1.6.0.md)

- **Breaking:** `Polhem.Api.Client` has one entry point, `PolhemApiClient`, which owns the connection and the signed-in identity and hands out the connectors.
- **Breaking:** `ApiClientInfo` and the ambient `ApiSessionContext` are removed, and their settings move to `PolhemApiClient`, `ClientInfo` and `ApiSessionCredentials`.
- **Breaking:** Blazor Server injects a `PolhemApiClient` per circuit in place of `PolhemApiConnectorFactory`, and `FormPage` calls as whoever signed in on it.
- **Breaking:** A connector calls with its client's current sign-in, the reachability check no longer sends the API key, and `ClientInfo.ApplyLoginResult` always discards the cached definitions.
- From this version a 1.x minor version may contain breaking changes ([ADR-052](maintainers/adr/adr-052-breaking-changes-in-1-x-minors.md)), so reference the packages with a range within one minor version, such as `[1.6,1.7)`; the wire is unchanged from 1.5.0.
- One process can call several servers, each through a `PolhemApiClient` with its own endpoint, API key and identity.

## [1.5.0] - 2026-10-09

Details and migration steps: [docs/en/changelogs/1.5.0.md](docs/en/changelogs/1.5.0.md)

- **Breaking:** In-process form reads on SQLite, MySQL and Oracle return numeric and boolean columns in the type the FormSchema declares, and an AutoIncrement value above `int.MaxValue` now fails them as it already failed on the wire.
- Form reads no longer drop the fractional part of SQLite decimals, and their JSON responses now decode in clients that decode each cell by its column type, such as polhem-connector-js ([#99](https://github.com/polhem-dev/polhem/issues/99)).
- `DbCommandSpec.ColumnTypes` declares the CLR type each named result column of a `DataTable` command is built with.

## [1.4.0] - 2026-10-08

Details and migration steps: [docs/en/changelogs/1.4.0.md](docs/en/changelogs/1.4.0.md)

- **Breaking:** Projects that reference the Polhem packages need the .NET SDK 10.0.400 or later, because the analyzers in `Polhem.Definition` move to Microsoft.CodeAnalysis 5.9.
- **Breaking:** `PayloadFormat` in `Polhem.Api.Core.Messages`, `Gzip` and `JsonRpcException`, left without a job by the move to Polhem.JsonRpc, are removed.
- **Breaking:** `[TreeNode]` and `[TreeNodeIgnore]` move from `Polhem.Core` to `Polhem.Definition`, and `DatabaseItem` is labelled by its `Id` instead of its `DbName`.
- `ObjectTreeBuilder` and `ObjectTreeView` build and show a tree of an object graph, with commands and drag-and-drop reordering.
- `PropertyGridControl` and `CollectionEditDialog` edit an object through its ComponentModel annotations, and DefineEditor now uses them and the object trees.
- With Avalonia 12.1, the signed-in user's culture no longer reverts to the operating system's.

## [1.3.1] - 2026-10-05

Details and migration steps: [docs/en/changelogs/1.3.1.md](docs/en/changelogs/1.3.1.md)

- **Breaking:** When an Encoded or Encrypted call is answered without a result, which only a server that is not Polhem does, `ApiConnector` throws `InvalidPayloadException` instead of `InvalidOperationException`.
- `ApiConnector` seals its calls through `PayloadConnector` from the new `Polhem.JsonRpc.Payload.Client` package, which requires Polhem.JsonRpc 1.2.0 or later; the wire is the one of 1.3.0.
- The documentation says what a shared API key (`StaticApiEncryptionKeyProvider`) leaves open: a captured call can be replayed in another session.

## [1.3.0] - 2026-10-05

Details and migration steps: [docs/en/changelogs/1.3.0.md](docs/en/changelogs/1.3.0.md)

- **Breaking (wire):** Polhem now runs on Polhem.JsonRpc 1.1, which binds each Encrypted payload to its method and direction and answers a result in its request's format, so the server and every remote client (including polhem-connector-js builds) must upgrade together.
- **Breaking:** The packages require Polhem.JsonRpc 1.1.1 or later, and a server does not start beside `Polhem.JsonRpc.AspNetCore` 1.0 or other code compiled against `Polhem.JsonRpc.Server` 1.0.
- **Breaking (wire):** While `RequireFrame` is on, methods that declare `ReplayProtection = UniqueSequence`, among them `Save` and `Delete`, refuse remote Plain and Encoded calls.
- **Breaking (wire):** A request with no value to bind is answered `-32602 Invalid params` before the method runs.
- **Breaking (wire):** A missing definition throws the new `DefinitionNotFoundException`, which a remote caller receives as a user message (`-32099`).
- Connecting to a remote endpoint no longer sends an HTTP `HEAD` request before the ping.

## [1.2.0] - 2026-10-03

Details and migration steps: [docs/en/changelogs/1.2.0.md](docs/en/changelogs/1.2.0.md)

- **Breaking (wire):** Error responses follow JSON-RPC 2.0 (an internal error is -32603, responses carry no `method` member, a malformed method name is `MethodNotFound`), so a non-.NET client such as polhem-connector-js has to move with the server.
- **Breaking:** JSON-RPC runs on the `Polhem.JsonRpc` packages: `Polhem.Api.AspNetCore`, `ApiServiceController`, `IJsonRpcProvider`, `ApiServiceOptions` and the payload types of `Polhem.Api.Core` are removed, a second exception to semantic versioning within 1.x ([ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md)).
- **Breaking:** A rejected API key or `Authorization` header is answered with HTTP 200 and a JSON-RPC error instead of 401, and a payload envelope with an unknown `format` is refused.
- **Breaking:** In-process calls serialize their parameters like remote calls, and the masked-failure and API key startup logs move to new log categories.
- `AddPolhemPayload()`, `ApiClientInfo.PayloadOptions` and `PolhemPayload` build the payload options, and `AddPolhemApiKeyGateCheck()` adds the API key startup check to HTTP hosts.

## [1.1.0] - 2026-09-30

Details and migration steps: [docs/en/changelogs/1.1.0.md](docs/en/changelogs/1.1.0.md)

- **Breaking:** `Polhem.Base` is renamed to `Polhem.Core`, the package ID and every namespace, as a one-time exception to semantic versioning within 1.x ([ADR-048](maintainers/adr/adr-048-rename-base-to-core-in-1-1.md)).
- **Breaking:** The built-in JSON-RPC type namespace allowlist names `Polhem.Core` instead of `Polhem.Base`.
- The user documents are grouped into topic folders under `docs/<lang>/`, so links to their old paths no longer resolve.
- The `Web.Js.Demo` sample is removed; browser clients use [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js).

## [1.0.0] - 2026-09-28

Details and migration steps: [docs/en/changelogs/1.0.0.md](docs/en/changelogs/1.0.0.md)

- **Breaking:** Polhem continues Bee.NET 4.33.0 under a new name: packages, namespaces, types, the CLI, analyzer IDs, MSBuild properties and every other `Bee` name become `Polhem`, with no compatibility layer.
- **Breaking:** A pre-release review renamed, moved, sealed or removed public types and gave the client API a service provider and cancellation tokens.
- **Breaking:** Stored access tokens and the old password hash format are replaced, so existing sessions end and accounts with old hashes need a password reset.
- **Breaking:** The server checks record scope, request bodies and anonymous calls more strictly, enforces required fields on save and returns one page from `GetList` without paging.
- **Breaking (wire):** The wire carries `Polhem.*` type names and a reshaped contract, so a Bee.NET client and a Polhem server cannot talk to each other.
- Built-in text is English with `zh-TW` translations that follow the user's culture, and the Avalonia heads run on iOS, Android and the browser.
