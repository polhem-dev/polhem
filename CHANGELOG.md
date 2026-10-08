# Changelog

[繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/). Each version lists its changes here in one line each;
the reasons and the background are in its detailed notes under [`docs/en/changelogs/`](docs/en/changelogs/).

## [Unreleased]

### Breaking API changes

- `Polhem.Api.Core.Messages.PayloadFormat` is removed. The connectors' `ExecuteAsync` and `ApiCallContext` take
  `Polhem.JsonRpc.Payload.PayloadFormat`, which has the same members and values: replace
  `using Polhem.Api.Core.Messages;` with `using Polhem.JsonRpc.Payload;` where `PayloadFormat` is named, and
  recompile. The wire is unchanged.
  ([ADR-051](maintainers/adr/adr-051-remove-jsonrpc-leftovers-in-1-x.md))
- `Polhem.Core.Serialization.Gzip` is removed; nothing in the framework used it since 1.2.0. Use `GZipStream`
  directly. ([ADR-051](maintainers/adr/adr-051-remove-jsonrpc-leftovers-in-1-x.md))

### Removed

- `Polhem.Api.Core.JsonRpc.JsonRpcException`. The framework has not thrown it since 1.2.0. A business object that
  threw it to show a message throws `UserMessageException` instead, which travels under the same code
  (`-32099`). Exception types may change in a minor version
  ([ADR-046](maintainers/adr/adr-046-api-evolution-policies-for-1-0.md), decision 4).

### Added

- `Polhem.Definition.ObjectTree`: `ObjectTreeBuilder` builds a UI-independent `ObjectTreeNode` tree from an object
  graph, driven by `[TreeNode]` and `[TreeNodeIgnore]`. Nodes raise change notifications so a UI head can bind them;
  the walk keeps each object to one node and stops at `ObjectTreeOptions.MaxDepth`, and `PropertyFilter`, `NodeBuilt`
  and `LabelTranslator` let the consumer adjust the tree.
- `TreeNodeAttribute.GetDisplayText(object, Func<string, string>?)` translates the display format before the property
  values are put into it.
- `ObjectTreeView` (`Polhem.UI.Avalonia`): a `TreeView` that shows an `ObjectTreeNode` tree. Set `RootNode`, and
  optionally `IconSelector` for an icon before each label; each item's `IsExpanded` binds both ways to the node.
- `ObjectTreeNode.IsExpanded` and `ObjectTreeOptions.ExpandDepth`, which sets how many levels of a built tree start
  expanded.
- `ITreeNodeCommandProvider` and `TreeNodeCommand` (`Polhem.Definition.ObjectTree`): UI-independent commands for a
  tree node. `ObjectTreeView.CommandProvider` fills the tree's context menu with the selected node's commands each
  time it opens.
- `ITreeNodeDragDropHandler` and `TreeNodeDropPosition` (`Polhem.Definition.ObjectTree`): which tree nodes can be
  dragged and where they may land. `ObjectTreeView.DragDropHandler` lets the user drop a node before or after another,
  shows where it will land, and moves the node in the tree after the handler has moved the objects.
- `ObjectTreeNode(object, Func<object, string>, bool)`: a node whose label is computed from its value and recomputed on
  `Refresh`, for nodes a consumer adds that must follow a change of display language.
- `PropertyGridControl` (`Polhem.UI.Avalonia`): shows the properties of `SelectedObject` as labels and editors driven
  by `[Category]`, `[Description]`, `[DisplayName]`, `[Browsable]`, `[DefaultValue]` and `[TypeConverter]`. A value
  that differs from its default is shown in bold and the label's context menu resets it; `PropertyValueChanged`
  reports each write. `LabelTranslator` translates the labels: it receives each text as a `PropertyGridText` with its
  `PropertyGridTextKind` (category, display name, description, item label), the type it belongs to and the property
  name, so a description can be looked up by the property; `Refresh` reads the object and translates again. A property
  marked `[PasswordPropertyText(true)]` is masked, and `ValueSuggestionProvider` turns a string property into a
  drop-down of suggested values that still takes typed text, and `PropertyFilter` narrows the rows, for example to
  one category. A collection property opens in `CollectionEditDialog`,
  or in whatever `CollectionEditorProvider` supplies; nested objects are shown read-only. The control reads types through `TypeDescriptor` and is meant for desktop tools, not
  trimmed mobile heads.
- `CollectionEditDialog` and `CollectionEditContext` (`Polhem.UI.Avalonia`): a modal dialog that edits a collection
  property, with the items in a list (add, delete, move up and down) and a `PropertyGridControl` for the selected one.
  It edits copies of the items and OK replaces the collection's items with them, so references to the old items must
  be read again; Cancel leaves the collection untouched. In a keyed collection a new item gets a free key, and OK
  refuses a key used twice.
- `PolhemUIText.ResetValue`, `CollectionSummary`, `MoveUp`, `MoveDown`, `CollectionKeyMissing` and
  `CollectionKeyDuplicate`, the text keys the property grid and the collection dialog use.

### Changed

- `TreeNodeAttribute` and `TreeNodeIgnoreAttribute` move from `Polhem.Core.Attributes` (`Polhem.Core`) to
  `Polhem.Definition.Attributes` (`Polhem.Definition`), next to the definition types they annotate. Nothing in the
  framework read them yet, so applying them had no effect; a type of your own that applies them needs
  `using Polhem.Definition.Attributes;` instead of `using Polhem.Core.Attributes;`.
- The `[TreeNode]` annotations of the definition types follow one rule for folders: a collection is shown as a folder
  when its owner has more than one annotated collection, and directly under the owner otherwise. The Sections and
  Details of `FormLayout` and the Tables and Rules of `FormSchema` become folders; the Fields of `FormTable` and the
  Entries of `LanguageEnum` no longer are. Every annotated object type now names the properties its label is
  formatted from, matching its `ToString()`, and `IndexField` with its collection gains an annotation so an index
  lists its fields. `TreeNodeAnnotationGateTests` pins both conventions.
- `PermissionRule.Action` declares a type converter whose exclusive standard values are the single actions, so a
  property grid offers a drop-down of one action rather than a text box for combined flags. The XML form of a rule is
  unchanged.
