# PolhemApp

A [Polhem](https://github.com/polhem-dev/polhem) application. Its forms are definitions under `Define/`: each form
describes its fields, lookups and rules, and the framework provides the database tables, the API and the screens.

| Folder | Content |
|--------|---------|
| `src/PolhemApp.Web` | The Blazor Server site (`http://localhost:5180`). It runs the backend in its own process and serves the Polhem shell: sign-in, the menu and the forms |
| `Define` | The definition files. The site adds the framework's default definitions on its first run |
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
4. **Run.** Press F5: the site starts and opens in the browser. Sign in to the company `POLHEM_COMPANY_ID` as `admin`
   with the password from step 2. The menu lists the forms you imported.

Run the **Polhem: Upgrade database** task after you change table definitions by hand. The same commands are available
from a terminal: `dotnet run --project src/PolhemApp.Web -- db create | check | upgrade | seed-admin`.

The sign-in belongs to the browser tab: a reload or a new tab shows the sign-in screen again. The language is picked
on the sign-in screen, before signing in.

## Pages of your own

The site's pages call the backend in the same process, so their calls are trusted local calls. The forms still check
the signed-in user's permissions, but the system calls a remote client may not make (reading any definition, starting
a session without credentials, managing API keys) are open to them. A page you add must not pass what the browser
sends into those calls. The comment in `Program.cs` says the same.

To serve the same backend to other clients (the Avalonia front ends, or a JavaScript client), add the JSON-RPC
endpoint to this site: reference `Polhem.JsonRpc.AspNetCore`, then call `builder.Services.AddJsonRpcServer()` and
`app.MapJsonRpc("/api")`. Those clients' calls are checked like any remote client's.

## Before deploying

`Define/SystemSettings.xml` is set up for development: debug mode is on, the master key is the file
`Define/Master.key` (ignored by git), and the database password is stored in clear text. The comment at the top of
that file lists what to change.
