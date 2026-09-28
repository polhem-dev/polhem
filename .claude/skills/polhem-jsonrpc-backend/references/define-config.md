# The Define/ settings tree

A handful of XML files plus the `TableSchema/` folder. They live under the host project's `Define/`, and `Program.cs`
locates them by walking up. Below is minimal content you can paste (a single-file SQLite dev setup).
`samples/Define/` is the working reference; when this file and it disagree, the sample wins.

## SystemSettings.xml — root settings

```xml
<?xml version="1.0" encoding="utf-8"?>
<SystemSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <CommonConfiguration>
    <Version>1.0.0</Version>
    <IsDebugMode>true</IsDebugMode>
    <!-- Extra namespaces allowed as payload type names, separated by '|'. The framework's own are always allowed.
         Needed only when your own types travel in Encoded / Encrypted bodies. -->
    <AllowedTypeNamespaces>Xxx.Server</AllowedTypeNamespaces>
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
  </BackendConfiguration>
  <FrontendConfiguration />
  <WebsiteConfiguration />
  <BackgroundServiceConfiguration />
</SystemSettings>
```

- **`AllowedTypeNamespaces` screens assembly names too.** A payload type name is accepted only when its namespace is
  allowed **and** its assembly's simple name equals or starts with an allowed entry (`WireTypeWhitelist` remarks).
  Listing the root namespace the assembly shares with its types (here `Xxx.Server`) covers both.
- The replaceable components (`<Components>` under `BackendConfiguration`) are the properties of `BackendComponents`
  (`src/Polhem.Definition/Settings/SystemSettings/BackendComponents.cs`); leave the element out to use the defaults.
  Business objects are not among them: they are bound in `ProgramSettings.xml`.

## DatabaseSettings.xml — logical DB id → connection string

A `DatabaseItem` whose `Id` is `common` is **required** (the router resolves the common scope to that literal id;
`st_session`, `st_cache_notify` and `st_user` live there). Company databases are reached per session through the
company registry, not by id; that setup belongs to `polhem-app-scaffold`.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DatabaseSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Items>
    <DatabaseItem Id="common" CategoryId="common" DisplayName="Common (SQLite)"
        DatabaseType="SQLite" ConnectionString="Data Source=xxx.db;Cache=Shared" />
  </Items>
</DatabaseSettings>
```

## DbCategorySettings.xml — which tables belong to which category/DB

The seeder iterates over this file to create tables. Adding a table = a new `TableSchema` file + one `TableItem` here.
`CategoryId` values are `common` / `company` / `log`; the rules for which one a table belongs to are in
`.claude/rules/database.md`.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DbCategorySettings xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Categories>
    <DbCategory Id="company" DisplayName="Company Database">
      <Tables>
        <!-- <TableItem TableName="ft_xxx" DisplayName="Xxx" /> -->
      </Tables>
    </DbCategory>
  </Categories>
</DbCategorySettings>
```

## ProgramSettings.xml — the progId registry (server side)

One flat list of `ProgramItem`s. `BusinessObject` binds a progId to its business object class and `Repository` to its
repository class; both are **assembly-qualified type names** (`"Namespace.Type, AssemblyName"`) and both may be left
out to get the framework default.

```xml
<?xml version="1.0" encoding="utf-8"?>
<ProgramSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Items>
    <!-- Reserved progId. No BusinessObject → the framework's SystemBusinessObject; bind a subclass to replace the
         credential check (business-object.md). -->
    <ProgramItem ProgId="System" DisplayName="System" />
    <ProgramItem ProgId="Game" DisplayName="Game"
        BusinessObject="Xxx.Server.BusinessObjects.GameBO, Xxx.Server" />
    <!-- No BusinessObject → FormBusinessObject (definition-driven CRUD) -->
  </Items>
</ProgramSettings>
```

- At startup the framework adds any missing reserved progId and rewrites the file, comments included (see the comments
  in `samples/Define/ProgramSettings.xml`). Declaring the reserved entries yourself avoids that rewrite.
- **ProgramSettings is not the menu and is not served to clients**: `GetDefine` serves only the types on its remote
  allow-list (`SystemBusinessObject.Define.cs`), and `ProgramSettings` is not one of them.

## MenuSettings.xml — the navigation menu (only for UI heads)

A tree of `MenuFolder` / `MenuEntry` nodes; each `MenuEntry` names a `ProgId` registered in `ProgramSettings.xml`.
Clients fetch it through `GetDefine`. A backend that serves no menu-driven UI can leave it out.
`apps/Polhem.Northwind/Define/MenuSettings.xml` is a complete example, with the meaning of `Id` / `Visible` in its
header comment.

## TableSchema/

The physical DB schema, in one folder per category:
- `TableSchema/common/st_cache_notify.TableSchema.xml`, `st_session.TableSchema.xml`, `st_user.TableSchema.xml` (do
  not hand-write them: on first startup `Defaults.MaterializeTo` writes them out from the framework's embedded
  defaults. **Commit them once written**; from then on the app's copies are authoritative, and skip-if-exists will not
  overwrite them again)
- `TableSchema/company/ft_xxx.TableSchema.xml` (your business tables; the folder name equals the CategoryId)

Each file defines `<Fields>` (`<DbField FieldName DbType Length>`) and `<Indexes>` (`<DbTableIndex>`). Polhem
convention columns: `sys_no` (AutoIncrement PK), `sys_rowid` (Guid relation key, unique `rx_`), `sys_id` (string
business code, unique `uk_`), `sys_name`. A foreign key is a `*_rowid` Guid column + an `fk_` index. The `{0}`
placeholder in an index name = the table name. The framework's reserved table names and progIds are listed in
`docs/en/framework-reserved-names.md`.

> **dev runs without business tables**: as long as the `common` DB and the framework tables above exist,
> `System.Ping` / `Login` and BOs that return in-memory/seed data work. Business tables can be added gradually later.

## appsettings.json / launchSettings.json

- `appsettings.json`: logging only; **Polhem settings do not go here** (they are in Define + environment variables).
  Connection strings are in DatabaseSettings.xml.
- `launchSettings.json`: `applicationUrl` determines the dev port.
- **The API key is sent by the client** in `X-Api-Key`. Until the deployment issues a key (stored hashed in
  `st_api_key`), any non-empty value passes; `UsePolhemFramework` logs that state at startup.
