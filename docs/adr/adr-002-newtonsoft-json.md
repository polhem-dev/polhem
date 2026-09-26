# ADR-002: Choosing and migrating the JSON serialization library

[繁體中文](adr-002-newtonsoft-json.zh-TW.md)

## Status

**Superseded** — the original decision (use Newtonsoft.Json) was migrated away from in 2026-04, and `System.Text.Json`
is used now. This ADR is kept to record the context of the choice and the result of the migration.

| Stage | Library | Period |
|-------|---------|--------|
| Original decision | Newtonsoft.Json | From the framework's early days to 2026-04 |
| Current | System.Text.Json | From 2026-04 |

## Context

The framework uses three serialization formats, each with a clear purpose:

| Format | Purpose | Scenario |
|--------|---------|----------|
| **XML** | Storing and representing definition data | Persistence and transport representation of complex types such as FormSchema and SystemSettings (when definition data is transmitted, it is first serialized to an XML string, then carried by MessagePack or JSON) |
| **MessagePack** | Front end to back end transport within the system | API Payload (see [ADR-004](adr-004-messagepack-payload.md)) |
| **JSON** | Integration with external systems | Third-party system integration, the JSON-RPC envelope, configuration files |

For JSON serialization, the .NET ecosystem has two main options:

1. **Newtonsoft.Json** (Json.NET): third-party, the most complete feature set
2. **System.Text.Json** (STJ): Microsoft's official library, built into .NET Core 3.0+, with higher performance

## Original decision (superseded)

Use `Newtonsoft.Json` as the only JSON serialization library.

### The reasons at the time

- **DataSet serialization (the main reason)**: the framework uses DataSet as the cross-layer DTO (see
  [ADR-001](adr-001-dataset-as-dto.md)). Newtonsoft.Json supports DataSet / DataTable serialization natively, while
  System.Text.Json does not and would need hand-written converters.
- **netstandard2.0 compatibility**: the core packages targeted netstandard2.0, where System.Text.Json has limited
  features.
- **Support for complex serialization**: the framework needs advanced features such as custom type binding
  (`JsonSerializationBinder`), polymorphic serialization and special collection handling.
- **Role as external integration**: in this framework JSON's role is integration with external systems, not
  high-frequency internal transport (MessagePack handles that). The performance difference has limited overall
  impact, and a complete feature set matters more.

### Why it was superseded

Both main obstacles have been removed:

- **netstandard2.0 has been dropped**: every project migrated to the single target framework net10.0 on 2026-04-14
  (see [ADR-006](adr-006-dual-target-framework.md)), and STJ is fully featured on net10.0.
- **The DataSet security constraint is under control**: restricting DataSet column types to the `FieldDbType` enum
  (13 fixed types, all basic types STJ supports natively) removes the security risk of arbitrary `System.Type`s.

In addition, STJ is built into the framework (no third-party dependency), performs better and has continued official
investment, so the time was right to migrate.

## Current decision

Use `System.Text.Json` as the only JSON serialization library; bringing `Newtonsoft.Json` back in is forbidden.

The encapsulation point is `Polhem.Base/Serialization/JsonCodec.cs`. By default it uses camelCase property names and
indented output, and it registers the framework's custom converters:

| Converter | Purpose |
|-----------|---------|
| `DataSetJsonConverter` | Restores the DataSet structure (Tables, Relations, PrimaryKey, Rows including RowState) |
| `DataTableJsonConverter` | Restores the DataTable structure (Columns including the `FieldDbType` mapping, Rows including RowState) |
| `JsonStringEnumConverter` | Writes enums as strings, for readability in external integration |

## Summary of the migration result

### Phase 1: custom converters

| Item | Where it landed |
|------|-----------------|
| `DataSetJsonConverter` / `DataTableJsonConverter` | `src/Polhem.Base/Serialization/`, with a fixed `FieldDbType` → .NET type mapping so that arbitrary `System.Type`s are never serialized |
| `ApiPayload` type information | Now carried by the existing `ApiPayload.TypeName` property, no longer relying on Newtonsoft.Json's automatic `$type` injection; whitelist validation still uses `SysInfo.IsTypeNameAllowed()` |
| `FilterNodeCollection` converter | Rewritten from `JArray` / `JObject` to `JsonDocument` / `JsonElement` |

### Phase 2: attributes and core modules

| Item | Result |
|------|--------|
| `[JsonIgnore]` | Namespace changed from `Newtonsoft.Json` → `System.Text.Json.Serialization` |
| `[JsonProperty("x")]` | Changed to `[JsonPropertyName("x")]` |
| `NullValueHandling.Include` (JsonRpcRequest/Response) | Replaced by `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]` or the global `DefaultIgnoreCondition` |
| `DefaultValueHandling.Include` (FormField, DbField) | Replaced by `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]` |
| `SerializeFunc.cs` | Refactored into `JsonCodec.cs`, implemented internally with `JsonSerializer` + `JsonSerializerOptions` |

### Phase 3: cleanup

| Item | Result |
|------|--------|
| `JsonSerializationBinder.cs` | Removed (the cross-runtime name mapping `mscorlib` ↔ `System.Private.CoreLib` is no longer needed with the single net10.0 target) |
| `Newtonsoft.Json` NuGet dependency | Removed from every `*.csproj` |
| Serialization rule in `code-style.md` | Updated to "JSON serialization uses System.Text.Json" |

## Consequences

- All JSON operations go through `JsonCodec`; `JsonSerializer` is no longer called directly, so the framework's
  default options stay consistent
- The three serializations keep their separate roles: XML stores definitions, MessagePack carries internal payloads,
  JSON connects to external systems
- All three front-end repositories are new, so there is no JSON format compatibility issue with existing clients;
  the migration kept no backward compatibility layer
- With `JsonSerializationBinder` removed, the difference in type names across runtimes disappears naturally because
  the framework has a single net10.0 target
