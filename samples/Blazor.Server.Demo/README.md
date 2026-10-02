# Blazor.Server.Demo

**English** | [繁體中文](README.zh-TW.md)

A Blazor Server host that demonstrates how to wire `Polhem.Web.Blazor.Server` components into ASP.NET Core and dispatch directly into the Polhem backend via the **in-process `LocalApiProvider`** (same process, no HTTP round-trip).

## How to run

```bash
cd samples/Blazor.Server.Demo
dotnet run
# Browser opens http://localhost:5055 automatically
```

On first run:

1. Reads the master key from `POLHEM_MASTER_KEY`; `DemoBackend.AddPolhemBackend` auto-injects a hard-coded demo value when the variable is unset (production hosts must override — see [`samples/README.md`](../README.md#master-key))
2. Creates `samples/Blazor.Server.Demo/quickstart.db` (SQLite) with every table [`Define/DbCategorySettings.xml`](../Define/DbCategorySettings.xml) registers: the demo's `ft_*` tables and the framework tables the backend needs (sign-in, company entry, the cache-notify poller), all defined under [`samples/Define/TableSchema/`](../Define/TableSchema/)
3. Seeds the `demo` user row (sign-in reads the locale from it), the demo company `DEMO` and the user's access to it, plus demo staff and teams

## What you'll see

1. Landing page shows a **Sign in** panel pre-filled with the `demo / demo` hint
2. Click Sign in (sends `SystemApiConnector.LoginAsync`, handled by `DemoAuthenticatingSystemBusinessObject`); the page then calls `EnterCompanyAsync("DEMO")`, because the Staff form is company-scoped, and only shows the form once both calls succeed ([`Components/Pages/Home.razor`](Components/Pages/Home.razor))
3. After a successful login, `<FormPage ProgId="Staff" />` renders:
   - Top toolbar: `New` / `Save` / `Delete`
   - Middle: staff grid (`DynamicGrid`, columns from `FormSchema.ListFields`)
   - Bottom: edit form (`DynamicForm`) appears when a row is selected
4. Click `New` → edit fields → `Save`; the new staff record appears in the grid

## What this maps to in the library

| Demo behavior | Library component |
|---------------|-------------------|
| Login form | `PolhemLoginPanel` |
| Entering the company | `SystemApiConnector.EnterCompanyAsync` |
| AccessToken cascading | `PolhemAccessTokenProvider` |
| Staff grid rendering | `DynamicGrid` + `FormSchema.GetListLayout()` |
| Staff edit form | `DynamicForm` + the stored `FormLayout` definition (`Define/FormLayout/Staff.FormLayout.xml`) |
| Grid + form integration | `FormPage` |
| CRUD through Polhem | `FormDataObject.LoadAsync / SaveAsync / NewAsync / DeleteAsync` |
| Local in-process dispatch | `PolhemBlazorOptions.UseLocalProvider()` |
| In-process JSON-RPC | `LocalApiProvider` → `JsonRpcDispatcher` → `FormBusinessObject` |

## Simplifications vs production

- **Local mode trusts every browser user.** `UseLocalProvider()` makes each call a trusted in-process call: the backend skips the access token check and the `LocalOnly` restriction for it. That suits a site whose users are all trusted with the whole backend, such as this single-user demo or an internal administration tool. A site whose users must be held to their own permissions uses `UseRemoteProvider(endpoint)` instead (see the `PolhemBlazorOptions` remarks)
- **`DemoAuthenticatingSystemBusinessObject`** replaces only the credential check: it accepts the hard-coded `demo/demo` instead of verifying a password stored in `st_user`. The rest of sign-in is the framework's, so `st_user` (the user's time zone and culture), `st_session` (the session seed), `st_company` and `st_user_company` (the company entry) are still created and seeded
- **One company, entered without asking**: a deployment with several puts a company picker between `Login` and `EnterCompany`
- SQLite is a single file (`quickstart.db`), same as `QuickStart.Server`

Session state is not a simplification: `AddPolhemBlazor` registers one `ApiSessionContext` per circuit, so concurrent users each keep their own transmission key and time zone.
