# Mobile trim / AOT rules (iOS / Android / Mac Catalyst)

This file records the mobile build pitfalls that are **independent of the UI framework**: trimming in Release mode
strips reflection dependencies, and AOT disables `Reflection.Emit`. Both break reflection-based mechanisms such as
`XmlSerializer`.

It applies to every mobile / Apple platform head. Currently that is `net10.0-ios` / `net10.0-android` of
`Polhem.UI.Avalonia` (see `apps/Polhem.Northwind/Polhem.Northwind.iOS` and `.Android`).

> The reasoning, measured data and complete build / verification command recipes are in
> `docs/repo-ops/gotchas/mobile-trim-aot.md` (read on demand, not always loaded).
> This file keeps only the criteria and hard requirements.

## Before building iOS, check which Xcode you are using

The .NET for iOS SDK **requires an exact matching Xcode version**. On a mismatch the build fails right at the start,
and the error message itself states which version it wants and which version is current (this file does not copy
the version number; it would drift).

**This is not "the environment is broken", and you usually do not need to touch `xcode-select`.** The matching
version is usually already **installed side by side** under `/Applications/`. When you hit it, look first, then
point `DEVELOPER_DIR` at it for the build. Do not change the global setting:

```bash
ls -d /Applications/Xcode*.app
DEVELOPER_DIR=/Applications/<matching-version>.app/Contents/Developer dotnet build <iOS project> -c Release
```

> **Before concluding "the local environment does not support this and it is unrelated to this change", always run
> the two lines above first.** It was misjudged exactly this way on 2026-08-18: `xcode-select` pointed at the newer
> main Xcode, the matching version was installed side by side, and only because `DEVELOPER_DIR` was not set the iOS
> head was reported as unbuildable. With the right one selected: 0 errors.

`-p:ValidateXcodeVersion=false` **is not the answer here.** It skips the check and forces the build with the wrong
Xcode. It is reserved for the few reproduction recipes in gotchas that "check whether the closure compiles at all".
Any build you deliver always uses the right version.

## Sandbox and IO

The `.app` bundle on iOS / Mac Catalyst is read-only. Any approach that writes settings back to the assembly's
directory (`FileUtilities.GetAssemblyPath()`, `AppContext.BaseDirectory`) is guaranteed to fail on mobile.

When you need to persist user data, use a writable location the platform provides: the per-user application data
directory, a rebuildable cache directory, or key-value preference storage. The framework-side seam is
`IEndpointStorage`: a mobile head replaces it with the platform implementation at startup. Do not keep the desktop's
file-based default.

## XmlSerializer under trim: solved, stop spending time on it

The `XmlSerializer` failure under Apple Release trimming **was solved and verified by measurement on 2026-06-27**.
The fix is `src/Polhem.Definition/ILLink.Descriptors.xml` (**its file header documents the mechanism and the preserve
scope; this file does not copy it**). Every downstream trim/AOT app, including external framework users, benefits
automatically. The descriptor only acts when `Polhem.Definition` is itself trimmed, which the default partial trim
does not do (next section).

- **Do not try `<PublishTrimmed>false</PublishTrimmed>` / `<MtouchLink>None</MtouchLink>` /
  `<MtouchLink>SdkOnly</MtouchLink>` / turning on `UseInterpreter` alone again.** All four were tried and do not work
  (the Apple SDK forces trimming, incomplete AOT compilation causes SIGABRT, SdkOnly does not protect the SDK itself).
  Details are in gotchas.
- **Adding a definition type requires nothing for trimming.** The descriptor covers everything at once with a
  wildcard root.

> ⚠️ This section covers only **the `XmlSerializer` (definition file) half**. Two other paths have their own
> requirements, both in `rules/serialization.md`: the MessagePack wire (every type needs an explicitly registered
> formatter), and the expression engine, whose BCL members are kept by a separate descriptor,
> `src/Polhem.Expressions/ILLink.Descriptors.xml`.

## Supported trim modes: untrimmed and partial only

The packages are not marked `IsTrimmable` / `IsAotCompatible`, so the SDK default partial trim (`TrimMode=partial`,
what the iOS, Mac Catalyst and Android SDKs choose) copies the Polhem assemblies untouched. That is the supported
configuration. **`TrimMode=full` and NativeAOT (`PublishAot=true`) are unsupported**: the System.Text.Json JSON-RPC
envelope is serialized by reflection, so full trim silently drops its `jsonrpc` / `id` members and NativeAOT breaks
every call. Full trim also covers `AndroidLinkMode=Full`, `MtouchLink=Full`, and `PublishTrimmed=true` without a
`TrimMode` on desktop or browser-wasm.

**`POLHEM9004`** warns about these configurations at build time. It lives in
`src/Polhem.Definition/buildTransitive/Polhem.Definition.targets`; the conditions and the opt-out property are in that
file, not copied here. Only a package reference imports `buildTransitive/`, so the heads in this repository, which use
`ProjectReference`, never see it.

