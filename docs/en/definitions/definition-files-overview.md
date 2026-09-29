# Definition Files Overview

[繁體中文](../../zh-TW/definitions/definition-files-overview.md) · [← Docs Index](../README.md)

> The map of every definition file: what each one owns, how they connect, and what changing one affects. This page is the orientation layer — each entry links to the document that covers it in depth.

Polhem is definition-driven: the XML under your `DefinePath` is not configuration bolted onto an application, it *is* the application's structure. The framework reads it to build SQL, render UI, enforce permissions and localise text.

---

## 1. The Full Set

The definition types are enumerated as `DefineType` and all reached through `IDefineAccess`. `FormSchema`, `TableSchema`, `FormLayout` and `Language` are keyed and live in subfolders; the others are single files at the root of `DefinePath`.

The text written in the definition files — captions, display names, rule messages, menu captions — is the base text, in English. `Language` files translate it; a key no language resource declares keeps the base text.

| Definition | File path under `DefinePath` | Owns | Read in depth |
|------------|------------------------------|------|---------------|
| **FormSchema** | `FormSchema/{progId}.FormSchema.xml` | The definition hub: fields, types, relations, master-detail structure, computed fields and rules | [Architecture Overview](../architecture/architecture-overview.md) |
| **TableSchema** | `TableSchema/{categoryId}/{tableName}.TableSchema.xml` | The physical table: columns, types, lengths, nullability, indexes | [Schema Upgrade](../database/database-schema-upgrade.md) |
| **FormLayout** | `FormLayout/{layoutId}.FormLayout.xml` | How the form is arranged on screen. Authored at design time — the runtime renders this file and fails when it is absent | [Architecture Overview](../architecture/architecture-overview.md) |
| **Language** | `Language/{lang}/{namespace}.Language.xml` | Translations of the base text — captions, enum entries, rule messages, menu captions — one file per namespace × language | [Tenant Customization](customization.md) |
| **SystemSettings** | `SystemSettings.xml` | Process-wide settings: master key source, payload options, debug mode | [Development Cookbook](../guides/development-cookbook.md) |
| **DatabaseSettings** | `DatabaseSettings.xml` | Physical databases and their connection strings | [Database Settings Guide](../database/database-settings-guide.md) |
| **DbCategorySettings** | `DbCategorySettings.xml` | Which logical category (`common` / `company` / `log`) each table belongs to. The databases are not listed here: each `DatabaseItem` in `DatabaseSettings` names the category it holds | [Database Settings Guide](../database/database-settings-guide.md) |
| **ProgramSettings** | `ProgramSettings.xml` | The type registry: progId → the business object and repository bound to it. Server-side only | — |
| **MenuSettings** | `MenuSettings.xml` | The navigation menu: folders, ordering, captions and visibility, each entry pointing at a progId | — |
| **PermissionModels** | `PermissionModels.xml` | Permission model registry: models, actions and record-scope strategies | [Permission & Authorization](../security/permission-authorization.md) |
| **CurrencySettings** | `CurrencySettings.xml` | Currency master: per-currency decimals and natural minor units | [Development Cookbook](../guides/development-cookbook.md) |
| **UnitSettings** | `UnitSettings.xml` | Unit-of-measure master: display decimals per unit | [Development Cookbook](../guides/development-cookbook.md) |
| **PluginSettings** | `PluginSettings.xml` | Business plugin bindings: which plugins each progId mounts, run in declaration order | [Tenant Customization](customization.md) |

## 2. FormSchema Is the Hub

One `FormSchema` drives three layers at once. This is the single most important relationship in the framework:

```text
                    ┌──────────────────┐
                    │   FormSchema     │  fields · types · relations
                    │   {progId}       │  master-detail · rules
                    └────────┬─────────┘
             ┌───────────────┼───────────────┐
             ▼               ▼               ▼
      ┌────────────┐  ┌──────────────────┐  ┌──────────────┐
      │ FormLayout │  │ TableSchema      │  │ Rules /      │
      │  (UI)      │  │ + runtime SQL    │  │ Expressions  │
      └────────────┘  └──────────────────┘  └──────────────┘
       how it looks    where it lives ·        what's valid
                       how it is reached
```

