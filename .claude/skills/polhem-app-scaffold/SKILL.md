---
name: polhem-app-scaffold
description: Wiring conventions for building a "standalone Polhem backend app/demo" in polhem (or in a standalone repository after it graduates) — without Polhem.Samples.Shared, depending only on public Polhem.* packages or ProjectReference. Covers the easiest thing to get wrong, DB scoping (CategoryId is the common/company/log scope selector; business data must be company), a lightweight company context, custom auth (no st_user), the seeder (DbCategorySettings-driven table creation + sys_id relation seeding), ProgramSettings doubling as BO binding + menu source, the rule that the Server must not depend on Polhem.Api.Client, and operational pitfalls that keep recurring. Use when the user wants to "start a Polhem app / backend", "build a demo that will graduate to its own repository", "wire up a Polhem backend host myself", "configure DatabaseSettings / DbCategorySettings / the company database", "how Polhem splits common vs company", and similar requests; trigger proactively even if they do not say scaffold.
---

# Standalone Polhem backend wiring

Building a standalone Polhem backend that does not rely on `Polhem.Samples.Shared` (typically a demo that will
"graduate" into its own repository, or a production app) means wiring up a dozen or so things yourself that are easy
to miss or get wrong. These are not arbitrary choices; **they are fixed conventions**. This skill pins them down and
marks `apps/Polhem.Northwind/` as the complete implementation to compare against.

> **Reference implementation**: `apps/Polhem.Northwind/` (Server / UI / Desktop + `Define/`). Every part can be read
> against its counterpart file there.

## When to use

- Starting a backend / demo that depends only on public `Polhem.*` packages (or ProjectReference) and
  **does not reference `Polhem.Samples.Shared`** (e.g. a sample that will later move to its own repository)
- You need to write your own host bootstrap (`AddXxxBackend` / `UseXxxBackend`)
- You need to set up multi-database scope (common / company), the company context, and the seeder correctly

## When not to use

- You only need "a JSON-RPC server + client round trip that runs", with no company scope / seeder →
  **`polhem-jsonrpc-backend`** (it is also the authoritative source for this file's bootstrap and auth templates)
- A demo placed in `samples/` that can use `Polhem.Samples.Shared` → use **`polhem-sample-add`** (it picks the
  backend, auth, slnx, and shared Define)
- A pure MAUI / Avalonia front-end skeleton → **`maui-app-scaffold`** / `avalonia-*` (front-end heads, no backend
  wiring)
- You just want to "add a form" to an app that is already wired → **`polhem-add-form`**
- Adding a cross-layer BO method → **`polhem-add-bo-method`**

## Division of labour with related skills

| Skill | Handles |
|-------|---------|
| **`polhem-app-scaffold`** (this skill) | Adds DB scoping + company context + seeder on top of the items below |
| `polhem-jsonrpc-backend` | **Authoritative templates for host bootstrap / empty controller / the login trio / client calls** |
| `polhem-sample-add` | Front-end/backend pairing for `samples/` projects (may use Polhem.Samples.Shared) |
| `polhem-add-form` | Adding a form to an already-wired app (FormSchema/TableSchema/registration) |
| `polhem-scaffold-from-formschema` | Generating layout/language/tableschema sidecars from one FormSchema |
| `demo-smoke` | End-to-end smoke test once wired |

> This skill's BO axis is **`FormBusinessObject`** (ERP definition-driven CRUD);
> `polhem-jsonrpc-backend`'s is **`BusinessObject`** (custom RPC actions). The two can coexist in the same host.

---

## Part 1 — DB scoping (the most critical, the easiest to get wrong)

> **CategoryId is not a free-form string; it is the DB scope selector.** `FormRepositoryFactory.ParseCategoryId`
> only accepts `common` / `company` / `log` (`DbCategoryIds`); anything else throws `Unknown schema.CategoryId`.
> Putting business tables under `common` is wrong (this was the main fix when Polhem.Northwind was wired up). See
> `.claude/rules/database.md` for the two orthogonal axes, table prefix and CategoryId.

| Scope | What goes here | How it resolves |
|-------|----------------|-----------------|
| **`company`** | **Business data**: `ft_*`, plus the app's organization tables `st_department` / `st_employee` (an app's employees belong to that company) | The router goes `session.CompanyId → ICompanyInfoService.Get → CompanyInfo.CompanyDatabaseId` |
| **`common`** | Framework tables shared across companies (`st_session`, `st_cache_notify`). **Not application data** | Fixed databaseId `"common"` (the framework enforces `DatabaseItem.Id == CategoryId == "common"`) |
| `log` | Audit / operation log | Fixed `"log"` |

Three things to put in place:
1. **FormSchema**: business tables use `CategoryId="company"`.
2. **TableSchema folder = CategoryId**: `TableSchema/company/*.xml` (framework tables stay in `TableSchema/common/`).
   Wrong folder → the seeder cannot find it / builds it in the wrong database.
