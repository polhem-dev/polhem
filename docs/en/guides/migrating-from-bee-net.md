# Migrating from Bee.NET

[繁體中文](../../zh-TW/guides/migrating-from-bee-net.md) · [← Docs Index](../README.md)

Polhem continues the [Bee.NET](https://github.com/jeff377/bee-library) framework (`Bee.*` packages, last released as 4.33.0) under a new name. Polhem 1.0.0 is Bee.NET 4.33.0
renamed, plus the changes of a pre-release review that the
[CHANGELOG](../../../CHANGELOG.md) lists. Every name that contained `Bee` was
renamed, and the old names are not recognized: there is no compatibility layer. This document covers what an upgrade has
to change.

## Packages, namespaces and types

- Every `Bee.<Name>` package becomes `Polhem.<Name>`, and the split into packages is unchanged: `Bee.Hosting` becomes
  `Polhem.Hosting`, and so on for each package in the [package tables of the README](../../../README.md#-assembly). The exception is `Bee.Api.AspNetCore`: its
  successor `Polhem.Api.AspNetCore` was removed in 1.2.0, and a host serves the API with `Polhem.JsonRpc.AspNetCore`
  (see the [CHANGELOG](../../../CHANGELOG.md)). Namespaces follow the same pattern:
  `Bee.Definition.Forms` becomes `Polhem.Definition.Forms`.
- The one exception is `Bee.Base`, which becomes `Polhem.Core`, the package and its namespaces alike:
  `Bee.Base.Serialization` becomes `Polhem.Core.Serialization`. Polhem 1.0.0 still called it `Polhem.Base`; the
  [CHANGELOG](../../../CHANGELOG.md) entry for 1.1.0 explains the rename.
- `Bee` in a type or member name becomes `Polhem`: `AddBeeFramework` becomes `AddPolhemFramework`, `BeeLoginPanel`
  becomes `PolhemLoginPanel`. `UseBeeFramework` has no counterpart from 1.2.0: call
  `services.AddPolhemApiKeyGateCheck()` instead.
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
| `ApiClientInfo` (`Endpoint`, `ApiKey`, `PayloadOptions`, `DefaultLanguage`, `ConnectType`) | The members of `PolhemApiClient`, created with `PolhemApiClient.CreateRemote(endpoint, apiKey)` (`IsLocal` instead of `ConnectType`); `SupportedConnectTypes` moved to `ClientInfo` (`Polhem.UI.Core`) |
| `ApiClientInfo.ApiEncryptionKey`, `ApiClientInfo.UserTimeZoneId` | `PolhemApiClient.Session.Credentials`, set by `LoginAsync` |
| `ApiClientInfo.LocalServiceProvider` | Pass the `IServiceProvider` to `PolhemApiClient.CreateLocal` |
| `new SystemApiConnector(endpoint, accessToken)`, `new FormApiConnector(endpoint, accessToken, progId)` | `client.System`, `client.Form(progId)` of a `PolhemApiClient`, which keeps the access token after `LoginAsync` |
| `Bee.UI.Avalonia.Storage.FileEndpointStorage` | `Polhem.UI.Core.FileEndpointStorage` |
| `ElementCapabilityResolver`, `IElementCapabilityResolver`, `FieldCapability` in `Bee.UI.Core.Permissions` | The same types in `Polhem.Api.Client.Permissions` |
| `DeploymentAuthorizationService`, `EmployeeContextResolver` in `Bee.ObjectCaching.Services` | `Polhem.Business.Security.DeploymentAuthorizationService`, `Polhem.Business.Session.EmployeeContextResolver` |
| `JsonRpcExecutor.Execute`, `ApiServiceController` | Removed in 1.2.0: the dispatcher of `Polhem.JsonRpc.Server`, published with `app.MapJsonRpc("/api")` |
| `BusinessObject.SessionInfo` | `SessionInfoService.Get(AccessToken)` inside the business object |
| `Bee.Base.Tracing` | Removed |

Classes that are not extension points are sealed, the framework repository implementations are internal (use the
`I*Repository` interfaces), and public async client members take a trailing `CancellationToken`. The CHANGELOG lists
every change.

The compiler reports every place in your code that still uses these names.

## Names the compiler does not check

These are strings. A build with the old names succeeds, and the problem only shows at run time or not at all.

| What | Bee.NET | Polhem | With the old name |
|------|---------|--------|-------------------|
| Type names in definition files: `BusinessObject` and `Repository` in `ProgramSettings.xml`, the elements under `BackendConfiguration/Components` in `SystemSettings.xml`, and type names in your own code | `Bee.Business.AuditLog.LogBusinessObject, Bee.Business` | `Polhem.Business.AuditLog.AuditLogBusinessObject, Polhem.Business` | The type is not found at run time |
| Default environment variable of the master key | `BEE_MASTER_KEY` | `POLHEM_MASTER_KEY` | Only a `SystemSettings.xml` that leaves the variable name to the default is affected: the master key is not found at startup. A `MasterKeySource` whose `Value` names the variable keeps using that name; the defaults that `dotnet bee defines materialize` wrote name `BEE_MASTER_KEY` |
| Analyzer diagnostic IDs in `.editorconfig`, `#pragma warning`, `NoWarn` and `[SuppressMessage]` | `BEE1001` | `POLHEM1001` (same numbers) | The setting is silently ignored |
| MSBuild properties for definition file checks | `BeeDefinitionFilesGlob`, `BeeRequireDefinitionFiles`, `BeeAnalyzeDefinitionFiles` | `PolhemDefinitionFilesGlob`, `PolhemRequireDefinitionFiles`, `PolhemAnalyzeDefinitionFiles` | The setting is silently ignored and the default applies |
| CSS classes of the Blazor components | `bee-dynamic-form`, `bee-dynamic-grid`, `bee-form-page`, `bee-login-panel` | `polhem-dynamic-form`, `polhem-dynamic-grid`, `polhem-form-page`, `polhem-login-panel` | Your own style rules no longer apply |
| Logging categories, such as filters under `Logging:LogLevel` | `Bee.Api.AspNetCore` | `Polhem.Hosting.ApiKeys.ApiKeyGateWarningService` | The filter no longer matches |
| The `Exception.Data` key of `SerializationErrorData.FilePath` | `Bee.FilePath` | `Polhem.FilePath` | Code that reads the key finds nothing |

To find them, run this in the root of your repository:

```bash
grep -rnE "Bee\.|Bee[A-Z]|BEE_|BEE[0-9]{4}|dotnet[- ]bee|bee-(dynamic|form|login)" --include="*.cs" --include="*.razor" --include="*.css" --include="*.xml" --include="*.json" --include="*.csproj" --include="*.props" --include="*.targets" --include=".editorconfig" --include="*.yml" --include="*.yaml" --include="*.sh" --include="*.js" --include="*.ts" --include="Dockerfile" .
```

## Settings

- **Components**: an entry under `BackendConfiguration/Components` in `SystemSettings.xml` may be left blank, which
  selects the framework default. Clear the entries that only repeat a Bee.NET default instead of renaming them.
- **Default language**: `CommonConfiguration/DefaultLanguage` (default `zh-TW`) is the culture of users who have no
  `st_user.culture` of their own and the last language fall-back. It replaces `CommonConfiguration/DefaultLang` and
  `BackendConfiguration/DefaultLanguage`, which are no longer read.

## What changes when you switch

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
