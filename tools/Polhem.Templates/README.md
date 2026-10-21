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

Creates an application with an Avalonia desktop front end, and optionally browser, iOS and Android front ends. The
server references the Polhem packages and the driver of one database:

| Path | Content |
|------|---------|
| `src/<Name>.Server` | An ASP.NET Core host serving the JSON-RPC API, with the `db create`, `db check`, `db upgrade` and `db seed-admin` commands of `Polhem.Hosting` (`dotnet run -- db ...`) |
| `src/<Name>.UI` | The front end every client starts: [Polhem.UI.Avalonia.Shell](../../src/Polhem.UI.Avalonia.Shell/README.md) starting with the server's endpoint and the first company |
| `src/<Name>.Desktop` | The Avalonia desktop client; `-p:PolhemMacAppBundle=true` on `dotnet publish` makes a macOS `.app` |
| `src/<Name>.Browser`, `.iOS`, `.Android` | With `--browser`, `--ios`, `--android`: the browser (WebAssembly), iOS and Android clients. The Android client reaches the server at `10.0.2.2`, the emulator's address for the machine that runs it. With `--browser` the server also allows calls from any `localhost` origin in development (CORS) |
| `Define/` | `SystemSettings.xml` set up for development and a `DatabaseSettings.xml` with the common, company and log databases. The server adds the framework's other default definitions on its first run |
| `Excel/` | The place for form workbooks, imported into `Define/` with DefineEditor |
| `.config/dotnet-tools.json` | DefineEditor (`polhem-define`) and DbManager (`polhem-db`), at the version of the Polhem packages |
| `.vscode/` | Tasks **Polhem: Initialize database**, **Polhem: Upgrade database** and **Polhem: Open DefineEditor**, the F5 launch **Server + Desktop**, and with the other clients **Server + Browser**, **Polhem: Run iOS simulator** and **Polhem: Run Android emulator** |

The generated `README.md` walks through the first run: set the database password, initialize the database, import
forms from Excel, press F5.

### Options

| Option | Default | Meaning |
|--------|---------|---------|
| `--database` | `SqlServer` | `SqlServer`, `PostgreSql`, `MySql`, `Oracle` or `Sqlite`: the driver package, its registration and the connection in `DatabaseSettings.xml` |
| `--company` | `DEMO` | The id of the first company, which the **Polhem: Initialize database** task creates |
| `--db-password` | empty | The database password written to `DatabaseSettings.xml`; when empty, enter it in DefineEditor |
| `--default-language` | `en-US` | `en-US` or `zh-TW`: the default language of users who have none of their own |
| `--browser` | off | Add the browser client. Building it needs the `wasm-tools` workload |
| `--ios` | off | Add the iOS client. Building it needs macOS, the `ios` workload and the Xcode that workload names |
| `--android` | off | Add the Android client. Building it needs the `android` workload and the Android SDK |

The client options are off by default, so an application created without them builds on any machine with the .NET SDK.

The generated application references the Polhem packages of the same version as this template package.

### Known limitation: Chinese in the browser client

The browser client's font, Inter, has no Chinese, Japanese or Korean glyphs, and the browser has no system font to
borrow them from. Its screens before sign-in are in English; after sign-in, an account whose culture is Chinese shows
empty boxes. Sign in to the browser client with an account whose culture is English.

### Known limitation: SQLite and DefineEditor

The SQLite connection uses a path relative to the application folder, and DbManager, which DefineEditor runs to
upgrade the database after an import, refuses relative SQLite paths. With SQLite, run the **Polhem: Upgrade database**
task after an import instead.
