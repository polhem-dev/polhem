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
2. Creates `samples/Blazor.Server.Demo/quickstart.db` (SQLite) with the demo's `ft_*` tables and the framework tables the backend needs (`st_session` and `st_user` for sign-in, `st_cache_notify` for the cache-notify poller), all defined under [`samples/Define/TableSchema/`](../Define/TableSchema/)
3. Seeds demo employees and departments, plus the `demo` user row that sign-in reads the locale from

## What you'll see

1. Landing page shows a **Sign in** panel pre-filled with the `demo / demo` hint
2. Click Sign in (sends `SystemApiConnector.LoginAsync`, handled by `DemoAuthenticatingSystemBusinessObject`)
3. After a successful login, `<FormPage ProgId="Employee" />` renders:
   - Top toolbar: `New` / `Save` / `Delete`
   - Middle: employee grid (`DynamicGrid`, columns from `FormSchema.ListFields`)
   - Bottom: edit form (`DynamicForm`) appears when a row is selected
4. Click `New` → edit fields → `Save`; the new employee appears in the grid

## What this maps to in the library

| Demo behavior | Library component |
|---------------|-------------------|
| Login form | `PolhemLoginPanel` |
| AccessToken cascading | `PolhemAccessTokenProvider` |
| Employee grid rendering | `DynamicGrid` + `FormSchema.GetListLayout()` |
| Employee edit form | `DynamicForm` + the stored `FormLayout` definition (`Define/FormLayout/Employee.FormLayout.xml`) |
| Grid + form integration | `FormPage` |
| CRUD through Polhem | `FormDataObject.LoadAsync / SaveAsync / NewAsync / DeleteAsync` |
| Local in-process dispatch | `PolhemBlazorOptions.UseLocalProvider()` |
| In-process JSON-RPC | `LocalApiProvider` → `JsonRpcExecutor` → `FormBusinessObject` |

## Simplifications vs production

- **Local mode trusts every browser user.** `UseLocalProvider()` makes each call a trusted in-process call: the backend skips the access token check and the `LocalOnly` restriction for it. That suits a site whose users are all trusted with the whole backend, such as this single-user demo or an internal administration tool. A site whose users must be held to their own permissions uses `UseRemoteProvider(endpoint)` instead (see the `PolhemBlazorOptions` remarks)
- **`DemoAuthenticatingSystemBusinessObject`** replaces only the credential check: it accepts the hard-coded `demo/demo` instead of verifying a password stored in `st_user`. The rest of sign-in is the framework's, so `st_user` (the user's time zone and culture) and `st_session` (the session seed) are still created and seeded. The demo never enters a company, so it has no `st_company` / `st_user_company` rows
- SQLite is a single file (`quickstart.db`), same as `QuickStart.Server`

Session state is not a simplification: `AddPolhemBlazor` registers one `ApiSessionContext` per circuit, so concurrent users each keep their own transmission key and time zone.
