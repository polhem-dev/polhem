---
name: polhem-sample-add
description: Add a new samples/ project to polhem, covering the choice of front-end type (Console / Blazor Server / Avalonia / WinForms), the backend pairing decision tree (QuickStart.Server / in-process Local), deciding whether auth is needed, `Polhem.Samples.slnx` integration, a README template and default ProjectReference values. Use when the user wants to "add a new sample", "add a demo", "build a demo for some src package", and similar requests.
---

# polhem: add a sample

Adding a `samples/<Sample.Name>/` demo involves 5 related decisions: front-end type, backend, auth mode, shared Define
references, and slnx folder. Each decision has a "right-answer table" to look up; this skill pins that table down so it
does not have to be re-explored every time.

## When to use

- Building a new demo for some src package (e.g. a `WinForms.Demo` once `Polhem.WinForms` exists)
- Building a demo for a scenario (e.g. "Avalonia desktop using OAuth", "Console running a batch import")
- An existing sample is too thin and you want to split out a separate demo (e.g. splitting the advanced features of
  `Blazor.Server.Demo` into `Blazor.Server.Advanced.Demo`)

## When not to use

- Small feature changes to an existing sample — edit it directly, no scaffolding needed
- Showcasing pure library code with no UI / no entry point — docs / an ADR fit better
- An app outside the samples directory that is meant to ship — goes through `src/` and the NuGet flow, outside this
  skill's scope

## Division of labour with related skills

| Skill | Handles |
|-------|---------|
| **`polhem-sample-add`** (this skill) | The sample's Polhem integration: which backend, auth, slnx, README, dependency settings |
| **`demo-smoke`** (this repository) | Verifying the demo runs after scaffolding |

For an Avalonia sample: first create the UI project and the platform heads you need → then invoke this skill to wire in
the Polhem backend. For the trim / AOT pitfalls of mobile heads, see `rules/apple-mobile-trim.md`.

## The 5 decisions you must ask about

### Decision 1: front-end type

```
1. Console        → a Polhem.Api.Client consumer demo (Ping / Login / calling a BO)
2. Blazor Server  → in-process Local provider, server-rendered, fastest to get started
3. Avalonia       → uses the remote API; desktop / iOS / Android / WASM share one UI project, platform heads built separately
4. WinForms       → either Local or Remote (skip while Polhem.UI.WinForms has not landed yet)
```

Use `AskUserQuestion` to pick one.

### Decision 2: backend pairing

Depends on the front-end decision + whether auth is needed:

| Front end | auth=no (Public BO only) | auth=yes (FormBO etc., login required) |
|-----------|--------------------------|----------------------------------------|
| Console | Connect to `QuickStart.Server` (5050), call the Echo BO | Connect to `QuickStart.Server` (5050), log in with `demo/demo` |
| Blazor Server | in-process (`builder.AddPolhemBackend()`) | in-process + `DemoAuthenticatingSystemBusinessObject` (already included in `DemoBackend`) |
| Avalonia | Connect to `QuickStart.Server` (5050) | Connect to `QuickStart.Server` (5050), `demo/demo` |
| WinForms | Either Local or Remote | Same as above |

**Key facts**:
- `QuickStart.Server` currently hosts `Polhem.Samples.Shared.DemoBackend`, so it **has** `demo/demo` login + the
  Employee / Department / Project seed data. Samples other than Console that want auth all connect to this server
- in-process mode is for Blazor Server only; Avalonia and other non-web hosts cannot run in-process (there is no
  `WebApplicationBuilder`); instead pass the `IServiceProvider` to the local connector constructors (or set
  `ClientInfo.LocalServiceProvider` on a native head) and use local mode

### Decision 3: is login needed?

Ask the user which BO actions the sample should show:

| Action category | Example BO method | auth? |
|-----------------|-------------------|-------|
| `Ping` (connectivity test) | `SystemApiConnector.PingAsync` | No (Public / Anonymous) |
| `Echo` custom BO | `EchoBusinessObject.Echo` | No (Public / Anonymous) |
| `GetDefine` to read a schema | `SystemApiConnector.GetDefineAsync` | **Yes** (Public / Authenticated) |
| FormBO CRUD (`GetList` / `GetData` / `Save` / `Delete`) | `FormApiConnector.*` | **Yes** (Public / Authenticated) |
| Custom ExecFunc | A custom method in `Polhem.Business` | Depends on its attribute |

If any action is `Authenticated` → the backend must host `DemoBackend` (for demo/demo), and the demo code must include a
Login step.

### Decision 4: shared Define references

