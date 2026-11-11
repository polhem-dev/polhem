# PolhemApp

A [Polhem](https://github.com/polhem-dev/polhem) application. Its forms are definitions under `Define/`: each form
describes its fields, lookups and rules, and the framework provides the database tables, the API and the screens.

| Folder | Content |
|--------|---------|
| `src/PolhemApp.Server` | The server (`http://localhost:5180`): the backend, its JSON-RPC API at `/api`, and the files of the browser client, from one origin |
| `src/PolhemApp.Client` | The browser client (Blazor WebAssembly): the Polhem shell with the sign-in, the menu and the forms. It calls the API of the server it was loaded from |
| `Define` | The definition files. The server adds the framework's default definitions on its first run |
| `Excel` | The form workbooks you import into `Define` |

## Requirements

- The .NET 10 SDK, and VS Code with the C# Dev Kit extension.
- A database server the connection in `Define/DatabaseSettings.xml` can reach (on Oracle, the three schemas it names).

## Getting started

Run these from **Terminal → Run Task…** in VS Code.

1. **Set the database password.** Run **Polhem: Open DefineEditor**, open `DatabaseSettings`, enter the password of
   the `local` server and save. (Skip this when you gave `--db-password` to `dotnet new`.)
2. **Polhem: Initialize database** creates the databases, builds the framework tables and creates the first user,
   `admin`, in the company `POLHEM_COMPANY_ID`. It asks for the user's password in the terminal, or reads it from the
   `POLHEM_ADMIN_PASSWORD` environment variable. Its first run also creates the master key `Define/Master.key`;
   from then on DefineEditor's **File → Test Database Connections** and database upgrades work too.
3. **Import forms.** In DefineEditor, **File → Import Form from Excel** (see [Excel/README.md](Excel/README.md)).
   After each import DefineEditor offers to upgrade the database tables of that form.
4. **Run.** Press F5: the server starts and opens the page in the browser. Sign in to the company `POLHEM_COMPANY_ID` as `admin`
   with the password from step 2. The menu lists the forms you imported.

Run the **Polhem: Upgrade database** task after you change table definitions by hand. The same commands are available
from a terminal: `dotnet run --project src/PolhemApp.Server -- db create | check | upgrade | seed-admin`.

The framework's `Department`, `Employee` and `AuditRule` forms are permission-checked and closed by default: grant
their permission models to a role in `st_role_grant`, one row per action, before a user can open them or pick a
department or an employee from one of your forms.

The sign-in belongs to the browser tab: the access token is kept in memory only, so a reload or a new tab shows the
sign-in screen again. The language is picked
on the sign-in screen, before signing in.

<!--#if (ui == "fluent") -->
## Fluent UI

The browser client is built on [Polhem.Web.Blazor.Fluent](https://github.com/polhem-dev/polhem/blob/main/src/Polhem.Web.Blazor.Fluent/README.md),
the Fluent UI package of the Polhem Blazor head. It behaves as the MudBlazor package does, with the differences its
README lists. The one that matters most for a page of your own: a dialog renders where it is placed, so it does not
see the `FormScope` around it.

<!--#endif -->
## The browser client calls the API like any remote client

Everything the browser client does goes through the server's JSON-RPC API, which checks each call as it checks a
remote client's: the forms check the signed-in user's permissions, and the system calls a remote client may not make
are refused. What differs from the other Polhem clients is in the
[Polhem.Web.Blazor.Wasm README](https://github.com/polhem-dev/polhem/blob/main/src/Polhem.Web.Blazor.Wasm/README.md#what-differs-from-blazor-server).
Two of those matter before you deploy:

- **The API key is public.** It is in `src/PolhemApp.Client/wwwroot/appsettings.json`, which every visitor downloads.
  The key names the application and protects nothing.
- **Serve the site over HTTPS.** The browser runtime cannot encrypt the calls itself, so the sign-in password is
  protected by HTTPS alone.

Other clients (the Avalonia front ends, or a JavaScript client) can call the same `/api`.

## Before deploying

Publish the server (`dotnet publish src/PolhemApp.Server -c Release`); the output includes the browser client. Once
you issue an API key, put it in `src/PolhemApp.Client/wwwroot/appsettings.json`.

`Define/SystemSettings.xml` is set up for development: debug mode is on, the master key is the file
`Define/Master.key` (ignored by git), the database password is stored in clear text, and the server accepts any API
key until one is issued. The comment at the top of
that file lists what to change.
