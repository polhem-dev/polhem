# Changelog

[繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/). Each version lists its changes here in one line each;
the reasons and the background are in its detailed notes under [`docs/changelogs/`](docs/changelogs/).

## [Unreleased]

## [1.0.0] - Unreleased

> Polhem continues [Bee.NET](https://github.com/jeff377/bee-library) under a new name. Polhem 1.0.0 is Bee.NET 4.33.0,
> the last Bee.NET release, renamed, plus the changes listed below. How to move an application over is described in
> [Migrating from Bee.NET](README.md#migrating-from-beenet).

📄 Full notes and background: [docs/changelogs/1.0.0.md](docs/changelogs/1.0.0.md)

### Renamed from Bee.NET

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
  reused.
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
  package.

### Security

- Access tokens are no longer stored: `st_session` is keyed by a SHA-256 derived key (`AccessTokenHasher`), and the log
  tables keep a short token fingerprint (`token_fingerprint`) instead of the `access_token` column.
- Passwords are hashed with PBKDF2-SHA256 at 600,000 iterations; weaker stored hashes are replaced at the next
  successful sign-in, and hashes in the PBKDF2-SHA1 format inherited from Bee.NET no longer verify.
- An unknown user name runs a decoy hash, so sign-in timing does not reveal whether the account exists, and the login
  attempt tracker stays bounded.
- Record scope is also checked on the stored row an update or delete targets, on modified and deleted detail rows, and
  on the values a saved master row leaves behind.
- `GetList` and `GetCount` accept filter and sort fields only when the form declares them, and never on protected
  fields; `GetLookup` applies the Read record scope (`FormBusinessObject.LookupAppliesRecordScope` opts out).
- Remote `GetDefine` serves only an explicit list of definition types.
- Business objects are created with the ProgId casing declared in `ProgramSettings`, so audit rules match however a
  caller spells the ProgId.
- Encoded and Encrypted requests are decoded into the parameter type of the resolved action, the depth limit applies
  to nested filters on both codecs, and actions resolve only to public, non-generic, one-parameter instance methods
  that are not accessors (`JsonRpcExecutor.IsResolvableAction`, also checked by `POLHEM3001`).
- Messages of BCL exceptions no longer reach remote callers; each error code has a fixed message and the original is
  logged through `JsonRpcExecutor.Logger`.
- `CreateSession` accepts only local calls. `CreateApiKey`, `SetApiKeyEnabled` and `SetApiKeyExpiry` are
  replay-protected.
- The database anomaly log is readable only by a deployment administrator.
- The client refuses a server-advertised "none" encryptor unless it runs in debug mode itself, and no longer adopts the
  server's debug flag or type namespaces.
- The master key file and the client's `apikey.txt` are written with owner-only permissions; a warning is logged when
  database passwords exist without a `ConfigEncryptionKey`.
- DDL escapes SQL Server string defaults and requires non-string defaults to be literals of their type; connection
  string placeholders are resolved with `DbConnectionStringBuilder` (`ConnectionStringTemplate`).

### Changed behaviour

- A request without an `Authorization` header is an anonymous call: `[ApiAccessControl]` alone decides, and a method
  that needs a session answers JSON-RPC `-32001` (Unauthorized) instead of HTTP 401. The client turns it into an
  `UnauthorizedAccessException`, and `RemoteApiProvider` sends no `Authorization` header before sign-in.
- `GetList` without paging returns the first page, capped at `PagingOptions.MaxPageSize`.
- English is the base language of every built-in definition file and UI text; Chinese moves to the shipped `zh-TW`
  language resources. Captions, enums, menus, rule messages and framework text share one fall-back chain
  (`LanguageFallback`): the requested culture, its parents, `CommonConfiguration.DefaultLanguage`, then the base text.
- The login response carries the user's culture, which the client adopts. `CommonConfiguration.DefaultLang` and
  `BackendConfiguration.DefaultLanguage` merge into `CommonConfiguration.DefaultLanguage` (default `zh-TW`).
- Framework messages meant for end users carry a key and arguments (`UserMessageException`) and are resolved in the
  session culture; form rule messages resolve `{ProgId}.Rule.{RuleId}.Message`.
- Numbers and dates are displayed and parsed in the user's culture; the wire stays culture-invariant.
- `ValueUtilities.CBool` accepts only `1`, `T`, `TRUE`, `Y` and `YES` (ignoring case) as true; the Chinese words for
  yes and true are no longer recognized.
- `BackendComponents` entries default to blank, which selects the framework default. A wrong `CacheProvider` or
  component type name fails at startup and names the setting.
- A type name that cannot be resolved and starts with `Bee.` gets a migration hint (`BeeNameHint`), and a missing
  `POLHEM_MASTER_KEY` says so when `BEE_MASTER_KEY` is set.
- The client stores the endpoint and the API key in `endpoint.txt` and `apikey.txt` under the per-user local
  application data folder (`FileEndpointStorage`, now in `Polhem.UI.Core`) instead of a settings file beside the
  assembly.
- Over MessagePack, a `DataTable` is written as one column table and positional rows. JSON and Plain are unchanged.
- Every wire member whose initializer is not the CLR default is always written, so an absent member means the CLR
  default on every codec. Plain requests bind object-typed filter and parameter values by JSON kind.
- Serializing a cached definition no longer changes it: empty collections are omitted through get-only `XSpecified`
  properties instead of a per-object serialize state.
- Startup logs a warning when methods require `ApiReplayProtection.UniqueSequence` while `RequireWireFrame` is off,
  and lists the forms that declare no permission model.
- Default values of `Short`, `Long`, `Decimal` and `Binary` fields are typed and non-null.

### Breaking API changes

- Public classes that are not extension points are sealed: data and definition types, attributes,
  exceptions, caches, service implementations without hooks, database dialects and builders, and leaf UI controls and
  Blazor components. Business objects, repository and collection bases, `TextEdit`, `DateEdit`, `ListView`, `FormView`,
  the connectors and `AuditRuleBusinessObject` stay open. `KeyCollectionBase<T>` is abstract.
- Renamed: `IBeeContext` / `BeeContext` → `IBusinessObjectContext` / `BusinessObjectContext`;
  `BeeStringLocalizer<T>` → `LanguageResourceStringLocalizer<T>`; the audit log axis `LogBusinessObject`,
  `LogListResult`, `LogAggregateResult`, `LogApiConnector`, `LogListResponse`, `LogAggregateResponse`,
  `ILogListResponse`, `ILogAggregateResponse` and `LogActions` → `AuditLog…`; `PermissionAction` →
  `PermissionActions`; `NullAuditLogWriter` → `NullLogWriter`; `UserID` → `UserId` (also `userId` / `funcId`
  parameters); `AuditEntry.AccessToken` → `TokenFingerprint`.
- Moved: `DeploymentAuthorizationService` to `Polhem.Business.Security`, `EmployeeContextResolver` to
  `Polhem.Business.Session`, `ElementCapabilityResolver`, `IElementCapabilityResolver` and `FieldCapability` to
  `Polhem.Api.Client.Permissions`, `FileEndpointStorage` to `Polhem.UI.Core`.
- `LocalApiProvider` and the local connector constructors take the `IServiceProvider`;
  `ApiClientInfo.LocalServiceProvider`, `ApiClientInfo.ApiEncryptionKey` and `ApiClientInfo.UserTimeZoneId` are
  removed (use `ApiSessionContext`).
- Every public async member of the client surface and `JsonRpcExecutor.ExecuteAsync` take a trailing
  `CancellationToken`; the connectors' action methods are virtual.
- `IReplayWindowStore` is one atomic `TryAcceptAsync`, so a store shared by several nodes can be implemented.
- Custom payload codecs are added with `ApiServiceOptions.RegisterPayloadCodec`; the undeclared default stays
  MessagePack and cannot be replaced.
- Cached database-dependent types (`CompanyInfo`, `DepartmentTree`, `ApiKeyInfo` and the like) have init-only
  properties; the session's company scope is one immutable `SessionCompanyScope`.
- `ICacheDataSourceProvider.GetCompanyAuditRules` has no default implementation.
- `PolhemLoginPanel` label parameters and `DynamicGrid.EmptyText` are `string?`; `null` shows the localized text.
- Wire: `CreateSessionRequest.userID` is `userId` and has no `oneTime`; `LoginResponse` gains `culture`; the audit log
  response types are renamed. `polhem-connector-js` follows.

### Removed

- The tracing subsystem (`Polhem.Base.Tracing`, `SysInfo.TraceListener`).
- The serialize state: `IObjectSerialize`, `IObjectSerializeEmpty`, `SerializeState`, `SerializationUtilities`.
- Compatibility leftovers: ignored constructor parameters and overloads, `JsonCodec`'s `includeTypeName`,
  `TableSchemaBuilder.Compare` and the legacy schema comparison path (`DbUpgradeAction`), the Hosting factories'
  constructor fallbacks, the one-time session flag, the synchronous `JsonRpcExecutor.Execute`,
  `BusinessObject.SessionInfo`, and `ClientInfo.ClientSettings`.
- Types and members without callers: `DateInterval`, `IPValidator`, `DataTableComparer`, `Dictionary<T>`,
  `DefaultBoTypeResolver`, `VersionInfo`, `SysInfo.IsToolMode`, `SysInfo.IsSingleFile` and several pure wrappers.
- Implementation types that are now internal: the Hosting background services, `NoEncryptionEncryptor`,
  `NoCompressionCompressor`, the payload converters, `HttpUtilities`, `ILMapper<T>`, `XmlSerializerCache`,
  `ReplayWindow`, `BackendDefaultTypes`, and the framework repository implementations (use the `I*Repository`
  interfaces).

### Added

- `dotnet polhem keys protect`.
- `GetFormSchemaAsync`, `GetFormLayoutAsync`, `GetLanguageAsync` and `GetCommonConfigurationAsync` on the connectors.
- Localization: `LanguageFallback`, `MenuLocalizer`, `FrameworkLanguageService`, `PolhemMessages`, `PolhemUIText`,
  `ILocalizableMessage`, and English defaults with `zh-TW` translations for the Avalonia and Blazor UI text.
  `FormView`, `ListView` and `LookupDialog` localize definitions through `ClientInfo.DefinitionLoader` by default.
- `AuthenticationRequiredException`, `IAuditLogSink` as a replaceable service, `PagingOptions.MaxPageSize`,
  `DataRowExtensions.RewriteVersions` and `FormDataGuard.TryGetRowId`.
- ADR-046 records the API policies for 1.0.

### Performance

- Definition cache hits check their source file at most once per second per entry.
- Gzip compresses at the fastest level by default; `AesCbcHmacCryptor` uses span APIs with the same output format.
- Per-request reflection is cached, and the Avalonia heads share one expression evaluator.
- The audit batch writer persists a batch in one transaction.
- `DataTable` over MessagePack is smaller and faster to serialize (see *Changed behaviour*).

### Platform support

- Polhem supports untrimmed and partial-trim builds. `POLHEM9004` warns when a project publishes with NativeAOT, trims
  with `TrimMode=full` or disables System.Text.Json reflection (`PolhemSuppressTrimSupportWarning=true` silences it).
- `Polhem.Expressions` ships an ILLink descriptor, so the default mobile trim keeps the members expressions use.
- `LookupDialog` and `RowEditDialog` open a native window only on desktop and use the overlay host elsewhere, so
  lookups work on iOS, Android and the browser.
- The MessagePack value formatter handles Polhem enums and nested `ParameterCollection`s without dynamic code.
- The HTTP client uses the platform default handler on the browser, Android and Apple mobile heads.

### Fixed

- Plain `GetList` with a valued filter failed at the SQL parameter, and the client read Plain `DataTable` results as
  empty tables.
- Concurrent saves and deletes could fail while a cached definition was being serialized; cache fills that raced an
  invalidation could keep stale values.
- Saving a language resource from a client always failed.
- The shipped AuditRule form showed empty mode dropdowns without a definition loader.
- Blazor: sign-in did not set the circuit's time zone, and `FormDataObject` did not resume on the circuit context.
- `SaveDatabaseSettings` encrypted the passwords of the instance it was given instead of a copy.
- A nil element in a `KeyCollectionBase` payload is rejected instead of skipped.

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