| Define needed | How to reference it |
|---------------|---------------------|
| An existing FormSchema (`Employee`, `Department`, `Project`) | The backend reads `samples/Define/FormSchema/` (the directory is found by `DemoBackend.ResolveDefinePath()`) |
| Custom FormSchema | Add `samples/Define/FormSchema/<ProgId>.FormSchema.xml` and its `samples/Define/FormLayout/<ProgId>.FormLayout.xml` (required: opening a form without one fails; generate it with `polhem-scaffold-from-formschema`), the TableSchema under `samples/Define/TableSchema/common/`, a `TableItem` under the `common` category in `samples/Define/DbCategorySettings.xml`, and in `Polhem.Samples.Shared/DemoSchemaSeeder.cs` a `builder.Execute("common", "<table>")` line in `EnsureSchema` (this seeder lists its tables by hand; it does not walk `DbCategorySettings`) plus seed data if the list should not start empty |
| No schema at all | A pure Echo / Ping demo, no Define dependency |

When adding a new FormSchema, also update the file list in the `/Define/` folder of `Polhem.Samples.slnx` (not required,
but it keeps the IDE tree tidier).

> **Samples keep their `ft_*` tables in `common`, which `.claude/rules/database.md` calls wrong for business data.**
> It is what the sample hosts do today: they never call `EnterCompany` (see the comment in `DemoBackend.cs`), so a
> `CategoryId="company"` form would throw `CompanyNotEnteredException`, and `samples/Define/DatabaseSettings.xml` /
> `DbCategorySettings.xml` define only `common`. Follow it inside `samples/` so the new form works with the shared
> hosts, and do not copy it anywhere else: an app outside `samples/` puts business tables in `company` and enters the
> company after login (`polhem-app-scaffold` Part 1 and Part 3).

### Decision 5: which slnx folder

Existing folders in `samples/Polhem.Samples.slnx`:

```
/QuickStart/        → entry-level demos (pure API client / pure server)
/Blazor/            → the Blazor family + Polhem.Samples.Shared
/Avalonia/          → the Avalonia family (desktop / mobile / WASM)
```

Where a new sample goes:
- Console / API host kinds → `/QuickStart/`
- Any Blazor variant → `/Blazor/`
- Any Avalonia variant → `/Avalonia/`
- Others (WinForms / WPF etc.) → open a new folder `/<Family>/`

## Procedure

### Step 1: ask the 5 questions

Use `AskUserQuestion` to ask the 5 decisions in a row (or merge them into 2-3 multiple-choice questions).

### Step 2: sanity-check whether an existing sample already covers it

```bash
ls samples/
grep -r "ProgId.*=.*\"<NewSample>\"" samples/ 2>/dev/null
```

If something of the same kind already exists (e.g. the sample wants to be an "Avalonia controls showcase" but
`Avalonia.DemoCenter` already exists; or a "complete Avalonia app connecting to a remote server" but
`apps/Polhem.Northwind` already exists) → stop and ask the user whether to add a new one or extend the existing one.

### Step 3: build the skeleton

**Avalonia sample**: first create the UI project and the platform heads you need, then come back to this procedure to
add the Polhem integration.
**Others**: write the csproj + code directly, following the templates below:

#### `samples/<Sample.Name>/<Sample.Name>.csproj` template

**Console**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{Sample.Name}</RootNamespace>
    <AssemblyName>{Sample.Name}</AssemblyName>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Polhem.Api.Client\Polhem.Api.Client.csproj" />
  </ItemGroup>
</Project>
```

**Blazor Server**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{Sample.Name}</RootNamespace>
    <AssemblyName>{Sample.Name}</AssemblyName>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Polhem.Web.Blazor.Server\Polhem.Web.Blazor.Server.csproj" />
    <ProjectReference Include="..\Polhem.Samples.Shared\Polhem.Samples.Shared.csproj" />
  </ItemGroup>
</Project>
```

### Step 4: Program.cs templates

#### Console template (auth=no)

See `samples/QuickStart.Console/Program.cs`: first set `ApiClientInfo.ApiKey`, then
`new SystemApiConnector(endpoint, Guid.Empty)` + `PingAsync()`, and call the Echo BO with
`ExecuteAsync<T>("Echo", request, PayloadFormat.Plain)` on `new FormApiConnector(endpoint, Guid.Empty, "Echo")`.

#### Console template (auth=yes)

`ClientInfo` lives in `Polhem.UI.Core`, so in addition to `Polhem.Api.Client` from the Console template above, the csproj
also needs `<ProjectReference Include="..\..\src\Polhem.UI.Core\Polhem.UI.Core.csproj" />`.

```csharp
using Polhem.Api.Client;
using Polhem.UI.Core;

namespace {Sample.Name};

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ApiClientInfo.ApiKey = "{sample-key}";
        await ClientInfo.InitializeAsync("http://localhost:5050/api"); // QuickStart.Server

        var login = await ClientInfo.SystemApiConnector.LoginAsync("demo", "demo");
        ClientInfo.ApplyLoginResult(login);

        // ... do authenticated work via ClientInfo.SystemApiConnector / CreateFormApiConnector
        return 0;
    }
}
```

