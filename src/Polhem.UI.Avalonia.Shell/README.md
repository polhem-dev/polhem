# Polhem.UI.Avalonia.Shell

> A ready-made Avalonia front end for a Polhem backend: connect, sign in to a company, and open every form of the
> server's menu. One package for the desktop, browser (WebAssembly), iOS and Android heads.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: UI Layer, at head level (beside an application's own UI project)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md).
- It references [`Polhem.UI.Avalonia`](../Polhem.UI.Avalonia/README.md) for the form controls and
  [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.md) for the connection and sign-in. `Polhem.UI.Avalonia`
  must not reference the bridge ([ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)), so a
  front end that talks to a backend lives one level up, and this package is that level.
- **Single-user heads only**, like the bridge it builds on.

## Target Framework

- `net10.0`

## What It Does

| Step | Screen |
|------|--------|
| Connect | Endpoint and API key, pinged before moving on. Both are remembered. |
| Sign in | Company ID, user ID and password. `Login`, then `EnterCompany`. The company starts as the last one entered on this device. |
| Forms | A menu built from the server's `MenuSettings`; each entry opens its form in a tab, as a `ListView` that swaps in a `FormView` for the selected record. **Sign out** returns to sign-in; signing in again fetches every definition anew, so a definition changed while the application runs shows without restarting it. |

The interface language before sign-in follows the operating system (English or Traditional Chinese; anything else
falls back to English) and can be switched on the connection and sign-in screens; the choice is remembered. A
`Language=en` or `Language=zh-TW` argument on the command line sets it for one run without changing the remembered
choice, which is what scripted runs such as smoke tests use. After sign-in the account's culture decides, as
`ClientInfo.ApplyLoginResult` makes it the process culture.

The shell applies the Semi theme in DefineEditor's colours (`Polhem.UI.Avalonia`'s `VisualStudioTheme`; light by default, with a light/dark switch on the forms screen) and registers the API
bridge as `UIServices.Provider`. It adds no font: the head chooses one.

## Usage

Derive from `ShellApplication` and supply the options:

```csharp
using Polhem.UI.Avalonia.Shell;

public class MyApp : ShellApplication
{
    protected override ShellOptions CreateOptions() => new()
    {
        AppTitle = "My App",
        DefaultEndpoint = "http://localhost:5000/api",
        DefaultApiKey = "my-app-key",
        DefaultCompanyId = "ACME",
    };
}
```

Start it from each head like any Avalonia application:

```csharp
// Desktop
AppBuilder.Configure<MyApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);

// iOS / Android
public partial class AppDelegate : AvaloniaAppDelegate<MyApp> { }
```

Every option is described in the XML documentation of `ShellOptions`. What differs per head goes through
`ShellApplication.ConfigureOptions`. A browser head has no persistent file system, so it keeps the endpoint, the API
key and the shell's preferences in `localStorage` through `BrowserLocalStorage`, and turns the language switch off when
its font cannot draw CJK text:

```csharp
var storage = new BrowserLocalStorage("MyApp");
ClientInfo.EndpointStorage = storage;
ClientInfo.ApiKeyStorage = storage;

AppBuilder.Configure(() => new MyApp
{
    ConfigureOptions = options =>
    {
        options.PreferenceStorage = storage;
        options.AllowLanguageSwitch = false;
    },
});
```

The default preference storage, `FileShellPreferenceStorage`, writes to the per-user folder the bridge's
`FileEndpointStorage` uses.

## macOS Application Bundle

The package carries an MSBuild target that wraps a published macOS head into a `.app` bundle, so it opens from Finder
and the Dock and other tools can address it by its bundle identifier:

```xml
<PropertyGroup>
  <PolhemBundleIdentifier>tw.example.myapp</PolhemBundleIdentifier>
</PropertyGroup>
```

```bash
dotnet publish MyApp.Desktop.csproj -c Release -r osx-arm64 -p:PolhemMacAppBundle=true
```

The bundle is written to `<publish folder>/<name>.app`. Add `--self-contained` to carry the .NET runtime in it. The
properties (`PolhemBundleName`, `PolhemBundleIcon`) are described in the header of
`buildTransitive/Polhem.UI.Avalonia.Shell.targets`. The bundle is not signed.

A package reference imports the target automatically. A project in this repository that references the shell as a
project imports the file itself, as `apps/Polhem.Northwind/Polhem.Northwind.Desktop` does.

## Example

[`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.md) runs on this shell: its UI project is one class,
`NorthwindApp`, that supplies the options.
