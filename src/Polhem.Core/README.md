# Polhem.Base

> Cross-layer shared utility library: type conversion, cryptographic primitives, serialization, collections,
> ADO.NET helpers and the expression abstraction.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Infrastructure (bottom layer of the Polhem framework)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Type Conversion & String Utilities

- `ValueUtilities` -- safe type conversions (`CInt`, `CStr`, `CBool`, …). The temporal members
  (`CDateOnly`, `CDateTime`, `CTimeOnly`) return a nullable from their one-argument form and take
  an explicit fallback in the two-argument overload — an unparseable input yields `null` rather
  than a sentinel date that could reach a report. `CDateTime` also parses ROC (Minguo) date strings.
  `CBool` accepts language-neutral codes only (`1`, `T`, `TRUE`, `Y`, `YES`, ignoring case); see its
  XML documentation
- `FrameworkClock` -- "today" and "now" in a user's time zone: `Today(timeZoneId)` and
  `Now(timeZoneId)`, returned as `Unspecified` (never `Local`); a blank zone id means UTC.
  `Now(timeZoneId, DateTimeBasis)` picks UTC on the server side of the connector. System timestamps
  use `DateTime.UtcNow` directly
- `StringExtensions` / `StringUtilities` -- string splitting, trimming and case-insensitive comparison helpers
- `DateTimeExtensions` -- `DateTime` extension methods

### Cryptography & Security

- `AesCbcHmacCryptor` -- AES-256-CBC encryption with HMAC-SHA256 authentication (random IV per operation)
- `RsaCryptor` -- RSA asymmetric encryption
- `PasswordHasher` -- PBKDF2-SHA256 password hashing
- `ApiKeyHasher` / `AccessTokenHasher` -- hashing of API key secrets and of access tokens for storage
- `FileHashValidator` -- file integrity verification via SHA-256
- `AesCbcHmacKeyGenerator` -- cryptographic key generation

### Serialization & Compression

- `XmlCodec` -- XML serialization through `XmlSerializer`
- `JsonCodec` -- JSON serialization through `System.Text.Json` (camelCase)
- `Gzip` -- Gzip compression / decompression for payload handling

### Collections

- `CollectionBase<T>` / `KeyCollectionBase<T>` -- abstract base classes for the framework's (keyed) collections

### Data Access Helpers

- `DataTable` / `DataSet` / `DataRow` / `DataRowView` / `DataView` extension methods for simplified ADO.NET usage
  (`DataRowViewExtensions.GetFieldValue<T>` is the data-binding counterpart of `DataRowExtensions`)
- `FieldDbType` and `DbTypeConverter` -- database type mapping utilities

### Exceptions

- `UserMessageException` -- a message meant for the end user, carrying a key and arguments that are
  resolved in the user's culture; `ForbiddenException`, `AuthenticationRequiredException` and the
  company-scope exceptions sit next to it in `Exceptions/`

### Expression Abstraction

- `IExpressionEvaluator` -- evaluates an expression against a named variable set. The
  DynamicExpresso-backed implementation lives in `Polhem.Expressions`; the abstraction sits here so
  the definition and business layers can consume the engine without a third-party dependency
  ([ADR-038](../../maintainers/adr/adr-038-definition-dependency-boundary.md))
- `ExpressionPolicy` -- the shared type / null policy applied when feeding field values in, so a
  computed field yields the same result on the server and on a UI client
- `ExpressionEvaluationException` -- thrown when an expression cannot be parsed or compiled

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `ValueUtilities` | Safe type conversion with defaults |
| `FrameworkClock` | Today / now in a user's time zone |
| `StringExtensions` / `StringUtilities` | String splitting, trimming, comparison |
| `AesCbcHmacCryptor` | Authenticated symmetric encryption |
| `PasswordHasher` | Password hashing (PBKDF2-SHA256) |
| `XmlCodec` / `JsonCodec` | XML / JSON serialization |
| `IObjectSerializeFile` | An object bound to the file path it is serialized to |
| `IKeyObject` | Keyed entity interface used across layers |
| `UserMessageException` | End-user message with a localizable key |
| `IExpressionEvaluator` | Expression evaluation abstraction (implementation in `Polhem.Expressions`) |
| `ExpressionPolicy` | Shared type / null policy for expression variables |

## Design Conventions

- **Static utility classes** -- `ValueUtilities`, `StringUtilities` and `FileUtilities` expose functionality as static methods; no instance state.
- **Constant-time comparison** -- `CryptographicOperations.FixedTimeEquals` is used for HMAC / hash validation to prevent timing attacks.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

The folders group the source by feature: `Attributes/`, `Collections/`, `Data/`, `Exceptions/`, `Expressions/`,
`Security/` and `Serialization/`. General utilities (`ValueUtilities`, `FrameworkClock`, `StringUtilities`,
`FileUtilities`, `SysInfo`, `IKeyObject`, …) sit at the project root.