- Every property a property grid shows on a definition type now declares `[Category]` and `[Description]`, so it
  groups and describes itself instead of falling into `Misc`; `EditorAnnotationGateTests` pins this. The collections
  an editor shows as tree nodes (the tables and rules of a form schema, the items of the settings files, a language
  resource's items and enums) and `SessionUser.AccessToken` are marked `[Browsable(false)]`. Neither attribute
  changes the XML or wire form.
- The encryption keys of `SecurityKeySettings` are marked `[PasswordPropertyText(true)]`, so a property grid masks
  them. `LanguageItem.Key` is marked browsable again: the override inherited `[Browsable(false)]` from the base key.
- `DatabaseItem` is labelled by its `Id` rather than its `DbName`, in its `[TreeNode]` annotation and its
  `ToString()`: an item that names no database, such as a SQLite one, showed an empty name.
- The Roslyn analyzers shipped in the `Polhem.Definition` package are built against Microsoft.CodeAnalysis 5.9, so a
  project that references the package needs the .NET SDK 10.0.400 or later. An older compiler reports `CS9057` instead
  of running the analyzers, which fails the build under `TreatWarningsAsErrors`.
- The package dependencies move to MessagePack 3.1.11, DynamicExpresso.Core 2.19.6 and the 10.0.12 releases of the
  `Microsoft.Extensions` packages the framework references.

### Samples and tools

- DefineEditor builds every document tree with `ObjectTreeBuilder`, shows it in `ObjectTreeView` and takes its context
  menus from an `ITreeNodeCommandProvider` per document type, in place of its hand-written tree nodes. Labels follow
  the `[TreeNode]` annotations and folders no longer show a count. In every editor a node can be dragged before or after
  a sibling to reorder the collection that holds it. Folder, root and group labels follow the editor's language and
  switch with it.
- DefineEditor shows the selected object in a `PropertyGridControl` instead of hand-written property panels, so every
  browsable property is editable, grouped by `[Category]` and described by `[Description]`. Categories and
  descriptions are translated into Traditional Chinese and follow the editor's language; property names stay as written.
  A field's ListItems are edited in the grid's collection dialog rather than as tree nodes; selecting a field's
  Relation or Lookup group shows its Relation properties, and form ids, mapping fields and the language enums of the
  solution are offered as suggestions.
  Pasting a connection string moves to a dialog on a database server's or item's context menu, and its preview no
  longer shows the password. Renaming an object relabels its tree node at once.
- Avalonia.DemoCenter adds a Property Grid case: `PropertyGridControl` over a `FormField`, a `DbField`, a
  `DatabaseServer`, a `PermissionRule`, `BackendConfiguration` and a demo object of groups with entries, with its
  display options, suggested form ids and a log of `PropertyValueChanged`; the FormField's `ListItems` open in
  `CollectionEditDialog`, and a group's entries open a second dialog from inside the first.

## [1.3.1] - 2026-10-05

📄 Full notes and background: [docs/en/changelogs/1.3.1.md](docs/en/changelogs/1.3.1.md)

### Changed

- `ApiConnector` seals its calls through `PayloadConnector` from the new `Polhem.JsonRpc.Payload.Client` package, which
  `Polhem.Api.Client` now references instead of `Polhem.JsonRpc.Client` (still referenced through it). Requires
  Polhem.JsonRpc 1.2.0 or later. The wire format and the formats, keys and sequence numbers a call uses are unchanged.
  ([#63](https://github.com/polhem-dev/polhem/pull/63))

### Documentation

- What a shared API key (`StaticApiEncryptionKeyProvider`) leaves open: a call captured in one session can be replayed
  in another session while its timestamp is within the tolerance. ([#62](https://github.com/polhem-dev/polhem/pull/62))

## [1.3.0] - 2026-10-05

> Polhem now runs on [Polhem.JsonRpc](https://github.com/polhem-dev/polhem-jsonrpc) 1.1 (1.1.1 or later), whose
> payload wire format is incompatible with that of Polhem 1.2.0: the HMAC of each Encrypted payload also covers its
> method and its direction, and a result is answered in its request's format. **The server and every remote client
> must upgrade together**: .NET clients on `Polhem.Api.Client`, and browser clients built from
> [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js) `main` at or after the commit that merges
> [polhem-dev/polhem-connector-js#11](https://github.com/polhem-dev/polhem-connector-js/pull/11). Polhem's public .NET
> API changes only by additions, so this ships in a minor version: a third exception within 1.x, after 1.1.0 and
> 1.2.0. The reasons are in [ADR-050](maintainers/adr/adr-050-jsonrpc-1-1-wire-break-in-1-3.md).

📄 Full notes and background: [docs/en/changelogs/1.3.0.md](docs/en/changelogs/1.3.0.md)

### Security

- On 1.2.0, someone who captured an Encrypted request could send it to another method of the same session, for
  example to turn a read into a delete, or send a result back as a request: the HMAC covered the payload's bytes and
  nothing else. 1.3.0 closes both by binding the method and the direction into the HMAC
  ([ADR-050](maintainers/adr/adr-050-jsonrpc-1-1-wire-break-in-1-3.md),
  [ADR-003 of Polhem.JsonRpc](https://github.com/polhem-dev/polhem-jsonrpc/blob/main/maintainers/adr/adr-003-bind-method-into-payload-hmac.md)).
  The fix changes the wire; what that means for an upgrade is under "Breaking wire changes" below.
  ([#60](https://github.com/polhem-dev/polhem/pull/60))

### Breaking wire changes

- The HMAC of each Encrypted payload also covers its method and its direction (see "Security" above). A mismatched
  server and client fail every Encrypted call, and the server answers it with `-32603 Internal error`.
  ([#60](https://github.com/polhem-dev/polhem/pull/60))
- While the wire frame is required (`RequireFrame`), a method that declares `ReplayProtection = UniqueSequence`
  refuses a remote Plain or Encoded call from a signed-in session with `-32602 Invalid params`. A Plain call used to
  be accepted without a sequence check, and an Encoded call was checked against a sequence number anyone could forge,
  because only an Encrypted frame is covered by the HMAC. The framework's own `Save`, `Delete`, `ExecFunc`,
  `EnterCompany` and `LeaveCompany` are such methods, so a client without a session key, such as a .NET browser
  (WebAssembly) head, can no longer call them under `RequireFrame`. ([#60](https://github.com/polhem-dev/polhem/pull/60))

To upgrade, move the server and its clients in the same window, and raise every `Polhem.JsonRpc.*` package you
reference directly to 1.1.1 or later. A host that serves the API over HTTP references `Polhem.JsonRpc.AspNetCore`
itself; version 1.0 of it stops the dispatcher from starting:

```diff
- <PackageReference Include="Polhem.Hosting" Version="1.2.0" />
- <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="1.0.0" />
+ <PackageReference Include="Polhem.Hosting" Version="1.3.0" />
+ <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="1.1.1" />
```

`JsonRpcServerOptions.AllowCodeCompiledAgainst10` cannot get past that startup error: from 1.1.1 it has no effect,
because code compiled against 1.0 that reads or sets the transport kind can take an HTTP call for an in-process one, and
Polhem lets in-process calls past its access checks. Code of your own compiled against `Polhem.JsonRpc.Server` 1.0, such as a filter, must be
recompiled. An `IPayloadEncryptor` of your own must implement the overloads that take associated data, and
authenticate that data: one that ignores it accepts the 1.2.0 form and reopens both attacks. The rest of
Polhem.JsonRpc's own changes are in [its changelog](https://github.com/polhem-dev/polhem-jsonrpc/blob/main/CHANGELOG.md).

### Added

- `DefinitionNotFoundException` in `Polhem.Definition.Storage`, a `FileNotFoundException` that names the definition
  type and key. ([#51](https://github.com/polhem-dev/polhem/pull/51))

### Changed behaviour

- **Wire-visible:** a result is answered in the format its request was sent in, a `null` result included, and
  `ApiConnector` refuses a result in another format with `InvalidPayloadException`. ([#60](https://github.com/polhem-dev/polhem/pull/60))
- **Wire-visible:** a request whose parameters carry no value to bind — no `params` member, or a payload envelope
  without a `value` or with a `null` one — is answered with `-32602 Invalid params` before the method runs. It used to
  call the method with a `null` argument: most framework methods check for it with `ArgumentNullException`, which
  answered `-32099` with "The request is not valid.", and the others failed with `-32603 Internal error`. This matches
  the default binder of `Polhem.JsonRpc`. ([#60](https://github.com/polhem-dev/polhem/pull/60))
- In-process calls are not checked for repeated sequence numbers. ([#60](https://github.com/polhem-dev/polhem/pull/60))
- The packages depend on Polhem.JsonRpc `[1.1.1, 2.0.0)` instead of `1.0.0` or later. 1.1.1 is the first version
  whose dispatcher refuses to start beside any assembly compiled against `Polhem.JsonRpc.Server` 1.0 that is loaded
  when it is created, with no exception: in it `AllowCodeCompiledAgainst10` has no effect. ([#60](https://github.com/polhem-dev/polhem/pull/60))
- A definition the built-in file or database storage must hold but does not — a form schema, a table schema, the
  program registry or the database categories — now throws `DefinitionNotFoundException`, and so does the
  `GetFormSchema` API when a storage of your own returns no form schema. A remote caller receives
  its message (such as `FormSchema 'Employee' not found.`) under the UserMessage code (-32099) instead of a generic
  InternalError (-32603) from a file storage or a fixed message from a database storage. The message names only the
  definition type and its key, never a path. A database storage threw `InvalidOperationException` before; code that
  caught that type for a missing definition must catch `DefinitionNotFoundException` (or `FileNotFoundException`)
  instead. ([#51](https://github.com/polhem-dev/polhem/pull/51))
- Connecting to a remote endpoint (`ApiConnectValidator`, and through it `ClientInfo` in the UI heads) checks the
  endpoint with the ping alone. It no longer sends an HTTP `HEAD` request first, which the endpoint answered with 405
  on every connect. A host that cannot be reached is still reported as `Endpoint not reachable`. ([#50](https://github.com/polhem-dev/polhem/pull/50))

### Samples and tools

- Northwind opens the first form of the menu after sign-in on the browser and iOS heads too, once the menu has loaded
  from the server. ([#49](https://github.com/polhem-dev/polhem/pull/49))

### Documentation

- The guide for migrating from Bee.NET moves out of the README into
  [`docs/en/guides/migrating-from-bee-net.md`](docs/en/guides/migrating-from-bee-net.md). ([#52](https://github.com/polhem-dev/polhem/pull/52))

## [1.2.0] - 2026-10-03

> JSON-RPC now runs on the [`Polhem.JsonRpc`](https://github.com/polhem-dev/polhem-jsonrpc) packages, and the payload
> envelope, its encryption and its replay frame on their optional payload packages. `Polhem.Api.AspNetCore` and
> `ApiServiceOptions` are removed. That breaks hosts, in a minor version: a second exception within 1.x, after the one
> in 1.1.0. On the wire, the parameters and the result envelope are unchanged;
> the internal error code and the `method` member of responses are aligned with JSON-RPC 2.0, so a non-.NET client
> has to move with the server. The reasons, and the framework types this release treats as internal plumbing, are in
> [ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md).

📄 Full notes and background: [docs/en/changelogs/1.2.0.md](docs/en/changelogs/1.2.0.md)

### Breaking API changes

- The `Polhem.Api.AspNetCore` package is removed, with `ApiServiceController` and `UsePolhemFramework()`. A host
  serves the API with `Polhem.JsonRpc.AspNetCore`. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- `IJsonRpcProvider` is removed. `RemoteApiProvider` and `LocalApiProvider` implement the package's
  `IJsonRpcTransport`, and `ApiConnector.Provider` has that type. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- `JsonRpcExecutor` and the message types `JsonRpcRequest`, `JsonRpcResponse` and `JsonRpcError` of
  `Polhem.Api.Core.JsonRpc` are removed. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- `ApiServiceOptions` is removed, with the payload types of `Polhem.Api.Core`: the transformer, serializer,
  compressor and encryptor interfaces and their implementations, `ApiPayloadOptionsFactory`, the envelope types
  (`ApiPayload`, `JsonRpcParams`, `JsonRpcResult`, `ApiPayloadConverter`), `ApiPayloadFrame`, `IReplayWindowStore`,
  `MemoryReplayWindowStore` and `ReplayRejectedException`. Their replacements are in `Polhem.JsonRpc.Payload`; the
  exception a client catches for a replayed call is now `Polhem.JsonRpc.Payload.ReplayRejectedException`. ([#47](https://github.com/polhem-dev/polhem/pull/47))
- `MessagePackPayloadSerializer` is renamed `MessagePackPayloadCodec` and implements the package's `IPayloadCodec`. ([#47](https://github.com/polhem-dev/polhem/pull/47))
- `IApiAuthorizationValidator` is resolved from the service collection instead of
  `ApiServiceOptions.AuthorizationValidator`; register your own to replace the default. ([#47](https://github.com/polhem-dev/polhem/pull/47))

To upgrade a host that serves the API over HTTP:

```diff
- <PackageReference Include="Polhem.Api.AspNetCore" Version="1.1.0" />
+ <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="1.0.0" />
```

```diff
  builder.Services.AddPolhemFramework(configuration, paths);
- builder.Services.AddControllers();
+ builder.Services.AddJsonRpcServer();
+ builder.Services.AddPolhemApiKeyGateCheck();
  var app = builder.Build();
- app.UsePolhemFramework();
- app.MapControllers();
+ app.MapJsonRpc("/api");
```

Delete the controller derived from `ApiServiceController`. A check that overrode one of its members becomes a filter,
added with `AddJsonRpcServer(options => options.Filters.Add(...))`.

Every host replaces its `ApiServiceOptions` calls:

```diff
- ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);
- ApiServiceOptions.RequireWireFrame = true;
+ builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode,
+     options => options.RequireFrame = true);
```

A .NET client sets `ApiClientInfo.PayloadOptions.RequireFrame` to match the server. The rest of the migration (codecs,
the replay store, the authorization validator, a host that also calls the API in-process) is in decision 5 of
[ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md).

### Added

- `AddPolhemApiKeyGateCheck()` in `Polhem.Hosting`: the startup log while no API key has been issued, for hosts that
  serve the API over HTTP. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- `AddPolhemPayload()` in `Polhem.Hosting`, `ApiClientInfo.PayloadOptions` in `Polhem.Api.Client`, and `PolhemPayload`
  in `Polhem.Api.Core`, which builds the payload options the framework's way. ([#47](https://github.com/polhem-dev/polhem/pull/47))

### Changed behaviour

- An internal error is answered with code -32603 instead of -32000, and `JsonRpcErrorCode.InternalError` has that
  value. Responses no longer carry a `method` member. A [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js)
  client needs the release that targets 1.2.0. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- A rejected API key or `Authorization` header is answered with HTTP 200 and a JSON-RPC error instead of 401, so the
  .NET client throws the error contract's exception instead of `HttpRequestException`. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- A malformed method name is answered with `MethodNotFound` (-32601) instead of `UserMessage`. An unknown method name
  is answered with a fixed message, also in debug mode, and leaves no anomaly record. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- In-process calls serialize their parameters, like remote calls. ([#46](https://github.com/polhem-dev/polhem/pull/46))
- A payload envelope whose `format` is not 0, 1 or 2 is refused as invalid parameters. ([#47](https://github.com/polhem-dev/polhem/pull/47))
- Log categories: masked failures log under `Polhem.Api.Core.Dispatch.PolhemExceptionMapper`, and the API key startup
  check under `Polhem.Hosting.ApiKeys.ApiKeyGateWarningService`. ([#46](https://github.com/polhem-dev/polhem/pull/46))

## [1.1.0] - 2026-09-30

> `Polhem.Base` is renamed to `Polhem.Core`. Semantic versioning would hold that back until 2.0.0; it ships in 1.1.0 as
> a one-time exception, because 1.0.0 had no known adopters and every 1.0.0 package is unlisted. From 1.1.0 on, 1.x
> follows semantic versioning without exceptions. The reasons are in
> [ADR-048](maintainers/adr/adr-048-rename-base-to-core-in-1-1.md).

📄 Full notes and background: [docs/en/changelogs/1.1.0.md](docs/en/changelogs/1.1.0.md)

### Breaking API changes

- `Polhem.Base` is renamed to `Polhem.Core`: the package ID and every namespace. ([#42](https://github.com/polhem-dev/polhem/pull/42))

To upgrade from 1.0.0 (the package reference only if you reference `Polhem.Base` directly):

```diff
- <PackageReference Include="Polhem.Base" Version="1.0.0" />
+ <PackageReference Include="Polhem.Core" Version="1.1.0" />
- using Polhem.Base.Serialization;
+ using Polhem.Core.Serialization;
```

### Changed behaviour

- The built-in JSON-RPC type namespace allowlist names `Polhem.Core` instead of `Polhem.Base`. ([#42](https://github.com/polhem-dev/polhem/pull/42))

### Samples and tools

- The `Web.Js.Demo` sample is removed; browser clients use
  [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js). ([#41](https://github.com/polhem-dev/polhem/pull/41))

### Documentation

- The user documents are grouped into topic folders under `docs/<lang>/`, and the maintainer documents and ADRs move
  to `maintainers/`. ([#38](https://github.com/polhem-dev/polhem/pull/38), [#39](https://github.com/polhem-dev/polhem/pull/39), [#40](https://github.com/polhem-dev/polhem/pull/40))

## [1.0.0] - 2026-09-28

> Polhem continues [Bee.NET](https://github.com/jeff377/bee-library) under a new name. Polhem 1.0.0 is Bee.NET 4.33.0,
> the last Bee.NET release, renamed, plus the changes listed below. How to move an application over is described in
> [Migrating from Bee.NET](README.md#migrating-from-beenet).

📄 Full notes and background: [docs/en/changelogs/1.0.0.md](docs/en/changelogs/1.0.0.md)

### Renamed from Bee.NET

The renaming was done before the repository took pull requests, so these entries have no link.

- Package IDs and namespaces are renamed from `Bee.*` to `Polhem.*`, and versions restart at 1.0.0. The split into
  packages is unchanged.
- Types and members named after Bee are renamed: `AddBeeFramework` → `AddPolhemFramework`, `UseBeeFramework` →
  `UsePolhemFramework`, `AddBeeBlazor` → `AddPolhemBlazor`, `BeeBlazorOptions` → `PolhemBlazorOptions`,
  `BeeBlazorProviderMode` → `PolhemBlazorProviderMode`, `BeeApiConnectorFactory` → `PolhemApiConnectorFactory`,
  `BeeAccessTokenProvider` → `PolhemAccessTokenProvider`, `BeeLoginPanel` → `PolhemLoginPanel`, and the extension
  classes `BeeFrameworkServiceCollectionExtensions`, `BeeFrameworkApplicationBuilderExtensions` and
  `BeeBlazorServiceCollectionExtensions` → `Polhem…`. `IBeeContext` / `BeeContext` and `BeeStringLocalizer<T>` got new
  names that do not mention the framework; see *Breaking API changes*.
- The command-line tool `Bee.Cli` is now `Polhem.Cli`, invoked as `dotnet polhem`.
- Analyzer diagnostic IDs are renamed from `BEE` to `POLHEM` with the same numbers, for example `BEE1008` →
  `POLHEM1008`. `POLHEM4001`–`POLHEM4004` are reserved: they were Bee.NET's `BEE4001`–`BEE4004` and are never
  reused. ([#10](https://github.com/polhem-dev/polhem/pull/10))
- The MSBuild properties for definition file checks are renamed: `BeeDefinitionFilesGlob`, `BeeRequireDefinitionFiles`
  and `BeeAnalyzeDefinitionFiles` → `PolhemDefinitionFilesGlob`, `PolhemRequireDefinitionFiles` and
  `PolhemAnalyzeDefinitionFiles`.
- The default environment variable of the master key is `POLHEM_MASTER_KEY` instead of `BEE_MASTER_KEY`.
- Type names in payloads and in the default settings are `Polhem.*`. A Bee.NET client and a Polhem server, or the other
  way around, cannot talk to each other.
- The labels that derive session encryption keys are `polhem-api-session-key` and `polhem-api-encryption-root-key`
  instead of `bee-api-*`.
- The CSS classes of the Blazor components start with `polhem-` instead of `bee-`.
- The `DataColumn` extended property that records a declared field type is `Polhem.FieldDbType` instead of
  `Bee.FieldDbType`, and `SerializationErrorData.FilePath` is `Polhem.FilePath` instead of `Bee.FilePath`.
- Package metadata: the authors and the copyright holder are Polhem contributors, the repository is
  `polhem-dev/polhem`, and the packages have a new icon. `Polhem.Cli` ships the same metadata, a README and a symbol
  package. ([#3](https://github.com/polhem-dev/polhem/pull/3))

### Security

- Access tokens are no longer stored: `st_session` is keyed by a SHA-256 derived key (`AccessTokenHasher`), and the log
  tables keep a short token fingerprint (`token_fingerprint`) instead of the `access_token` column.
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- Passwords are hashed with PBKDF2-SHA256 at 600,000 iterations; weaker stored hashes are replaced at the next
  successful sign-in, and hashes in the PBKDF2-SHA1 format inherited from Bee.NET no longer verify.
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- An unknown user name runs a decoy hash, so sign-in timing does not reveal whether the account exists, and the login
  attempt tracker stays bounded. ([#5](https://github.com/polhem-dev/polhem/pull/5))
- Record scope is also checked on the stored row an update or delete targets, on modified and deleted detail rows, and
  on the values a saved master row leaves behind. ([#4](https://github.com/polhem-dev/polhem/pull/4))
- `GetList` and `GetCount` accept filter and sort fields only when the form declares them, and never on protected
  fields; `GetLookup` applies the Read record scope (`FormBusinessObject.LookupAppliesRecordScope` opts out).
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- Remote `GetDefine` serves only an explicit list of definition types.
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- A caller cannot slip past an audit rule by changing the casing of a ProgId: business objects are created with the
  casing `ProgramSettings` declares, audit rules are looked up case-insensitively, and audit records carry the
  FormSchema's ProgId spelling. ([#4](https://github.com/polhem-dev/polhem/pull/4),
  [#26](https://github.com/polhem-dev/polhem/pull/26))
- Encoded and Encrypted requests are decoded into the parameter type of the resolved action, the depth limit applies
  to nested filters on both codecs, and actions resolve only to public, non-generic, one-parameter instance methods
  that are not accessors (`JsonRpcExecutor.IsResolvableAction`, also checked by `POLHEM3001`).
  ([#6](https://github.com/polhem-dev/polhem/pull/6))
- `Plain` bodies are read into the action's request type and then copied into its arguments, like Encoded ones, so a
  Plain call cannot set members the contract does not declare. ([#22](https://github.com/polhem-dev/polhem/pull/22))
- Messages of BCL exceptions no longer reach remote callers; each error code has a fixed message and the original is
  logged through `JsonRpcExecutor.Logger`. ([#6](https://github.com/polhem-dev/polhem/pull/6))
- `CreateSession` accepts only local calls. `CreateApiKey`, `SetApiKeyEnabled` and `SetApiKeyExpiry` are
  replay-protected. ([#5](https://github.com/polhem-dev/polhem/pull/5))
- The database anomaly log is readable only by a deployment administrator.
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- The client refuses a server-advertised "none" encryptor unless it runs in debug mode itself, and no longer adopts the
  server's debug flag or type namespaces. ([#6](https://github.com/polhem-dev/polhem/pull/6))
- The master key file and the client's `apikey.txt` are written with owner-only permissions; a warning is logged when
  database passwords exist without a `ConfigEncryptionKey`. ([#5](https://github.com/polhem-dev/polhem/pull/5))
- DDL escapes SQL Server string defaults and requires non-string defaults to be literals of their type; connection
  string placeholders are resolved with `DbConnectionStringBuilder` (`ConnectionStringTemplate`).
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- The error for a missing definition file names the file, never its path on the server
  (`FileUtilities.EnsureFileExists`). ([#26](https://github.com/polhem-dev/polhem/pull/26))

### Changed behaviour

- A request without an `Authorization` header is an anonymous call: `[ApiAccessControl]` alone decides, and a method
  that needs a session answers JSON-RPC `-32001` (Unauthorized) instead of HTTP 401. The client turns it into an
  `UnauthorizedAccessException`, and `RemoteApiProvider` sends no `Authorization` header before sign-in.
  ([#6](https://github.com/polhem-dev/polhem/pull/6), [#18](https://github.com/polhem-dev/polhem/pull/18))
- `GetList` without paging returns the first page, capped at `PagingOptions.MaxPageSize`.
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- English is the base language of every built-in definition file and UI text; Chinese moves to the shipped `zh-TW`
  language resources. Captions, enums, menus, rule messages and framework text share one fall-back chain
  (`LanguageFallback`): the requested culture, its parents, `CommonConfiguration.DefaultLanguage`, then the base text.
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- The login response carries the user's culture, which the client adopts. `CommonConfiguration.DefaultLang` and
  `BackendConfiguration.DefaultLanguage` merge into `CommonConfiguration.DefaultLanguage` (default `zh-TW`).
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- Framework messages meant for end users carry a key and arguments (`UserMessageException`) and are resolved in the
  session culture; form rule messages resolve `{ProgId}.Rule.{RuleId}.Message`.
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- Numbers and dates are displayed and parsed in the user's culture; the wire stays culture-invariant.
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- `ValueUtilities.CBool` accepts only `1`, `T`, `TRUE`, `Y` and `YES` (ignoring case) as true; the Chinese words for
  yes and true are no longer recognized. ([#15](https://github.com/polhem-dev/polhem/pull/15))
- `BackendComponents` entries default to blank, which selects the framework default. A wrong `CacheProvider` or
  component type name fails at startup and names the setting.
  ([#8](https://github.com/polhem-dev/polhem/pull/8), [#11](https://github.com/polhem-dev/polhem/pull/11))
- The client stores the endpoint and the API key in `endpoint.txt` and `apikey.txt` under the per-user local
  application data folder (`FileEndpointStorage`, now in `Polhem.UI.Core`) instead of a settings file beside the
  assembly. ([#12](https://github.com/polhem-dev/polhem/pull/12))
- Over MessagePack, a `DataTable` is written as one column table and positional rows. JSON and Plain are unchanged.
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- Every wire member whose initializer is not the CLR default is always written, so an absent member means the CLR
  default on every codec. Plain requests bind object-typed filter and parameter values by JSON kind.
  ([#9](https://github.com/polhem-dev/polhem/pull/9))
- Serializing a cached definition no longer changes it: empty collections are omitted through get-only `XSpecified`
  properties instead of a per-object serialize state. ([#8](https://github.com/polhem-dev/polhem/pull/8))
- Startup logs a warning when methods require `ApiReplayProtection.UniqueSequence` while `RequireWireFrame` is off,
  and lists the forms that declare no permission model, including stored forms `ProgramSettings` does not list
  (`IDefineStorage.GetFormSchemaIds`).
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6),
  [#26](https://github.com/polhem-dev/polhem/pull/26))
- Default values of `Short`, `Long`, `Decimal` and `Binary` fields are typed and non-null.
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- `DefaultValueExpression` takes precedence when a new row is created: `GetNewData` and the client's new row write the
  expression's value over the literal `DefaultValue` and the per-type seed. At save it still fills only fields that are
  empty. ([#22](https://github.com/polhem-dev/polhem/pull/22))
- `ClientInfo.UseDefinitionLoader` is on by default, so the Avalonia views show localized captions, the tenant's layout
  and the company's number formats without setup. ([#22](https://github.com/polhem-dev/polhem/pull/22))
- A host built with `AddPolhemFramework` does not start without a `common` database item
  (`IDatabaseSettingsProvider.ValidateRequired`). ([#22](https://github.com/polhem-dev/polhem/pull/22))
- An unknown action answers JSON-RPC `-32601` and an unreadable Plain body `-32602`, each with a fixed message.
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- `FormField.Required` is enforced on save: an added or modified row, master or detail, whose required field is
  empty is refused with a localized message, and the Avalonia `FormView` and the Blazor `FormPage` name every such
  field before sending the save. Check the `Required` flags of your FormSchemas: fill the data or clear the flag.
  ([#26](https://github.com/polhem-dev/polhem/pull/26), [#27](https://github.com/polhem-dev/polhem/pull/27))
- Audit records of a form that `ProgramSettings` does not list carry the FormSchema's ProgId spelling instead of the
  caller's; records written before keep theirs, so compare older `prog_id` values case-insensitively.
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- A custom `DefineAccess` or `DefineStorage` named in `BackendComponents` is created with `ActivatorUtilities`, so its
  constructor can take any registered service instead of one of a fixed set of signatures.
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- The Blazor `FormPage` localizes definitions through a definition loader by default
  (`PolhemBlazorOptions.UseDefinitionLoader`); set it to `false` to render definitions as stored.
  ([#27](https://github.com/polhem-dev/polhem/pull/27))
- A `ProgramSettings.xml` in the nested `<Categories>` layout is no longer refused; it loads as an empty registry.
  Bee.NET 4.33.0 refused such a file, so only a deployment that never ran on it is affected: convert the file with
  Bee.NET's `dotnet bee defines split-menu` first. ([#28](https://github.com/polhem-dev/polhem/pull/28))
- The shipped `AuditRule`, `Department` and `Employee` layouts are regenerated from their schemas (drop-downs, a check
  box and lookups instead of text boxes). Materializing skips existing files, so a deployment that already has these
  layouts keeps the old ones until it overwrites or edits them. ([#29](https://github.com/polhem-dev/polhem/pull/29))
- Generated layouts and the `Auto` control type give a `DateTime` field the new `DateTimeEdit` instead of `DateEdit`.
  Layouts already saved keep `DateEdit`, which edits only the date and drops the time of day; change those fields to
  `DateTimeEdit` where the time matters. ([#33](https://github.com/polhem-dev/polhem/pull/33))

### Breaking API changes

- Public classes that are not extension points are sealed: data and definition types, attributes,
  exceptions, caches, service implementations without hooks, database dialects and builders, and leaf UI controls and
  Blazor components. Business objects, repository and collection bases, `TextEdit`, `DateEdit`, `ListView`, `FormView`,
  the connectors and `AuditRuleBusinessObject` stay open. `KeyCollectionBase<T>` is abstract.
  ([#13](https://github.com/polhem-dev/polhem/pull/13))
- Renamed: `IBeeContext` / `BeeContext` → `IBusinessObjectContext` / `BusinessObjectContext`;
  `BeeStringLocalizer<T>` → `LanguageResourceStringLocalizer<T>`; the audit log axis `LogBusinessObject`,
  `LogListResult`, `LogAggregateResult`, `LogApiConnector`, `LogListResponse`, `LogAggregateResponse`,
  `ILogListResponse`, `ILogAggregateResponse` and `LogActions` → `AuditLog…`; `PermissionAction` →
  `PermissionActions`; `NullAuditLogWriter` → `NullLogWriter`; `UserID` → `UserId` (also `userId` / `funcId`
  parameters); `AuditEntry.AccessToken` → `TokenFingerprint`.
  ([#5](https://github.com/polhem-dev/polhem/pull/5), [#10](https://github.com/polhem-dev/polhem/pull/10),
  [#11](https://github.com/polhem-dev/polhem/pull/11))
- Moved: `DeploymentAuthorizationService` to `Polhem.Business.Security`, `EmployeeContextResolver` to
  `Polhem.Business.Session`, `ElementCapabilityResolver`, `IElementCapabilityResolver` and `FieldCapability` to
  `Polhem.Api.Client.Permissions`, `FileEndpointStorage` to `Polhem.UI.Core`.
  ([#11](https://github.com/polhem-dev/polhem/pull/11), [#12](https://github.com/polhem-dev/polhem/pull/12))
- `LocalApiProvider` and the local connector constructors take the `IServiceProvider`;
  `ApiClientInfo.LocalServiceProvider`, `ApiClientInfo.ApiEncryptionKey` and `ApiClientInfo.UserTimeZoneId` are
  removed (use `ApiSessionContext`).
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11))
- Every public async member of the client surface, of the UI packages and `JsonRpcExecutor.ExecuteAsync` take a
  trailing `CancellationToken`; the connectors' action methods are virtual. Implementations of `IUIViewService` and
  overrides of the protected `Resolve*Async` hooks of `FormView` and `ListView` change their signatures.
  ([#12](https://github.com/polhem-dev/polhem/pull/12), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `IReplayWindowStore` is one atomic `TryAcceptAsync`, so a store shared by several nodes can be implemented.
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- Custom payload codecs are added with `ApiServiceOptions.RegisterPayloadCodec`; the undeclared default stays
  MessagePack and cannot be replaced. ([#10](https://github.com/polhem-dev/polhem/pull/10))
- Cached database-dependent types (`CompanyInfo`, `DepartmentTree`, `ApiKeyInfo` and the like) have init-only
  properties; the session's company scope is one immutable `SessionCompanyScope`.
  ([#8](https://github.com/polhem-dev/polhem/pull/8), [#10](https://github.com/polhem-dev/polhem/pull/10))
- `ICacheDataSourceProvider.GetCompanyAuditRules` has no default implementation.
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- `PolhemLoginPanel` label parameters and `DynamicGrid.EmptyText` are `string?`; `null` shows the localized text.
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- Wire: `CreateSessionRequest.userID` is `userId` and has no `oneTime`; `LoginResponse` gains `culture`; the audit log
  response types are renamed. `polhem-connector-js` follows.
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11),
  [#15](https://github.com/polhem-dev/polhem/pull/15))
- `IFormRuleProcessor` gains `ApplyNewRowDefaults`; another implementation must add it.
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- Tunable limits and defaults are `static readonly` instead of `const`: `ApiKeyFormat.MinSysIdLength`,
  `ApiKeyFormat.MaxSysIdLength`, `LoginAttemptTracker.DefaultLockoutMinutes`, `DefaultMaxFailedAttempts` and
  `DefaultMaxTrackedAccounts`, `CurrencySettings.FallbackRounding`, `UnitSettings.FallbackDecimals`,
  `ApiKeyCache.AbsoluteMinutes` and `NegativeMinutes`, `RowEditPanel.CompactWidthThreshold` and
  `FormView.DefaultCompactWidthThreshold`. Code that uses them in a constant expression must change.
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `ExecuteAsync<T>` of `SystemApiConnector` and `AuditLogApiConnector` is protected; `FormApiConnector.ExecuteAsync<T>`
  stays public for a form's own actions. ([#26](https://github.com/polhem-dev/polhem/pull/26))

### Removed

- The tracing subsystem (`Polhem.Base.Tracing`, `SysInfo.TraceListener`).
  ([#10](https://github.com/polhem-dev/polhem/pull/10))
- The serialize state: `IObjectSerialize`, `IObjectSerializeEmpty`, `SerializeState`, `SerializationUtilities`.
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- Compatibility leftovers: ignored constructor parameters and overloads, `JsonCodec`'s `includeTypeName`,
  `TableSchemaBuilder.Compare` and the legacy schema comparison path (`DbUpgradeAction`), the Hosting factories'
  constructor fallbacks, the one-time session flag, the synchronous `JsonRpcExecutor.Execute`,
  `BusinessObject.SessionInfo`, and `ClientInfo.ClientSettings`.
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#12](https://github.com/polhem-dev/polhem/pull/12))
- Types and members without callers: `DateInterval`, `IPValidator`, `DataTableComparer`, `Dictionary<T>`,
  `DefaultBoTypeResolver`, `VersionInfo`, `SysInfo.IsToolMode`, `SysInfo.IsSingleFile` and several pure wrappers.
  ([#10](https://github.com/polhem-dev/polhem/pull/10))
- Implementation types that are now internal: the Hosting background services, `NoEncryptionEncryptor`,
  `NoCompressionCompressor`, the payload converters, `HttpUtilities`, `ILMapper<T>`, `XmlSerializerCache`,
  `ReplayWindow`, `BackendDefaultTypes`, and the framework repository implementations (use the `I*Repository`
  interfaces).
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11),
  [#12](https://github.com/polhem-dev/polhem/pull/12))
- The unused settings types `ClientSettings`, `EndpointItem` and `EndpointItemCollection`.
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- `StringHashSet` and the public setter of `SysInfo.Version`. ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `ProgramSettingsFormat` and `dotnet polhem defines split-menu`, which converted the nested `ProgramSettings` layout
  of earlier Bee.NET versions. ([#28](https://github.com/polhem-dev/polhem/pull/28))

### Added

- `dotnet polhem keys protect`. ([#10](https://github.com/polhem-dev/polhem/pull/10))
- `GetFormSchemaAsync`, `GetFormLayoutAsync`, `GetLanguageAsync` and `GetCommonConfigurationAsync` on the connectors.
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- Localization: `LanguageFallback`, `MenuLocalizer`, `FrameworkLanguageService`, `PolhemMessages`, `PolhemUIText`,
  `ILocalizableMessage`, and English defaults with `zh-TW` translations for the Avalonia and Blazor UI text.
  `FormView`, `ListView` and `LookupDialog` localize definitions through `ClientInfo.DefinitionLoader` by default.
  ([#15](https://github.com/polhem-dev/polhem/pull/15), [#22](https://github.com/polhem-dev/polhem/pull/22))
- `AuthenticationRequiredException`, `IAuditLogSink` as a replaceable service, `PagingOptions.MaxPageSize`,
  `DataRowExtensions.RewriteVersions` and `FormDataGuard.TryGetRowId`.
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6),
  [#8](https://github.com/polhem-dev/polhem/pull/8), [#14](https://github.com/polhem-dev/polhem/pull/14))
- ADR-046 records the API policies for 1.0. ([#12](https://github.com/polhem-dev/polhem/pull/12))
- `RequiredFieldCheck` and `MissingRequiredField`, the required-field rule the server and the UI heads share, with the
  messages `PolhemMessages.SaveFieldRequired`, `SaveDetailFieldRequired` and `PolhemUIText.RequiredFieldsEmpty`.
  ([#26](https://github.com/polhem-dev/polhem/pull/26), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `IDefineStorage.GetFormSchemaIds` and `FileUtilities.EnsureFileExists`.
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `PolhemBlazorOptions.UseDefinitionLoader` and `PolhemApiConnectorFactory.CreateDefinitionLoader`.
  ([#27](https://github.com/polhem-dev/polhem/pull/27))
- `FormDataObject.RowEditFieldChanged` on Avalonia. ([#30](https://github.com/polhem-dev/polhem/pull/30))
- `ControlType.DateTimeEdit`, with the Avalonia `DateTimeEdit` editor and a `datetime-local` input in Blazor;
  `FormValueBinding.TryGetListItemText`, a `GridControl.Bind` overload that takes the `FormTable`, and
  `DynamicGrid.FormTable`. ([#33](https://github.com/polhem-dev/polhem/pull/33))

### Performance

- Definition cache hits check their source file at most once per second per entry.
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- Gzip compresses at the fastest level by default; `AesCbcHmacCryptor` uses span APIs with the same output format.
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- Per-request reflection is cached, and the Avalonia heads share one expression evaluator.
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- The audit batch writer persists a batch in one transaction. ([#14](https://github.com/polhem-dev/polhem/pull/14))
- `DataTable` over MessagePack is smaller and faster to serialize (see *Changed behaviour*).
  ([#12](https://github.com/polhem-dev/polhem/pull/12))

### Platform support

- Polhem supports untrimmed and partial-trim builds. `POLHEM9004` warns when a project publishes with NativeAOT, trims
  with `TrimMode=full` or disables System.Text.Json reflection (`PolhemSuppressTrimSupportWarning=true` silences it).
  ([#17](https://github.com/polhem-dev/polhem/pull/17))
- `Polhem.Expressions` ships an ILLink descriptor, so the default mobile trim keeps the members expressions use.
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- `LookupDialog` and `RowEditDialog` open a native window only on desktop and use the overlay host elsewhere, so
  lookups work on iOS, Android and the browser. ([#7](https://github.com/polhem-dev/polhem/pull/7))
- The MessagePack value formatter handles Polhem enums and nested `ParameterCollection`s without dynamic code.
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- The HTTP client uses the platform default handler on the browser, Android and Apple mobile heads.
  ([#17](https://github.com/polhem-dev/polhem/pull/17))
- An expression with more than two variables no longer terminates the app on iOS and Mac Catalyst:
  `DynamicExpressoEvaluator` compiles each expression to one delegate over an object array, which needs no dynamic
  code. ([#30](https://github.com/polhem-dev/polhem/pull/30))
- Desktop heads on macOS and Linux connect in Local mode: `FileUtilities.IsLocalPath` accepts paths fully qualified on
  the current OS, not only Windows drive and UNC paths. ([#29](https://github.com/polhem-dev/polhem/pull/29))
- On phones the lookup and row-edit overlays fit the screen, stay clear of the safe areas and the on-screen keyboard,
  and keep their buttons outside the scrolled content. ([#33](https://github.com/polhem-dev/polhem/pull/33))

### Fixed

- Plain `GetList` with a valued filter failed at the SQL parameter, and the client read Plain `DataTable` results as
  empty tables. ([#9](https://github.com/polhem-dev/polhem/pull/9))
- Concurrent saves and deletes could fail while a cached definition was being serialized; cache fills that raced an
  invalidation could keep stale values. ([#8](https://github.com/polhem-dev/polhem/pull/8))
- Saving a language resource from a client always failed. ([#17](https://github.com/polhem-dev/polhem/pull/17))
- The shipped AuditRule form showed empty mode dropdowns without a definition loader.
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- Blazor: sign-in did not set the circuit's time zone, and `FormDataObject` did not resume on the circuit context.
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- `SaveDatabaseSettings` encrypted the passwords of the instance it was given instead of a copy.
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- A nil element in a `KeyCollectionBase` payload is rejected instead of skipped.
  ([#9](https://github.com/polhem-dev/polhem/pull/9), [#14](https://github.com/polhem-dev/polhem/pull/14))
- Rebuilding a table (every column change on SQLite) narrowed columns without `UpgradeOptions.AllowColumnNarrowing`.
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- Relation joins used a blank table name when a form table declared no `DbTableName`; they fall back to `TableName`.
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- The `zh-TW` resources were not found for the `zh-Hant-TW` culture macOS and iOS report; `LanguageFallback` follows
  `zh-Hant` with `zh-TW` and `zh-Hans` with `zh-CN`. ([#26](https://github.com/polhem-dev/polhem/pull/26))
- The refusal of a detail row that belongs to a record the save does not carry was not localized
  (`PolhemMessages.PermissionDetailOutOfScope`). ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `FormView` applied permission capabilities to a `Layout` the host assigned instead of to a copy.
  ([#27](https://github.com/polhem-dev/polhem/pull/27))
- The Avalonia card list at phone width showed a time part on dates and ignored number formats; it formats values
  like the grid. ([#29](https://github.com/polhem-dev/polhem/pull/29))
- Following the Blazor Server README in Remote mode failed with a 401; it and `UseRemoteProvider` now say that
  `ApiClientInfo.ApiKey` must be set. ([#29](https://github.com/polhem-dev/polhem/pull/29))
- The Avalonia row-edit overlay did not recompute computed fields until the row was confirmed; it recomputes them
  while the row is edited, and Cancel restores them. ([#30](https://github.com/polhem-dev/polhem/pull/30))
- Lists (grids, card lists, lookups and detail grids, in Avalonia and Blazor) showed a drop-down field's stored value;
  they show its localized list-item text, and booleans show a check box in the card list and a localized yes or no
  elsewhere. ([#33](https://github.com/polhem-dev/polhem/pull/33))
- The Blazor `DateEdit` showed a blank box for a value with a time of day; it shows the date.
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

### Samples and tools

- The samples keep their business tables in the company scope and enter a demo company after sign-in.
  ([#23](https://github.com/polhem-dev/polhem/pull/23))
- Northwind adds a zh-TW demo account (`demo-tw`) and keeps its README screenshots in the repository.
  ([#23](https://github.com/polhem-dev/polhem/pull/23))
- DefineEditor shows a File menu inside the window on Windows and Linux.
  ([#23](https://github.com/polhem-dev/polhem/pull/23))
- The samples' `Employee` and `Department` forms are renamed `Staff` (`ft_staff`, `ft_staff_phone`) and `Team`
  (`ft_team`), so they no longer replace the framework's reserved forms of those names.
  ([#25](https://github.com/polhem-dev/polhem/pull/25))
- Northwind ships `zh-TW` messages for its order rules. ([#25](https://github.com/polhem-dev/polhem/pull/25))

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.3.1...HEAD
[1.3.1]: https://github.com/polhem-dev/polhem/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.3.0
[1.2.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.2.0
[1.1.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