3. **DatabaseSettings**: keep `common` (Id==CategoryId=="common") + add a `company` DatabaseItem. For a single-company
   demo both can point at **the same SQLite file** to stay single-file; only a real multi-company setup needs separate
   databases.

```xml
<!-- DbCategorySettings.xml: business tables go under the company category -->
<DbCategory Id="company" DisplayName="...Company Database">
  <Tables><TableItem TableName="ft_xxx" DisplayName="..." /> ... </Tables>
</DbCategory>
```

## Part 2 — Host bootstrap (`AddXxxBackend` / `UseXxxBackend`)

> **For the complete order and copy-paste templates see the `polhem-jsonrpc-backend` skill's
> `references/backend-bootstrap.md`** (master key fallback, `ResolveDefinePath` walk-up,
> `Defaults.MaterializeTo` laying down framework tables, provider/dialect registration, `SystemSettingsLoader` →
> `SysInfo` → `ApiServiceOptions` → `AddPolhemFramework`, empty controller).
> That file is the single authoritative source; this file does not duplicate it.

On top of that template, **this scenario (company scope + seeder) needs only two extra things**:

1. **Override one more service, `ICompanyInfoService`** → lightweight company (Part 3). Like the factory / resolver,
   it must be registered with `AddSingleton` **after** `AddPolhemFramework` (the later registration wins).
2. **`UseXxxBackend` runs the full seeder** (Part 5), not just the creation of the single framework table
   `st_cache_notify`.

Implementation to compare against: `apps/Polhem.Northwind/Polhem.Northwind.Server/NorthwindBackend.cs`.

## Part 3 — Company context: even a single company goes through `EnterCompany`

⛔ **Do not take the shortcut of "hard-code `ICompanyInfoService` + stamp `SessionInfo.CompanyId` inside the overridden
`Login`".** Northwind took it and removed it on 2026-08-28. It cost two silent wrong behaviours:

1. **Every company-keyed lookup stops working.** `CacheDataSourceProvider`'s `GetCompanyRolePermissions` /
   `GetDepartmentTree` / `GetCompanyAuditRules` all call `GetCompanyInfo` first, and that one goes through
   **`ICompanyRepository` (reads `st_company`)**, not the `ICompanyInfoService` you replaced.
   No row in `st_company` → always returns `null` → those mechanisms silently do nothing.
2. **Session rebuild loses the company, and the application cannot patch it.** Besides writing the cache,
   `EnterCompany` also calls `SessionRepository.UpdateSession(CreateSeed(...))` to write the company into the
   `st_session` seed; and `CreateSeed` is `private static`, `SessionRepository` is `private`, so **subclasses cannot
   reach them**. As soon as the cache is evicted or the server restarts, the rebuilt session has no company, and every
   `CategoryId="company"` form throws `CompanyNotEnteredException`.

The correct approach is to follow the framework's two steps; it costs less than the shortcut:

- **Seed three tables**: `st_user`, `st_company` (do not miss `customize_id`; the customization layer depends on it),
  `st_user_company`. The three XML columns (`number_formats_xml` / `cash_rounding_xml` / `allowed_currencies_xml`) are
  `DbType="Text"`; hand-written INSERTs must give `''` explicitly (MySQL's TEXT cannot have a DEFAULT).
- ⛔ **Also register `st_role` / `st_role_grant` / `st_user_role`** (company category; empty tables are fine).
  `RolePermissionRepository` has **no** schema-probe guard (`AuditRuleRepository` does). Seeding only `st_company`
  without creating these three upgrades "silently does nothing" to **cannot enter the company after login**.
- **On the client, call once after login** `SystemApiConnector.EnterCompanyAsync(companyId)` +
  `ClientInfo.ApplyEnterCompanyResult(...)`. With a single company it is entered automatically; no picker needed.
  ⚠️ **Re-fetch the connector**: `ClientInfo.ApplyLoginResult` discards the cached connector
  (it was built with the empty pre-login token). Reusing the local variable from before login gives
  "AccessToken is required or invalid".
- If a form has no `PermissionModelId`, empty roles are fine — both server and front end short-circuit when
  `modelId` is empty.

Implementation to compare against: `apps/Polhem.Northwind/Polhem.Northwind.Server/NorthwindSchemaSeeder.cs`
(`SeedCommon`) and `apps/Polhem.Northwind/Polhem.Northwind.UI/ViewModels/LoginViewModel.cs`.

## Part 4 — Custom auth (no st_user)

> **For the complete code of the login trio** (Credentials / authenticating System BO / factory) **see the
> `polhem-jsonrpc-backend` skill's `references/business-object.md`**. This file does not duplicate it.

The only difference in this scenario: the authenticating BO's `Login` **also stamps the company** (Part 3);
the `AuthenticateUser` part is the same as that template. Compare against
`NorthwindAuthenticatingSystemBusinessObject`.

## Part 5 — Seeder (create tables + seed data)

Compare against `NorthwindSchemaSeeder.cs`. Idempotent (tables are create-if-not-exists; each table is filled only
when empty).

- **Table creation is data-driven**: enumerate each category in `DbCategorySettings`,
  `new TableSchemaBuilder(category.Id, ...)` + `Execute(category.Id, tableName)` — `category.Id` is both the **target
  db** and the **TableSchema folder**. Framework tables (`st_cache_notify`) are built into common with a separate common
  builder. **This makes "add a table = pure XML (TableSchema + one DbCategorySettings entry)" hold; the seeder needs no
  C# changes.**
- **Seed data goes into the company db** (business data): `dbAccessFactory.Create("company")`.
- **Relation seeding uses `sys_id`**: relation fields in the JSON hold the target `sys_id` (human-readable), and the
  seeder resolves it to `sys_rowid`. **Forward** (target already created) resolves inline; **Deferred** (circular,
  e.g. Department.manager↔Employee) does a second-pass UPDATE. Details use the same Forward mechanism
  (`sys_master_rowid` → master table sys_id, `product_rowid` → product sys_id); no special master-detail logic needed.
- **Copy SeedData to the output**: csproj
  `<Content Update="SeedData\**\*.json" CopyToOutputDirectory="PreserveNewest" />`.

## Part 6 — ProgramSettings does two jobs

`Define/ProgramSettings.xml` serves two purposes in one file:
1. **BO binding**: `ProgramItem.BusinessObject="Ns.Type, Asm"` → `ProgramSettingsBoTypeResolver` loads the custom
   `FormBusinessObject`. Empty → framework default (pure definition CRUD).
2. **Navigation menu source**: the front end enumerates category→header, item→form link from
   `ClientInfo.DefineAccess.GetProgramSettings()` (data-driven, not hard-coded `NavItems`). `ProgramCategory` groups
   the menu (unrelated to the DB's common/company).

> GetDefine transports via `GetDefineResult.Xml` (an XML string); a definition type only needs to be XML-serializable
> to be fetched remotely (same path as FormSchema). `SystemBusinessObject.GetDefine` only blocks remote fetches of
> `SystemSettings`/`DatabaseSettings`; ProgramSettings can be fetched remotely.

---

## Hard rules

1. **The Server must not `ProjectReference Polhem.Api.Client`**. The backend is the backend, the client is the client.
   The only temptation is the in-process client bridge (local connectors built with the host's `IServiceProvider`)
   — remote heads go over HTTP and do not need it; delete it.
2. **CategoryId ∈ {common, company, log}**, business data = company (Part 1).
3. **TableSchema folder name = CategoryId**.
4. **The slnx does not list `Define/` files**: they are runtime data and go stale; the server reads the whole directory
   via `PathOptions.DefinePath`.
5. **Override services are registered after `AddPolhemFramework`** (the later one wins).
6. **Mark computed / server-derived fields `FormField.ReadOnly="true"`** (e.g. an amount computed by the BO) — when the
   FormLayout is generated this carries over to `LayoutField.ReadOnly`, so there is no need to mark it again in the
   layout. **The FormLayout itself must still be written to a file** (it is not generated automatically at runtime).

## Pitfalls (hit repeatedly)

- **Port in use**: before re-running the server, `lsof -ti :<port> | xargs kill -9`; if the old instance is not
  stopped you get `address already in use`, yet the new instance's seeder may already have run (the seeder runs before
  `app.Run()`) — do not misread it.
- **Schema changes need a rebuild**: after adding a column, delete `*.db` and re-run so the seeder rebuilds it
  (create-if-not-exists does not ALTER existing tables to add columns). `.db` should be gitignored.
- **apps/ is not in CI**: `build-ci.yml` only triggers on `src/ tests/ slnx props sonar yml`. Backend correctness
  relies on a local build + `demo-smoke`; when you change low-level src, CI still builds src+tests.
- **Avalonia UI self-testing is handed to the user**: computer-use's `request_access` does not recognize a bare
  dotnet process running Avalonia (it must be wrapped in a .app); once it compiles, hand it to the user to test.
- **The symptoms of wrong company wiring are unmistakable**: every form reports `CompanyNotEntered` (the session has no
  CompanyId) or the db cannot be resolved — check Part 3 first; it is not a form problem.

## Completion checks

- [ ] `dotnet build` (including front-end heads) is fully green
- [ ] Delete the db and re-run: the seeder creates every table (company + framework), seeding has no errors, and the
      server prints `Now listening`
- [ ] The Server csproj has no `Polhem.Api.Client`
- [ ] Every business FormSchema has `CategoryId="company"`, TableSchema is in the `company/` folder, and
      DbCategorySettings has the matching category
- [ ] Form CRUD works after login (company routing works) — hand to the user to self-test, or use `demo-smoke`
