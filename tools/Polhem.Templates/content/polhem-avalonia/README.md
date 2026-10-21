# PolhemApp

A [Polhem](https://github.com/polhem-dev/polhem) application. Its forms are definitions under `Define/`: each form
describes its fields, lookups and rules, and the framework provides the database tables, the API and the screens.

| Folder | Content |
|--------|---------|
| `src/PolhemApp.Server` | The JSON-RPC server (`http://localhost:5180/api`) |
| `src/PolhemApp.Desktop` | The Avalonia desktop client, built on the Polhem shell: connect, sign in, and the forms of the server's menu |
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
4. **Run.** Press F5: **Server + Desktop** starts both. Connect (the endpoint is filled in), then sign in to the
   company `POLHEM_COMPANY_ID` as `admin` with the password from step 2. The menu lists the forms you imported.

Run the **Polhem: Upgrade database** task after you change table definitions by hand. The same commands are available
from a terminal: `dotnet run --project src/PolhemApp.Server -- db create | check | upgrade | seed-admin`.

## Packaging the desktop client for macOS

```bash
dotnet publish src/PolhemApp.Desktop -c Release -r osx-arm64 -p:PolhemMacAppBundle=true
```

writes `PolhemApp.app` into the publish folder. Its bundle identifier is `PolhemBundleIdentifier` in
`src/PolhemApp.Desktop/PolhemApp.Desktop.csproj`; replace `com.example` with your own domain.

## Before deploying

`Define/SystemSettings.xml` is set up for development: debug mode is on, the master key is the file
`Define/Master.key` (ignored by git), the database password is stored in clear text, and the server accepts any API
key until one is issued. The comment at the top of
that file lists what to change.