**Do not add `IsTrimmable` / `IsAotCompatible` to fix a trim problem** before the System.Text.Json half is
source-generated: it would make the default partial trim start trimming the Polhem assemblies.

## AOT: Android cannot verify the dynamic-code half

The .NET for iOS SDK sets `DynamicCodeSupport` to `false` by default for **every configuration of iOS / tvOS /
MacCatalyst** (Debug and Release, device and simulator), unless the interpreter is explicitly enabled.
"Only Release needs worrying about" is wrong.

**Android does not have this.** It keeps the JIT, and `IsDynamicCodeSupported` stays `true`.
**Any concern about `Reflection.Emit` / dynamic code cannot be verified on the Android emulator.** It can only verify
trimming. Stop using Android as evidence for this half.

A desktop reproduction does not need a csproj change; one command-line property is enough (it uses the same SDK path):

```bash
dotnet test <test project> -c Release --settings .runsettings -p:DynamicCodeSupport=false
```

### Interpreting results: the exception *type* is not diagnostic; the pass / fail boundary is

The same failure throws **different** exceptions on three runtimes: CoreCLR with the switch off, NativeAOT, and Mono
(the mapping table is in gotchas). The `InvalidProgramException` specific to the desktop reproduction only means the
**symptom** is distorted. **It does not mean the failure is fake.**

**Test: does it pass with the switch removed? If it passes without the switch and fails with it, you really hit the
no-dynamic-code path.**

When you need a real Apple runtime, Mac Catalyst is cheapest, then the iOS simulator. When you need "really no Emit",
use a local NativeAOT console. Command recipes are in gotchas.

## Combining AOT and the interpreter

Do not combine these settings arbitrarily:

- `MtouchLink` + AOT, the default combination, is stable (SDK default)
- `MtouchLink=None` + `UseInterpreter=true` looks like it "solves everything", but the CoreLib AOT version still
  mismatches → worse
- Interpreter for everything (`<MtouchInterpreter>-all</MtouchInterpreter>`) works in theory, but startup is slow
  and performance is poor

## Mobile compatibility requirements for serialized types

The reflection-only `XmlSerializer` (the iOS AOT path) is stricter about type shape than desktop: a collection type
**may expose only one** public instance `Add`, **must have a parameterless constructor**, and a collection property
mapped to repeated `[XmlElement]` **must have a public setter**. Violations throw, respectively,
`AmbiguousMatchException` / `MissingMethodException` / `ArgumentException: Property set method not found`, and
**none of them shows up on desktop at all**.

Violators are always definition-layer types → for the full rules and the correct way to write the setter, see
`src/Polhem.Definition/CLAUDE.md`. `XmlSerializerShapeGateTests` (tests/Polhem.Definition.UnitTests) checks the three
rules over every type the definition roots reach, so a violation turns the build's tests red on desktop.

## Diagnostic noise (so you do not take the long way round again)

- The error `There is an error in XML document (2, 2)` looks like broken XML content, but **is usually an AOT path
  problem** (`2,2` is just the start of the root node). Do not investigate the XML content.
- The real on-device `XmlSerializer` trigger is **on the mobile head side**: the head is a remote JSON-RPC client,
  FormSchema travels to the client as an **XML string inside the JSON wire**, and it is deserialized only on the
  client by `XmlCodec.Deserialize<T>(result.Xml)`. It is **not** the server reading `Define/*.xml`.
- This was once misjudged as a plain trimming pitfall; `MtouchLink=None` and a linker.xml `TrimmerRootDescriptor`
  were both tried with no effect. The real cause was the overloaded `Add`.
- **Apple app bundles do not handle incremental rebuilds**: after changing a framework assembly and building again,
  the bundle may still hold the old managed assembly while `dotnet build` reports success with zero warnings.
  The symptom is SIGABRT on launch (`Main` never runs), or the app runs but behaves like the old version.
  **Compare the timestamps of the assemblies in the bundle before investigating the code.** The fix is a
  `rm -rf bin obj` rebuild. Details are in gotchas.

## Related

- `docs/repo-ops/gotchas/mobile-trim-aot.md`: reasoning, measured data, command recipes
- `rules/avalonia.md`: Avalonia-specific rules (version compatibility, control pitfalls)
- `rules/serialization.md`: the AOT conclusions for MessagePack / DynamicExpresso. **The two conclusions are
  opposite**: MessagePack's contractless resolver has **no** reflection fallback, so every wire type needs an
  explicitly registered formatter; DynamicExpresso automatically falls back to the interpreter and needs nothing for
  AOT. **Trimming is different**: DynamicExpresso needs `src/Polhem.Expressions/ILLink.Descriptors.xml`.
  ("MessagePack also has a fallback" is an old conclusion disproved by measurement on 2026-08-10; do not reason
  from it.)
- `src/Polhem.Definition/ILLink.Descriptors.xml`: the file that implements the adopted fix
- `src/Polhem.Expressions/ILLink.Descriptors.xml`: the trim roots for the members expressions reach by reflection