#### Blazor Server template

See `samples/Blazor.Server.Demo/Program.cs`:

```csharp
using Polhem.Samples.Shared;
using Polhem.Web.Blazor.Server.DependencyInjection;
using {Sample.Name}.Components;

namespace {Sample.Name};

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // Must run before AddPolhemBlazor so the Local provider has services to resolve.
        builder.AddPolhemBackend();
        builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        var app = builder.Build();
        app.UsePolhemBackend();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        app.Run();
    }
}
```

### Step 5: slnx integration

Edit `samples/Polhem.Samples.slnx` and add to the matching folder:

```xml
<Folder Name="/{Family}/">
  ...
  <Project Path="{Sample.Name}/{Sample.Name}.csproj" />
</Folder>
```

### Step 6: README

Samples READMEs are bilingual: write `samples/<Sample.Name>/README.md` (English) and `README.zh-TW.md` (Traditional
Chinese) together, following the template below. Each file starts with the language switch line the existing samples
use (`**English** | [繁體中文](README.zh-TW.md)` in the English file, `[English](README.md) | **繁體中文**` in the
Chinese one); the Chinese file translates the same sections.

```markdown
# {Sample.DisplayName}

**English** | [繁體中文](README.zh-TW.md)

{One sentence on what the sample sets out to prove, e.g. "A console app calls a remote BO over JSON-RPC"}

## Prerequisites

{Environment requirements for Console / Blazor / Avalonia respectively, e.g. Android SDK, Xcode, SQL container}

## Running it

{Step-by-step commands; when a backend must be started separately, first list "run X in another terminal"}

## Expected screens / output

{What you see at each step; include the demo/demo credentials hint (if needed)}

## Corresponding library components

| Demo behaviour | Library component |
|----------------|-------------------|
| {step 1} | [src/Polhem.X/Y.cs](../../src/Polhem.X/Y.cs) |
| ... | ... |

## Relationship to other samples

{If it overlaps with or shares a host with an existing sample, state that explicitly here}

## Out of scope

{Deliberately excluded scope, so it is not mistaken for a missing feature}
```

Also add the sample to the list in `samples/README.md` and `samples/README.zh-TW.md`.

### Step 7: build + one run

```bash
dotnet build samples/{Sample.Name}/{Sample.Name}.csproj --configuration Debug
```

For Avalonia mobile heads add the matching `-f net10.0-ios` / `net10.0-android`. If the build fails, stop and fix it; if
it succeeds, **do not** `dotnet run` automatically — let the user run it (to avoid tying a background process to the
session).

### Step 8: commit suggestion

When the skill is done, output a suggested commit message; **do not** commit automatically:

```
feat(samples): add {Sample.Name} — {one-line description}

- References Polhem.X / Polhem.Y
- Backend: {QuickStart.Server / in-process}
- Auth: {demo/demo / anonymous}
```

## Known pitfalls

- **Non-web samples cannot reference Polhem.Samples.Shared** (it has an AspNetCore framework reference) — shared
  constants need another approach
- **DemoBackend is shared**: `QuickStart.Server` and `Blazor.Server.Demo` share `DemoBackend`; changing it affects both
- **`Polhem.Samples.slnx` and `Polhem.slnx` are separate solutions**: samples are not in the main solution and CI does
  not build samples; to verify sample changes locally you must run the build by hand
- **`samples/**/quickstart.db` is gitignored**: it is created on the first run; do not commit it. There is no master
  key file: `samples/Define/SystemSettings.xml` reads the key from the `POLHEM_MASTER_KEY` environment variable, and
  `DemoBackend.AddPolhemBackend` sets it to `DemoCredentials.DemoMasterKey` when it is unset (see "Master key" in
  `samples/README.md`)
- **The Echo BO is anonymous Public** — to add a new anonymous BO, copy `EchoBusinessObject` + bind its progId in
  `samples/Define/ProgramSettings.xml` (the `BusinessObject` attribute); to add an authenticated BO, go through the
  DemoBackend path in `samples/Polhem.Samples.Shared`
- **Do not register your own `IBoTypeResolver` in a sample host**: the last registration replaces the framework's
  ProgramSettings-based resolver, so the `System` binding to `DemoAuthenticatingSystemBusinessObject` stops applying
  and demo/demo login is rejected. QuickStart.Server did exactly this until 2026-09-27

## Outside this skill's scope

- Real BO implementations (use `polhem-add-bo-method`)
- UI verification of running the demo (use `demo-smoke`)
- Editing the main README to add a Quick Start link (manual edit, needs review)
- Listing sample changes in the CHANGELOG (done when the CHANGELOG is drafted for a release)