- **Against the database**: the framework generates SQL per FormSchema at runtime — no ORM, no generated entity classes. See [FormSchema-Driven Database Access](formschema-data-access.md).
- **Against the UI**: `FormLayout` arranges the fields a FormSchema declares; controls read the field metadata (max length, list items, read-only, relation → lookup) directly.
- **Against validation**: computed fields and `FormRule` entries live inside the FormSchema itself. See [Expressions and Rules](expression-rules.md). A field marked `Required="true"` is enforced on the server: `FormBusinessObject.Save` refuses an added or modified row, master or detail, that leaves it empty, and names the field by its caption in the user's language. The check runs after the server fills default values and before anything is written. Empty means no value, text that is blank, or an empty GUID; a number, a boolean or a date always has a value, because those columns are `NOT NULL` with a default. Only stored fields are checked, so to require a lookup mark its stored key field, not the relation field that displays it. The Avalonia `FormView` and the Blazor `FormPage` apply the same rule (`RequiredFieldCheck` in `Polhem.Definition`) before they send a save, and name every empty field at once instead of sending it.

The practical consequence: **ordinary CRUD requires no code**. A FormSchema, its TableSchema, its FormLayout and a `DbCategorySettings` entry are a working form, and a `MenuEntry` in `MenuSettings` puts it on the menu. A `ProgramSettings` item is needed only to bind a custom business object or repository: a progId the registry does not name resolves to `FormBusinessObject` and `DataFormRepository` (§4).

## 3. The Startup Trio

Three settings files underpin every data access, and the first must load before the other two:

```text
SystemSettings.xml          ──▶ SysInfo.Initialize + ApiServiceOptions.Initialize
   (master key, payload)         (process-wide state)
        │
        ▼
DatabaseSettings.xml        ──▶ physical databases + connection strings
   (referenced by id)            (passwords decrypted using the master key)
        │
        ▼
DbCategorySettings.xml      ──▶ table → category
   (common / company / log)
```

