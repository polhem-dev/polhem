# The Define/ settings tree

Five XML files (+ the TableSchema folder). They live under the host project's `Define/`, and `Program.cs` locates them
by walking up. Below is minimal content you can paste (a single-file SQLite dev setup).

## SystemSettings.xml — root settings

```xml
<?xml version="1.0" encoding="utf-8"?>
<SystemSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <CommonConfiguration>
    <Version>1.0.0</Version>
    <IsDebugMode>true</IsDebugMode>
    <!-- Your args/result namespaces; typeless serialization for Encoded/Encrypted relies on this allowlist -->
    <AllowedTypeNamespaces>Xxx.Server.Contracts</AllowedTypeNamespaces>
    <!-- The body codec is not set here: each request declares it in the envelope; undeclared means MessagePack (adr-044). -->
    <ApiPayloadOptions>
      <Compressor>gzip</Compressor>
      <Encryptor>aes-cbc-hmac</Encryptor>
    </ApiPayloadOptions>
  </CommonConfiguration>
  <BackendConfiguration>
    <LogOptions>
      <DbAccess>
        <Level>Warning</Level>
        <AffectedRowThreshold>10000</AffectedRowThreshold>
        <ResultRowThreshold>10000</ResultRowThreshold>
        <ExecutionTimeThreshold>300</ExecutionTimeThreshold>
      </DbAccess>
    </LogOptions>
    <SecurityKeySettings>
      <MasterKeySource>
        <Type>Environment</Type>
        <Value>POLHEM_MASTER_KEY</Value>
      </MasterKeySource>
    </SecurityKeySettings>
    <Components>
      <!-- Leave empty when the factory is overridden in code through DI -->
      <BusinessObjectProvider></BusinessObjectProvider>
    </Components>
  </BackendConfiguration>
  <FrontendConfiguration />
  <WebsiteConfiguration />
  <BackgroundServiceConfiguration />
</SystemSettings>
```

## DatabaseSettings.xml — logical DB id → connection string

`common` is **required by the framework** (st_session, st_cache_notify). `company` holds business data. A single-file
dev setup can share one .db file.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DatabaseSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Items>
    <DatabaseItem Id="common"  CategoryId="common"  DisplayName="Shared (SQLite)"
        DatabaseType="SQLite" ConnectionString="Data Source=xxx.db;Cache=Shared" />
    <DatabaseItem Id="company" CategoryId="company" DisplayName="Company (SQLite)"
        DatabaseType="SQLite" ConnectionString="Data Source=xxx.db;Cache=Shared" />
  </Items>
</DatabaseSettings>
```

## DbCategorySettings.xml — which tables belong to which category/DB

The seeder iterates over this file to create tables. Adding a table = a new `TableSchema` file + one `TableItem` here.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DbCategorySettings xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Categories>
    <DbCategory Id="company" DisplayName="Company Database">
      <Tables />   <!-- <TableItem Id="ft_xxx" /> ... -->
    </DbCategory>
  </Categories>
</DbCategorySettings>
```

## ProgramSettings.xml — program list + (optional) declarative BO binding

When progId→BO is bound by a **code resolver**, this file can stay empty (or hold only menu items). For
**declarative** binding:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ProgramSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Categories>
    <ProgramCategory Id="main" DisplayName="Main">
      <Items>
        <ProgramItem ProgId="Game" DisplayName="Game"
            BusinessObject="Xxx.Server.BusinessObjects.GameBO, Xxx.Server" />
        <!-- BusinessObject left empty → the framework default FormBusinessObject (definition-driven CRUD) -->
      </Items>
    </ProgramCategory>
  </Categories>
</ProgramSettings>
```
`BusinessObject` is an **assembly-qualified type name**: `"Namespace.Type, AssemblyName"`.

## TableSchema/

The physical DB schema, in one folder per category:
- `TableSchema/common/st_cache_notify.TableSchema.xml` (do not hand-write it: on first startup
  `Defaults.MaterializeTo` writes it out from the framework's embedded default. **Commit it once written**; from then
  on the app's copy is authoritative, and skip-if-exists will not overwrite it again)
- `TableSchema/company/ft_xxx.TableSchema.xml` (your business tables)

Each file defines `<Fields>` (`<DbField FieldName DbType Length>`) and `<Indexes>`. Polhem convention columns: `sys_no`
(AutoIncrement PK), `sys_rowid` (Guid relation key, unique `rx_`), `sys_id` (string business code, unique `uk_`),
`sys_name`. A foreign key is a `*_rowid` Guid column + an `fk_` index. The `{0}` placeholder = the table name.

> **dev runs without business tables**: as long as the `common` DB + st_cache_notify exist, `System.Ping`/`Login` and
> BOs that return in-memory/seed data work. Business tables can be added gradually later.

## appsettings.json / launchSettings.json

- `appsettings.json`: logging only; **Polhem settings do not go here** (they are in Define + environment variables).
  Connection strings are in DatabaseSettings.xml.
- `launchSettings.json`: `applicationUrl` determines the dev port.
- **The API key is a client-side constant**, not a server setting (the default check only verifies it is non-empty).
