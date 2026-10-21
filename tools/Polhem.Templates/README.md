# Polhem.Templates

**English** | [繁體中文](README.zh-TW.md)

`dotnet new` templates for [Polhem](https://github.com/polhem-dev/polhem). Each template is named after the front-end
technology of the application it creates (`polhem-<front end>`); the database is an option of the template.

```bash
dotnet new install Polhem.Templates
dotnet new polhem-avalonia -n Acme.Erp --database SqlServer
```

In VS Code with the C# Dev Kit, the template is listed as **Polhem App (Avalonia)** under **New .NET Project**.

## `polhem-avalonia`

Creates an application with an Avalonia front end. In this version it creates the server, the definitions and the
tools; the Avalonia desktop project is not part of it yet. The server references the Polhem packages and the driver of
one database:

| Path | Content |
|------|---------|
| `src/<Name>.Server` | An ASP.NET Core host serving the JSON-RPC API, with the `db create`, `db check`, `db upgrade` and `db seed-admin` commands of `Polhem.Hosting` (`dotnet run -- db ...`) |
| `Define/` | `SystemSettings.xml` set up for development and a `DatabaseSettings.xml` with the common, company and log databases. The server adds the framework's other default definitions on its first run |
| `Excel/` | The place for form workbooks, imported into `Define/` with DefineEditor |
| `.config/dotnet-tools.json` | DefineEditor (`polhem-define`) and DbManager (`polhem-db`), at the version of the Polhem packages |
| `.vscode/` | Tasks **Polhem: Initialize database**, **Polhem: Upgrade database** and **Polhem: Open DefineEditor**, and the F5 launch of the server |

The generated `README.md` walks through the first run: set the database password, initialize the database, import
forms from Excel, press F5.

### Options

| Option | Default | Meaning |
|--------|---------|---------|
| `--database` | `SqlServer` | `SqlServer`, `PostgreSql`, `MySql`, `Oracle` or `Sqlite`: the driver package, its registration and the connection in `DatabaseSettings.xml` |
| `--company` | `DEMO` | The id of the first company, which the **Polhem: Initialize database** task creates |
| `--db-password` | empty | The database password written to `DatabaseSettings.xml`; when empty, enter it in DefineEditor |
| `--language` | `en-US` | `en-US` or `zh-TW`: the default language of users who have none of their own |

The generated application references the Polhem packages of the same version as this template package.

### Known limitation: SQLite and DefineEditor

The SQLite connection uses a path relative to the application folder, and DbManager, which DefineEditor runs to
upgrade the database after an import, refuses relative SQLite paths. With SQLite, run the **Polhem: Upgrade database**
task after an import instead.
