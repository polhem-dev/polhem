# PolhemApp

A [Polhem](https://github.com/polhem-dev/polhem) application. Its forms are definitions under `Define/`: each form
describes its fields, lookups and rules, and the framework provides the database tables, the API and the screens.

| Folder | Content |
|--------|---------|
| `src/PolhemApp.Server` | The JSON-RPC server (`http://localhost:5180/api`) |
| `src/PolhemApp.UI` | The front end every client starts: the Polhem shell (connect, sign in, and the forms of the server's menu) with this application's defaults |
| `src/PolhemApp.Desktop` | The Avalonia desktop client |
<!--#if (browser) -->
| `src/PolhemApp.Browser` | The browser (WebAssembly) client |
<!--#endif -->
<!--#if (ios) -->
| `src/PolhemApp.iOS` | The iOS client |
<!--#endif -->
<!--#if (android) -->
| `src/PolhemApp.Android` | The Android client |
<!--#endif -->
| `Define` | The definition files. The server adds the framework's default definitions on its first run |
| `Excel` | The form workbooks you import into `Define` |

## Requirements

- The .NET 10 SDK, and VS Code with the C# Dev Kit extension.
- A database server the connection in `Define/DatabaseSettings.xml` can reach (on Oracle, the three schemas it names).
<!--#if (browser) -->
- For the browser client, the WebAssembly tools: `dotnet workload install wasm-tools`.
<!--#endif -->
<!--#if (ios) -->
- For the iOS client, macOS, the iOS workload (`dotnet workload install ios`) and the Xcode version its build names.
<!--#endif -->
<!--#if (android) -->
- For the Android client, the Android workload (`dotnet workload install android`), the Android SDK and an emulator.
<!--#endif -->

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

<!--#if (browser || ios || android) -->
## The other clients

Start the server first (F5, or the **Server** launch configuration). Every client signs in the same way.

<!--#if (browser) -->
- **Browser.** The **Server + Browser** launch configuration starts the server and the browser client's development
  server and opens `http://localhost:5380`. In development the server allows calls from any `localhost` origin (CORS);
  in production, serve the published client from the server's own origin and remove that policy from `Program.cs`.
  The bundled font has no Chinese, Japanese or Korean glyphs, and the browser has no system font to borrow them from,
  so this client shows those languages as empty boxes; sign in with an account whose culture is English here.
<!--#endif -->
<!--#if (ios) -->
- **iOS.** Run the **Polhem: Run iOS simulator** task. The simulator shares the machine's network, so the endpoint
  `http://localhost:5180/api` works as it is. `Info.plist` allows plain HTTP for development; remove that exception
  before shipping.
<!--#endif -->
<!--#if (android) -->
- **Android.** Start an emulator, then run the **Polhem: Run Android emulator** task. Inside the emulator `localhost`
  is the emulator itself, so the client's endpoint is `http://10.0.2.2:5180/api`, the emulator's address for this
  machine. `AndroidManifest.xml` allows plain HTTP for development; remove `usesCleartextTraffic` before shipping.
<!--#endif -->

<!--#endif -->
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
