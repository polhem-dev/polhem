# PolhemApp

A [Polhem](https://github.com/polhem-dev/polhem) application. Its forms are definitions under `Define/`: each form
describes its fields, lookups and rules, and the framework provides the database tables, the API and the screens.

| Folder | Content |
|--------|---------|
| `src/PolhemApp.Server` | The JSON-RPC server (`http://localhost:5180/api`) |
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
4. **Run.** Press F5 to start the server.

Run the **Polhem: Upgrade database** task after you change table definitions by hand. The same commands are available
from a terminal: `dotnet run --project src/PolhemApp.Server -- db create | check | upgrade | seed-admin`.

## Before deploying

`Define/SystemSettings.xml` is set up for development: debug mode is on, the master key is the file
`Define/Master.key` (ignored by git), and the database password is stored in clear text. The comment at the top of
that file lists what to change.
