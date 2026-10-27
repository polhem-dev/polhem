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

Details and migration steps: [docs/en/changelogs/1.10.0.md](docs/en/changelogs/1.10.0.md)

- **Breaking:** `Polhem.Web.Blazor.Server` replaces `DynamicForm` with `FormLayoutPresenter`.
- **Breaking:** `IExpressionEvaluator` gains `Validate`, which parses an expression against variable types without
  evaluating it; an evaluator implemented outside the framework has to implement it.
- **Breaking:** `FormField.LookupProgId` and `FormField.LookupFieldMappings` are removed. A lookup opens
  `RelationProgId` and writes back `RelationFieldMappings`; definitions that still carry the two are read without them.
- `FieldMapping.LookupOnly` marks a relation mapping that a lookup selection writes back but the query join does not
  read, so a selection can carry extra values along, such as an employee's department into a leave request.
- A field marked `FormField.ValueChangeCallback` calls its form's new `FormBusinessObject.FieldValueChange` when the
  user changes it, and both the Avalonia and the Blazor head write the values the server returns into the row.
- `Polhem.Web.Blazor.Server` gains field editors with the same names, contract and behavior as those of
  `Polhem.UI.Avalonia`, and `FormPage` now follows the form mode and each field's `AllowEditModes`.
- `Polhem.Web.Blazor.Server` gains the lookup flow: `ButtonEdit`, `LookupDialog` and `LookupPanel`.
- `Polhem.Web.Blazor.Server` recomputes computed fields live through the new `FormLiveComputation`, and its
  `FormDataObject` gains new-row defaults, row events and buffered row edit sessions.
- `Polhem.Web.Blazor.Server` renders detail tables: `GridControl` (in-cell or edit-form editing), `RowEditPanel`
  and `RowEditDialog`, shown by `FormLayoutPresenter` and `FormPage`, which also previews numbers with the
  company, currency and unit rounding context.
- `Polhem.Web.Blazor.Server`'s `FormPage` switches to a compact layout (one master column, detail rows edited in a
  form) on a narrow viewport, measured in the browser by the new `CompactViewportObserver`.
- Live computation no longer stops silently: when an expression cannot be evaluated, the Avalonia `FormView` and the
  Blazor `FormPage` name the field and the reason, and the cause is exposed as `LiveComputationDegradation`.
- `Polhem.UI.Core` gains the field rules both heads share: `FieldEditRules`, `FieldValueFormatter`,
  `NumericFieldFormat`, `DateTimeFieldFormat`, `LookupFieldRules`, `LookupDisplay`, `GridCellFormatter`, `FieldCaptionRules` and
  the live computation engine `FormLiveComputationEngine`.
- DefineEditor lists the FormSchema expressions that would fail at run time, such as `amount * 1.05` on a decimal
  field, when it validates or saves a form, through the new `FormExpressionCalculator.ValidateExpressions`.

## [1.9.0] - 2026-10-23

Details and migration steps: [docs/en/changelogs/1.9.0.md](docs/en/changelogs/1.9.0.md)

- DefineEditor syncs a FormSchema's new fields into its TableSchemas and FormLayout and upgrades only that form's
  tables, through the new `FormLayoutGenerator.Merge`, which the form workbook import can use too.
- DefineEditor's FormLayout editor can show the property grid, the preview or both, with a properties dialog when the
  grid is hidden, and **View › Toggle Side Bar** (⌘B) collapses the sidebar.
- On iOS and Android, `Polhem.UI.Avalonia.Shell` shows one form at a time with a menu that closes after a pick, and
  the new `ShellOptions.DefaultThemeVariant` sets the starting theme; new template projects and Northwind start dark.
- The shell's menu and tab close icons draw on the browser head, and menu entries and tree rows selected through
  accessibility now open and follow (`TreeViewExtensions.KeepSelectionSingle`).

## [1.8.0] - 2026-10-22

Details and migration steps: [docs/en/changelogs/1.8.0.md](docs/en/changelogs/1.8.0.md)

- A form can be described in an Excel workbook and imported as the definition files of a runnable form, with
  `dotnet polhem forms` or DefineEditor, whose wizard imports several workbooks in a row and upgrades each form's tables.
- The new template package `Polhem.Templates` creates an application with `dotnet new polhem-avalonia`: a server, an
  Avalonia desktop client (browser, iOS and Android clients on request), a definition folder, the tools, and VS Code
  tasks that initialize the database, open DefineEditor and start everything at once.
- The new package `Polhem.UI.Avalonia.Shell` is a ready-made Avalonia front end for every head: connect, sign in, open
  the server's forms, and sign out to see changed definitions; `Polhem.UI.Avalonia` gains `VisualStudioTheme`.
- DefineEditor ships as the dotnet tool `Polhem.DefineEditor`, and the new dotnet tool `Polhem.DbManager` runs the `db`
  commands against a definition folder without a project; `db check` and `db upgrade --table <name>` are new.
- DefineEditor previews the FormLayout being edited (through the new `FormLayoutPresenter`), shows the CREATE TABLE
  statement of a TableSchema, switches its sidebar between files, programs and tables, and takes VS Code's colours.
- A generated detail TableSchema indexes `sys_master_rowid` (`fk_{0}_master`), as the hand-written ones do.

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