`SystemSettings` must load before anything else because the master key it names is what decrypts the database passwords in `DatabaseSettings`. See [Development Cookbook § Framework Initialization Order](../guides/development-cookbook.md#framework-initialization-order) for the full sequence, and [Development Constraints](../architecture/development-constraints.md) for what breaks when the order is violated.

### Category is a scope selector, not a free string

`CategoryId` accepts exactly three values (`RepositoryFactory.ParseCategoryId` throws for anything else), and picking the wrong one is the most common setup mistake:

| Category | Meaning |
|----------|---------|
| `common` | Cross-company framework tables (sessions, cache notifications, users) |
| `company` | Per-company data — **all business tables belong here**, as do the application organisation tables |
| `log` | Log and audit tables |

A table prefix (`st_` / `ft_`) indicates *who owns* the table; the category indicates *where the data lives*. They are independent axes. See [Database Settings Guide](../database/database-settings-guide.md) and [Framework-Reserved Names](../reference/framework-reserved-names.md).

## 4. ProgramSettings Is the Type Registry

`ProgramSettings.xml` maps each progId to the types bound to it, and to nothing else. It is one
flat list — a progId is the key, so global uniqueness is a property of the structure and a
duplicate is rejected when the file loads.

```xml
<ProgramSettings>
  <Items>
    <ProgramItem ProgId="Customer" DisplayName="Customers" />
    <ProgramItem ProgId="Order" DisplayName="Orders"
                 BusinessObject="MyApp.Server.BusinessObjects.OrderBO, MyApp.Server"
                 Repository="MyApp.Server.Repositories.OrderRepository, MyApp.Server" />
  </Items>
</ProgramSettings>
```

- **`BusinessObject` empty** → the progId resolves to the framework's default `FormBusinessObject`, i.e. pure definition-driven CRUD.
- **`BusinessObject` set** → that type handles the progId, for the cases declarations cannot express (cross-row aggregation, database lookups).
- **`Repository` empty** → the framework's `DataFormRepository`, whose statements are generated from the FormSchema.
- **`Repository` set** → that type, which must derive from `DataFormRepository` so the CRUD surface stays intact. A business object asks for it by its own interface: `CreateFormRepository<IOrderRepository>()`.

The two attributes are independent — a program may customise its logic, its data access, both, or
neither. When both are set they are the same progId's pair: one program, one business object, one
repository.

**Both attributes follow the same failure policy: a name that will not load throws.** Declaring
nothing is not a failure — an empty attribute keeps the framework default, which is what makes
incremental adoption safe. But a name that is present and will not resolve is a configuration
error. Falling back would buy only the appearance of a running system: a misconfigured `Order`
would behave generically for a while, and on the data-access side it would run the program's reads
and writes through the generic SQL its author replaced on purpose, surfacing later with the data
already wrong.

`System`, `AuditLog` and `AuditRule` are reserved progIds (`ReservedProgIds`) and are entries like
any other. The host registers them at startup if they are absent, and they carry a tighter
base-type constraint than ordinary progIds: `System` must resolve to `SystemBusinessObject` and
`AuditLog` to `AuditLogBusinessObject`, or a subclass; `AuditRule`, a form whose default business
object is `AuditRuleBusinessObject`, must resolve to a `FormBusinessObject`. A binding outside that
base stops the host from starting. The registration leaves their `Repository` empty: `System` and
`AuditLog` reach data through the framework repositories, and `AuditRule` uses the schema-driven
default.

`ProgramSettings` is **server-side only**. It carries assembly-qualified type names that no client
has any use for, so remote `GetDefine` refuses it: a remote caller may read only the definition
types a client needs to render forms and menus (see [API Method Reference](../api/api-method-reference.md)).

## 4b. MenuSettings Is the Navigation Menu

Where a program *appears* is a separate definition, read by the client:

```xml
<MenuSettings>
  <Items>
    <MenuFolder Id="transactions" Caption="Transactions" Order="10">
      <Items>
        <MenuEntry Id="sales-order" Caption="Orders" Order="10" ProgId="Order" />
      </Items>
    </MenuFolder>
  </Items>
</MenuSettings>
```

- **`Id` is the node key and is unique across the whole tree**, independent of `ProgId`. The same
  program may legitimately appear in several places, so a shell tracks the open node by `Id`, not
  by `ProgId`.
- **Folders nest arbitrarily** and exist only to group entries.
- **`Visible` is a design-time switch, not a permission.** It is the same for every user;
  per-user visibility belongs to [PermissionModels](../security/permission-authorization.md). The framework
  applies no permission filter to the menu today.
- **`Caption` is the base text; translations** live in `LanguageResource` in the `Menu` namespace,
  sub-keys `Folder.{id}.Caption` / `Entry.{id}.Caption` — keyed by `Id` rather than `ProgId`, since
  the same program may appear under different titles. `MenuLocalizer` resolves them, and a client
  gets one from `FormDefinitionLoader.GetMenuLocalizerAsync`; a key no culture declares keeps the
  node's own `Caption`.

Splitting the two apart follows from what each is for. The registry is read by the server and holds
type names; the menu is read by the client and holds ordering, captions and visibility. They have
different readers, different lifecycles and different sensitivity — and keeping type names off the
wire is a direct consequence.

Adding a form to a running application is therefore a matter of XML edits, with no code.

## 5. Change One, Change What Else

| You changed | Also update |
|-------------|-------------|
| Added a field to a **FormSchema** | The matching **TableSchema** column, then run a [schema upgrade](../database/database-schema-upgrade.md); add it to the **FormLayout** if it should be visible; add translations of its caption to **Language** |
| Added a **new form** | **FormSchema** + **TableSchema** + **FormLayout** + a table entry in **DbCategorySettings** + a `MenuEntry` in **MenuSettings**; a `ProgramItem` in **ProgramSettings** only to bind a custom business object or repository |
| Added a **table** | Its **TableSchema** must sit in the `TableSchema/{categoryId}/` folder matching its `DbCategorySettings` category — the folder name *is* the category |
| Added a **database** | A `DatabaseItem` in **DatabaseSettings** whose `CategoryId` names the category it holds; a company database is assigned to its company through `st_company.company_database_id` — see [Database Settings Guide](../database/database-settings-guide.md) |
| Changed a **currency or unit precision** | **CurrencySettings** / **UnitSettings**; field-level rounding follows `NumberKind`, not the raw column type |
| Added a **permission-controlled action** | **PermissionModels**, then the relevant `FormField.ScopeRole` entries — see [Permission & Authorization](../security/permission-authorization.md) |

## 6. `DefinePath` and the `Defaults/` Scaffold

Two things that are easy to conflate:

- **`DefinePath`** is what the runtime reads. It is the only source of definitions at runtime.
- **`Defaults/`**, embedded in `Polhem.Definition.dll`, is a **scaffold source** for starting a new project. `dotnet polhem defines materialize` copies it into your `DefinePath` once.

> **There is no fallback.** If a definition is missing from `DefinePath`, the framework does **not** fall back to `Defaults/`. To use a framework system table in your project, materialise its definition into your `DefinePath` and extend from there — keeping the framework's standard fields, which the permission and organisation features depend on.

### Definitions are immutable after initialisation

Everything obtained through `IDefineAccess.GetX(...)` is a **process-wide cached instance** shared by every session. Mutating one at runtime leaks across sessions. Clone before modifying, and persist changes through `IDefineAccess.SaveX(...)`, which writes to storage and invalidates the cache slot.

See [Development Constraints § Cached Data Immutability After Init](../architecture/development-constraints.md) for the full rule.

### Storage is pluggable

The file layout above is the default (`FileDefineStorage`). Definitions can also live in a database — see [ADR-018](../../../maintainers/adr/adr-018-db-define-storage.md). `IDefineAccess` is the same either way; only the backing store changes.

## 7. `CustomizePath` and the Tenant Customization Overlay

`DefinePath` holds the base definitions every tenant shares. `CustomizePath` is the optional second root that lets one company override parts of them without forking the base — see [ADR-016](../../../maintainers/adr/adr-016-multitenant-customization-overlay.md) for the design.

### Turning it on

The host computes it and hands it to `AddPolhemFramework` alongside `DefinePath`. There is no configuration binding — `PathOptions` is constructed by the host, exactly as `DefinePath` always has been:

```csharp
var paths = new PathOptions
{
    DefinePath = definePath,
    CustomizePath = Path.Combine(deployRoot, "Customize"),
};
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
```

**An empty `CustomizePath` disables the overlay entirely** — every consumer resolves against the base layer, bit for bit as if the feature did not exist. That is the default. `samples/Polhem.Samples.Shared/DemoBackend.cs` shows the wiring.

### Layout

```
{CustomizePath}/{customizeId}/ProgramSettings.xml
{CustomizePath}/{customizeId}/MenuSettings.xml
{CustomizePath}/{customizeId}/PluginSettings.xml
{CustomizePath}/{customizeId}/FormLayout/{layoutId}.FormLayout.xml
{CustomizePath}/{customizeId}/Language/{lang}/{namespace}.Language.xml
```

The directory need not exist. A tenant that supplies no file for a given lookup falls back to the base layer.

### The customizable types and their granularity

| Type | Overlay granularity |
|------|--------------------|
| **LanguageResource** | **Per key** for text (`LanguageItem`). The customization file holds only the keys it changes; every other key comes from base — so a base translation added later propagates automatically. **A `LanguageEnum` is the exception: whole-enum.** A customization enum of the same name replaces the base one outright, so it must list every entry the option set should have |
| **ProgramSettings** | **Per progId, then per property.** A customization entry wins over the base entry of the same progId, and within that entry each binding is independent: a customization that names only `BusinessObject` keeps the base `Repository`. Write only what you are changing |
| **PluginSettings** | **Per progId, concatenated.** The only granularity that adds instead of choosing: the base chain runs, then the customization chain. A plugin is an extra step rather than a replacement, so two layers' plugins do not conflict |
| **MenuSettings** | **Whole file.** A customization menu replaces the base menu outright |
| **FormLayout** | **Whole file.** A customization layout replaces the base layout for that `layoutId` |

Property-level inheritance inside a `ProgramSettings` entry is what stops a partial customization from
undoing a base binding. An entry carries two independent bindings, and an empty one is a legal "use the
framework default" rather than an error — so replacing the entry wholesale would drop the base
repository and report nothing. To deliberately return a binding to the framework's own type, name that
type explicitly rather than clearing the attribute:

```xml
<!-- This program keeps its custom repository and goes back to generic CRUD behaviour. -->
<ProgramItem ProgId="Order" BusinessObject="Polhem.Business.Form.FormBusinessObject, Polhem.Business" />
```

The granularities differ on purpose, and the dividing line is whether the artifact is a bag of independent values or a single composed whole. `PluginSettings` forms a category of its own because it is neither: it is an ordered chain, so concatenating the two layers is the correct semantics.

Text keys are independent: "this label reads differently here" leaves every other key alone, so merging key by key is both cheap and obvious. A layout is one visual arrangement — sections, ordering, column spans and nesting only make sense together, and a partial merge would raise questions ("this section moved — do the fields under it follow?") with no intuitive answer. An enum sits on the layout side of that line rather than the text side: it is an ordered option set, where merging entry by entry would leave both the ordering and the meaning of an omitted entry ambiguous.

So for layouts and enums, a tenant that customizes one owns it whole, and a tenant that does not gets the base version untouched.

Owning it whole cuts both ways: a field added to the base `FormSchema` later **does not** appear on a tenant that has customized that layout, and the framework neither merges it in nor warns about the difference. This is the intent rather than a limitation — **the layout is the authority on what the screen shows**, and a schema gaining a field is not a statement that every tenant's form should now display it. Putting the new field on that tenant's form is a decision, and it is made by editing that tenant's layout file.

> **FormSchema and TableSchema are permanently excluded.** Both drive the database schema and the validation rules as well as the UI; letting them diverge per tenant would split the physical schema. This is a decision, not a gap — see ADR-016.

> The overlay is **read-only, except for `PluginSettings`**. Customization files are produced by deployment tooling; every other `SaveXxx` on the file-backed override layer throws `NotSupportedException` (`CustomizeOnlyStorage`). Plugin bindings are maintained through the local-only `SystemBusinessObject.SaveCustomizePluginSettings` — see [Tenant Customization](customization.md#read-only-except-for-plugins).

### Where `customizeId` comes from

`CompanyInfo.CustomizeId` (column `st_company.customize_id`) is copied onto `SessionInfo.CustomizeId` when the session enters a company, and cleared on leave / logout. Server-side consumers read it from `SessionInfo`; the local-only plugin maintenance API, which names the tenant it maintains, is the one exception.

Two consequences worth planning around:

- **Nothing is customized before `EnterCompany`.** The login screen, the company picker, and every message on the way there resolve against the base layer, because there is no `CustomizeId` yet.
- **`SessionInfo.CustomizeId` is a snapshot, not a live value.** It is copied at the moment the session enters the company, the same as roles and the employee context. Editing `st_company.customize_id` afterwards does not move existing sessions — they pick the new value up on the next `EnterCompany`.

> **Security boundary:** no API a remote caller can reach accepts a `customizeId` from the client as the lookup key — doing so would let a caller choose which tenant's customization to read. The plugin maintenance calls that do take one are `LocalOnly`. Clients receive their own `CustomizeId` from `EnterCompany` for their own UI localization only; the server always reads `SessionInfo.CustomizeId`.

---

## Where to go next

| You want to | Read |
|-------------|------|
| See how these pieces form an architecture | [Architecture Overview](../architecture/architecture-overview.md) |
| Follow the full definition → API flow | [Development Cookbook](../guides/development-cookbook.md) |
| Understand SQL generation from a FormSchema | [FormSchema-Driven Database Access](formschema-data-access.md) |
| Compute and validate fields declaratively | [Expressions and Rules](expression-rules.md) |
| Know which names the framework owns | [Framework-Reserved Names](../reference/framework-reserved-names.md) |
| Set up databases and categories | [Database Settings Guide](../database/database-settings-guide.md) |
