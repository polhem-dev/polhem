# Pitfall log: the four Northwind heads and the standalone repository sync

Since 2026-06-26, `apps/Polhem.Northwind` has had **four heads: Desktop / Browser / iOS / Android**, sharing
`Polhem.Northwind.UI` (Avalonia App/VM/View) and the backend `Polhem.Northwind.Server`.
The web case is an **Avalonia Browser (WASM) backend**, **not a separate Blazor app** (`.UseBrowser` vs `.UseDesktop`
are symmetric).

`apps/` is not built by polhem's CI (see [test-ci-release.md](test-ci-release.md)): if **this copy** breaks, nobody
will tell you. **Where the heads get built today**: the in-repo heads (Desktop, Browser, iOS, Android) are built only
by hand, with the commands in test-ci-release.md and below. The CI of the standalone repository
[`polhem-dev/polhem-northwind`](https://github.com/polhem-dev/polhem-northwind) (see "The standalone repository's
CI" below) builds that repository's own copy against published NuGet packages; it cannot catch the in-repo copy
drifting against `src/`.
The unit test projects of the packages a head ships run under `-p:DynamicCodeSupport=false` in the mobile AOT gate of
`build-ci.yml`, which covers the no-dynamic-code half but builds no head.

## Android head

**Toolchain** (these have to be relearned on every new machine):

- `net10.0-android` compiles against API **36**; the SDK needs `platforms;android-36` (+ `build-tools;36.0.0`).
  Having only .NET's `Microsoft.Android.Ref.36` ref pack is **not enough**: without platforms you get XA5207 "cannot
  find android.jar".
- The `maui-android` workload already includes `Microsoft.Android.Sdk.Darwin` → `dotnet workload install android` is
  **not needed**.
- A JDK (17) and the Android command-line tools are needed; where they live is machine-specific. **Non-interactive
  Bash does not read `~/.zshrc`** → export your own `JAVA_HOME` / `ANDROID_HOME` before running dotnet/adb, and pick
  an AVD from `emulator -list-avds`.
- The emulator's loopback to the host is **`10.0.2.2`** (not localhost); set the endpoint to
  `http://10.0.2.2:5100/api`; AndroidManifest needs `<application android:usesCleartextTraffic="true">` (dev only;
  Android 9+ blocks cleartext by default).

**Head structure**: `[Application] class Application : AvaloniaAndroidApplication<App>` in `Application.cs`; its
`CustomizeAppBuilder` is the **only** authoritative hook that builds the AppBuilder + SetupWithLifetime → the client
wiring (`ApiClientInfo` / `ClientInfo.EndpointStorage`) goes here (the counterpart of the iOS AppDelegate).
`MainActivity : AvaloniaMainActivity` (**non-generic**) only hosts the view and takes the lifetime from Application.

`FileEndpointStorage` is writable in the Android sandbox (`/data/data/<pkg>/files/...`), but the ConnectionView field
is always prefilled with `AppDefaults.Endpoint` and does not read back from storage. That is existing behavior of the
shared UI, the same on every head.

## iOS head

**.NET for iOS is tied to an exact Xcode version, so when macOS updates Xcode the build breaks.**

The error names the Xcode version the SDK wants and the one currently selected (an example as it was once seen; the
numbers move with the workload):

```
error : This version of .NET for iOS (26.5.10284) requires Xcode 26.5.
The current version of Xcode is 26.6. Either install Xcode 26.5, or use a
different version of .NET for iOS.
```

The fix is to **install the Xcode that the error message names side by side and select it with `DEVELOPER_DIR`**;
do not touch `xcode-select`. The latter is a machine-wide setting, needs sudo, and also affects other work that needs
the newer Xcode. First look at what is installed, then point `DEVELOPER_DIR` at the matching one:

```bash
ls -d /Applications/Xcode*.app
export DEVELOPER_DIR=/Applications/<the Xcode the error names>.app/Contents/Developer
```

The error message only says "install that version or switch the workload" and does not mention the `DEVELOPER_DIR`
route, so it is easily judged as "the environment is broken, it can only be shelved". That is exactly how the mirror
sync to Bee.NET 4.21.0 on 2026-08-13 recorded iOS as an environment problem and exempted it, when in fact both Xcodes
were already on the machine. (`.claude/rules/apple-mobile-trim.md` holds the same rule.)

**The required version moves with the workload, so no script should hard-code it**; read it from the SDK itself
instead (`_RecommendedXcodeVersion` is declared in `Microsoft.iOS.Sdk.Versions.props` and available after restore):

```bash
dotnet msbuild <iOS project> -getProperty:_RecommendedXcodeVersion -p:ValidateXcodeVersion=false
```

Evidence: on the same day, 2026-09-02, the local workload wanted **26.5** and the one on GitHub's `macos-latest` runner
wanted **26.6**. Writing the 26.5 seen locally into CI would make that job go red the very first time, somewhere
completely unrelated to the program being verified.

**On a clean tree, `-t:Run` must be run in two steps.**

```
error : The app must be built before the arguments to launch the app using
mlaunch can be computed.
```

The symptom looks like wrong mlaunch arguments or the wrong simulator, but actually, within the same MSBuild
invocation, the app bundle does not exist yet when the Run target computes the launch arguments. Build first, then
Run:

```bash
export DEVELOPER_DIR=/Applications/<the Xcode the SDK names>.app/Contents/Developer
dotnet build Polhem.Northwind.iOS -f net10.0-ios -c Debug
dotnet build Polhem.Northwind.iOS -t:Run -f net10.0-ios -c Debug \
  -p:_DeviceName=:v2:udid=<simulator UDID>
```

Get the simulator UDID from `xcrun simctl list devices available`; if `_DeviceName` is omitted the SDK picks one
itself, which with several simulators running is not necessarily the one you want. **The iOS simulator uses the
endpoint `http://localhost:5100/api`** (unlike Android's `10.0.2.2`; ATS allows arbitrary connections in dev).

Both points apply equally to `apps/Polhem.Northwind/Polhem.Northwind.iOS` inside polhem.

## Browser (WASM) head

**The csproj must add**:

```xml
<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
```

browser-wasm disables STJ reflection by default, and Polhem's `JsonCodec` (messages are not source-generated) throws
`JsonSerializerIsReflectionDisabled` already in `request.ToJson()`: **the request is never sent**, and the UI only
shows "Connection failed during Ping" (the outer layer wraps the real cause). **It has nothing to do with trimming;
Debug disables it too.**

**Release publishing uses `PublishTrimmed=false`** (~16M gzip): the trimming surface is wider than expected. Not just
the FormSchema XmlSerializer, but `JsonCodec` / MessagePack / `TypeDescriptor` / `Assembly.GetType` / DataGrid all
report IL2026 (with `TreatWarningsAsErrors` the build fails outright); `TrimmerRootAssembly` does not remove the
warnings. Full trim safety requires moving to source generation, which is a framework-level issue.

**Dialogs**: `OverlayDialogHost` (internal); LookupDialog / RowEditDialog ask `DialogHosting` (browser, iOS and
Android → overlay; only a desktop classic-window lifetime → native `Window`).

**Connections are always async**: a sync-over-async wait (`Task.Run(...).GetAwaiter().GetResult()`, the shape of the
former `SyncExecutor.Run`, since removed) throws **"Cannot wait on monitors on this runtime"** on the
single-threaded browser-wasm runtime: it blocks the only thread waiting for the task, and completing the task needs the
same thread to pump the event loop → deadlock. Desktop/WinForms tolerate it;
WASM does not.
**The client connection of any WASM head uses `await ClientInfo.InitializeAsync(endpoint)`**; there is no synchronous
`Initialize` any more, and wrapping an async call in `Task.Run(...).GetAwaiter().GetResult()` must not come back. The underlying HTTP is already `HttpClient` (WASM goes
through `BrowserHttpHandler`/fetch), so async is safe all the way. Likewise, load definitions through the async
`ClientDefineAccess` (`GetFormSchemaAsync` and the other `Get…Async` members); the synchronous remote define access that
wrapped such a wait no longer exists, so do not reintroduce one.

**Environment pitfalls**: building WASM needs `sudo dotnet workload install wasm-tools`. Run it locally with the Claude
preview (`.claude/launch.json` needs `autoPort:false`); **synthetic pointer events injected by the headless preview do
not reach Avalonia's input layer** (intercepted by `div.avalonia-native-host` over the canvas), so verifying UI clicks
needs a real browser.

## The back key (shared UI)

`MainView` handles `TopLevel.BackRequested` level by level: record → back to the list (`FormWorkspace.TryGoBack()`),
list → close the tab (`FormsView.TryHandleBack()`), no tabs → exit the app. iOS predictive back and the browser's back
key benefit at the same time.

## Graduation and periodic sync

> **Current state (2026-09-28)**: the standalone repository is
> [`polhem-dev/polhem-northwind`](https://github.com/polhem-dev/polhem-northwind), on the `Polhem.*` packages. It was
> created from `apps/Polhem.Northwind` at the `v1.0.0` tag, without history. The Bee.NET-era mirror
> `jeff377/bee-northwind-avalonia` was not its starting point: its last sync predated the Polhem rename, so it lacked
> the fixes made to this copy since. That mirror stays on the `Bee.*` packages and receives no further syncs.

**Graduation means "copy", not "move"** (user instruction, 2026-06-15): when the first standalone repository (the
Bee.NET-era `bee-northwind-avalonia`) was created, `apps/Polhem.Northwind` was copied over (ProjectReference → PackageReference),
**but `apps/Polhem.Northwind` inside polhem stays; it is not `git rm`ed yet**.

**Why**: `Polhem.UI.Avalonia` is still filling in controls and architecture, and keeping the in-repo demo on
ProjectReference is what allows immediate dogfooding while Avalonia changes. The standalone copy is the snapshot proof
from "an external point of view, pure NuGet"; the two coexist.
**The `git rm` is postponed until `Polhem.UI.Avalonia` is complete.**

**Sync process**: **publish the new framework version first** (the src changes that the new in-repo features depend on
must be on NuGet first) → copy the changed files over → reapply ProjectReference→PackageReference and bump → sync docs
and launch files → local build + smoke test → a pull request in `polhem-northwind`. Its `main` is protected like
polhem's (required checks, squash merge only), so nobody pushes to it directly.

**After verifying locally, still take a look at CI.** That repository has CI, and the environment differences between
local and the runner really do bite (Xcode version, whether a workload is installed): all green locally does not mean
CI will be green.

**Three pitfalls that were hit**:

1. **rsync needs `--exclude '*.csproj' --exclude 'README*.md' --exclude '.smoke.yaml'`** and syncs only source.
   The two repositories are laid out differently: each project in `apps/Polhem.Northwind/<Project>/` here lives in
   `src/<Project>/` there, while `Define/`, `Customize/`, `docs/`, the READMEs, the solution and `.smoke.yaml` stay
   at its root. So the destination of a project is `src/<Project>/`, and every path that crosses that boundary has one
   more `..` there than here (the Server csproj's `PolhemDefinitionFilesGlob`, for one). csproj files are handled
   individually and READMEs are ported by hand; otherwise the files specific to the standalone repository (its paths,
   the NuGet framework description) get overwritten. `.smoke.yaml` is kept apart for the same reason: its paths are
   relative to the root of each repository, `src/<Project>` there and `apps/Polhem.Northwind/<Project>` here, and
   the standalone copy and its solution, launch settings, docs and CI workflow all name the `src/` paths.
   The Server csproj also differs in shape, not only in reference type: in polhem the convention analyzer is a
   ProjectReference and the definitions are listed as `AdditionalFiles`, because `buildTransitive/` only reaches a
   package consumer; the standalone repository gets both from the `Polhem.Definition` package and only sets
   `PolhemDefinitionFilesGlob`.
2. **Copying over brings the in-repo src ProjectReferences back into the standalone repository**: they must be changed
   back to PackageReference + bump. This is the step most easily missed.
3. **`gh secret set` syntax pitfall**: `gh secret set <key value>` creates a secret whose **name** is the key value (and
   leaks the key in the UI); the correct form is `gh secret set NUGET_API_KEY --body "<value>"`, then confirm with the
   Updated timestamp in `gh secret list`. **Before publishing, confirm that the secret is the new, valid key.**
   (A Bee.NET-era record: polhem's publish workflow holds no NuGet API key; see
   [test-ci-release.md](test-ci-release.md) § Publishing: NuGet Trusted Publishing. The `gh secret set` syntax pitfall
   applies to any secret, `NUGET_USER` included.)

## The standalone repository's CI

`polhem-northwind` has `.github/workflows/build-ci.yml` (carried over from `bee-northwind-avalonia`, where it was
added on 2026-09-02) with two jobs: ubuntu builds the
Server, the UI project and three heads (Desktop / Browser / Android) and runs a runtime smoke test, and macOS builds
iOS on its own. **It verifies the build against the published NuGet packages**, which is exactly the path external
users will take.

**When the solution contains cross-platform heads, you cannot run `dotnet restore` at the solution level.**

```
error NETSDK1178: The project depends on the following workload packs that do not exist
in any of the workloads available in this installation: Microsoft.iOS.Sdk.net10.0_26.5
[.../Bee.Northwind.iOS.csproj]
```

`dotnet restore` without arguments restores **the whole solution**, and the solution contains the iOS head. Its
workload pack does not and cannot exist on Linux, so the restore aborts before it reaches any project that could be
built, and every project is skipped.
The fix is to let each project restore itself (the build steps do not pass `--no-restore`), which also keeps the list
of projects in a single place, the build step.

**This pitfall cannot be caught locally**: manual verification runs `dotnet build` project by project and never
reaches a solution-level restore.

**The smoke test must verify what "a build cannot see", otherwise it is not worth adding.** This smoke test starts the
server, waits for it to create the tables and finish seeding, and then asserts the key row counts and the total of
order 10252 **computed by summing its detail lines** (rather than reading a stored column, so a missing line shows up
as a wrong number). It targets the seeder's idempotency gate: the gate skips any table that already has rows, so a
partial insert is permanent and nobody reports it. That is how `ft_order_detail` survived once with only 11 of its 12
rows, and the build showed nothing at all.

**After adding the smoke test, verify that it really goes red.** A smoke test that cannot go red is worse than none,
because it gives the illusion that something is being verified. The way to do it: deliberately break the data and run
it again (delete one detail line of order 10252), and confirm that the step really exits 1.
