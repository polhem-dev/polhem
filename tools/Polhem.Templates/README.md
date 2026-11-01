# Polhem.Templates

**English** | [繁體中文](README.zh-TW.md)

`dotnet new` templates for [Polhem](https://github.com/polhem-dev/polhem). Each template is named after the front-end
technology of the application it creates (`polhem-<front end>`); the database is an option of the template.

```bash
dotnet new install Polhem.Templates
dotnet new polhem-avalonia -n Acme.Erp --database SqlServer
dotnet new polhem-blazor-server -n Acme.Erp --database SqlServer
```

| Template | Name in VS Code | Creates |
|----------|-----------------|---------|
| [`polhem-avalonia`](#polhem-avalonia) | **Polhem App (Avalonia)** | A JSON-RPC server and an Avalonia desktop client, optionally browser, iOS and Android clients |
| [`polhem-blazor-server`](#polhem-blazor-server) | **Polhem App (Blazor Server)** | One Blazor Server site that runs the backend in its own process |

In VS Code with the C# Dev Kit, the templates are listed under **New .NET Project** by those names. The C# Dev Kit
creates the application with the default options; to choose others, run `dotnet new` in a terminal and open the
folder it creates.

Install the templates from the package, as above, or from a `.nupkg` file (`dotnet new install <path>.nupkg`).
Installing a template from its folder under `content/` is not supported: the files every template shares
(`Define/`, `Excel/`, `.config/`, `.gitignore`) live once in `content/_shared/` and are added to each template only
when the package is built, so a template installed from its folder lacks them.

The templates' names, descriptions and options are also translated into Traditional Chinese. `dotnet new` shows them
when its language is Traditional Chinese (`DOTNET_CLI_UI_LANGUAGE=zh-TW`), and the C# Dev Kit when the operating
system's language is; every other language shows the English.

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
| `.vscode/` | Tasks **Polhem: Initialize database**, **Polhem: Upgrade database** and **Polhem: Open DefineEditor**, the F5 launch **Server + Desktop**, and with the other clients **Server + Browser**, **Polhem: Run iOS simulator**, **Polhem: Run Android emulator** and **Polhem: Run all** (the server and every client at once) |

The generated `README.md` walks through the first run: set the database password, initialize the database, import
forms from Excel, press F5.

### Options

| Option | Default | Meaning |
|--------|---------|---------|
| `--database` | `SqlServer` | `SqlServer`, `PostgreSql`, `MySql`, `Oracle` or `Sqlite`: the driver package, its registration and the connection in `DatabaseSettings.xml` |
| `--company` | `DEMO` | The id of the first company, which the **Polhem: Initialize database** task creates |
| `--db-password` | empty | The database password written to `DatabaseSettings.xml`; when empty, enter it in DefineEditor |
| `--default-language` | `en-US` | `en-US` or `zh-TW`: the default language of users who have none of their own, and the language of the client's connection and sign-in screens until the viewer picks another |
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
task after an import instead. This applies to `polhem-blazor-server` as well.

## `polhem-blazor-server`

Creates an application with a Blazor Server front end: one ASP.NET Core site that runs the backend in its own process
and serves the [Blazor Server shell](../../src/Polhem.Web.Blazor.Server/README.md#shell) (sign-in, the server's menu
and the forms). There is no separate server to start, and no API endpoint until you add one.

| Path | Content |
|------|---------|
| `src/<Name>.Web` | The site: the backend (`AddPolhemBackend`) with the `db create`, `db check`, `db upgrade` and `db seed-admin` commands of `Polhem.Hosting` (`dotnet run -- db ...`), the Polhem shell, and one page that shows every form |
| `Define/`, `Excel/`, `.config/dotnet-tools.json` | As in `polhem-avalonia` |
| `.vscode/` | Tasks **Polhem: Initialize database**, **Polhem: Upgrade database** and **Polhem: Open DefineEditor**, and the F5 launch **Web** |

Its options are `--database`, `--company`, `--db-password` and `--default-language`, as in `polhem-avalonia`; the
default language is also the language of the sign-in screen until the viewer picks another.

The pages call the backend in the same process, so their calls are trusted local calls: the forms check the signed-in
user's permissions, but the system calls a remote client may not make are open to them. The generated `Program.cs` and
`README.md` say what that means for a page you add.
