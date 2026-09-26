# Pitfall log: mobile trim / AOT (iOS / Android / Mac Catalyst)

On-demand context from the maintainer's point of view. The hard rules and criteria are always loaded from
`.claude/rules/apple-mobile-trim.md`; this file holds **the reasoning, the measured data and the operating recipes**.
You only need to read it when you actually build or verify a mobile head.

It applies to every mobile / Apple platform head. Currently that is `net10.0-ios` / `net10.0-android` of
`Polhem.UI.Avalonia` (see `../../../apps/Polhem.Northwind/Polhem.Northwind.iOS`, `.Android`).

---

## 1. The reasoning behind the trim decision tree

**Solved (2026-06-27, verified by measurement)**: embedding a descriptor in `Polhem.Definition` solves it
(`../../../src/Polhem.Definition/ILLink.Descriptors.xml`). The context below is kept to explain the why.

For `net10.0-ios` / `net10.0-maccatalyst` in Release, the Mono linker strips reflection dependencies that are not
referenced. The most common hit is the reflection fallback of `System.Xml.Serialization` being stripped →
`XmlCodec.Deserialize<FormSchema>` throws:

```
Error: XmlSerializeErrorDetails, 2, 2
```

`2, 2` is line 2 col 2 (the start of the XML root node). It looks like broken XML, but **it is actually type
metadata that was stripped**.

> **Correction from measurement**: the problem has two halves: half A (trimming strips metadata) and half B (AOT
> forbids `Reflection.Emit`, so XmlSerializer takes the reflection-only path). Measurement showed that even a full trim
> that strips 57% of `Polhem.Definition` does not break FormSchema deserialization, so `2,2` is **mostly half B, not
> half A**. The descriptor mainly guarantees complete coverage for half A (no polymorphic subtype is missed).

### "Fixes" known not to work

| Attempt | Result |
|------|------|
| `<PublishTrimmed>false</PublishTrimmed>` | The Apple SDK requires `true`; the build is rejected |
| `<MtouchLink>None</MtouchLink>` alone | Incomplete AOT compilation → `load_aot_module mismatch` SIGABRT |
| `<UseInterpreter>true</UseInterpreter>` alone | Some assemblies still go through AOT; CoreLib version mismatch SIGABRT |
| `<MtouchLink>SdkOnly</MtouchLink>` | XmlSerializer reflection is still stripped (SdkOnly does not protect the SDK itself) |

### Fixes evaluated but not adopted (from lowest to highest cost)

1. **Just run Debug** (demo / development stage): no trimming and no AOT restrictions, but large, slow, and not
   shippable.
2. **Precompile Sgen assemblies with `Microsoft.XmlSerializer.Generator`**: expands the reflection path into static
   code at build time.
3. **Add `[DynamicallyAccessedMembers]` annotations**: the most thorough, but with a wide impact.

### Details of the adopted fix

**`ILLink.Descriptors.xml` embedded in the library** (shipped with the NuGet package):

- Embedded in `Polhem.Definition` as `<EmbeddedResource LogicalName="ILLink.Descriptors.xml">`. The trimmer scans this
  logical name automatically, so **every downstream trim/AOT app (including external framework users) benefits
  automatically**.
- A wildcard `preserve="all"` roots `Polhem.Definition.*` + `Polhem.Base.Collections.*`. "FormSchema has too many
  subtypes" is exactly why a wildcard covers everything at once.
- Measured: a full trim on the Android emulator strips 57% without the descriptor and keeps ~98% with it; the
  round-trip passes in both.

### Verification status

- **Mobile Release trim/AOT `XmlSerializer` is verified to pass**: Android emulator full-trim round-trip PASS; iOS
  device-target AOT build 0 errors; iOS simulator and Mac Catalyst Release (both real Mono, both
  `IsDynamicCodeSupported=False`) round-trip PASS.
  Only the AOT runtime on a **physical** iOS device was closed out as low-risk (it needs Apple Developer signing + a
  physical device).

---

## 2. Fidelity of the reflection-only reproduction (measured 2026-08-10)

The "verify half B without a device" method works, but **when interpreting the results there are two things you must
know**, otherwise you will judge a real defect to be an artifact (that exact misjudgement was made on 2026-08-09; see
`serialization-and-expressions.md` and `.claude/rules/serialization.md`).

### This switch is set by the iOS SDK itself; it is not an artificial scenario

`Xamarin.Shared.Sdk.targets` in `Microsoft.iOS.Sdk`:

```xml
<DynamicCodeSupport Condition="'$(DynamicCodeSupport)' == ''
    And ('$(MtouchInterpreter)' == '' And '$(UseInterpreter)' != 'true')
    And ('$(_PlatformName)' == 'iOS' Or '$(_PlatformName)' == 'tvOS'
         Or '$(_PlatformName)' == 'MacCatalyst')">false</DynamicCodeSupport>
```

`Microsoft.NET.Sdk.targets` then maps `DynamicCodeSupport` to a `RuntimeHostConfigurationOption` for
`RuntimeFeature.IsDynamicCodeSupported`.

Inferences:

- **Every configuration of iOS / tvOS / MacCatalyst** (Debug and Release, device and simulator) turns dynamic code off
  by default, unless the interpreter is explicitly enabled. "Only Release needs worrying about" is wrong.
- **Android does not have this**: it keeps the JIT, and `IsDynamicCodeSupported` stays `true`.

### The exception *type* cannot be used for diagnosis

The same failing case throws different exceptions on three runtimes:

| Runtime | Exception |
|---------|------|
| CoreCLR + switch off (desktop reproduction) | `InvalidProgramException` (a JIT exists but is declared unavailable, reflection invoke goes through an interpreted thunk, and `MessagePackWriter` is a `ref struct`) |
| NativeAOT (really no dynamic code) | `InvalidOperationException` / `NotSupportedException` / `MissingMethodException` |
| Mono (Mac Catalyst / iOS simulator) | Purely managed decisions such as `FormatterNotRegisteredException` match the desktop; no sample was obtained for generic instantiation failures |

`InvalidProgramException` **is** a symptom specific to the desktop reproduction, but that only means the **symptom**
is distorted; **it does not mean the failure is fake**.

---

## 3. Operating recipes

### Desktop reproduction (no csproj change needed)

```bash
dotnet test <test project> -c Release --settings .runsettings -p:DynamicCodeSupport=false
```

It uses the same SDK path (`DynamicCodeSupport` → `RuntimeHostConfigurationOption`), exactly as the iOS SDK does.

### When you need a real Apple runtime: Mac Catalyst is cheapest, then the iOS simulator

Both have `DynamicCodeSupport` set to `false` by the SDK and run Mono, which is exactly the cell that neither the
desktop reproduction nor NativeAOT covers. Compile the logic under test into a minimal app:

```bash
# Mac Catalyst: run the executable inside the bundle directly; stdout goes to os_log
dotnet build -c Release -p:ValidateXcodeVersion=false
./bin/Release/net10.0-maccatalyst/maccatalyst-arm64/<App>.app/Contents/MacOS/<App>

# iOS simulator: build first, then -t:Run (never simctl install a hand-built .app)
dotnet build -c Release -f net10.0-ios -r iossimulator-arm64 -p:ValidateXcodeVersion=false
dotnet build -t:Run -c Release -f net10.0-ios -r iossimulator-arm64 \
  -p:ValidateXcodeVersion=false -p:_DeviceName=:v2:udid=<sim udid>

# iOS device target: AOT compilation completes even without signing; verifies "the whole closure compiles"
dotnet build -c Release -f net10.0-ios -r ios-arm64 \
  -p:ValidateXcodeVersion=false -p:EnableCodeSigning=false
```

The project needs an `ApplicationId` (otherwise `A bundle identifier is required`).

**Pitfall before compiling iOS**: the recipes above carry `-p:ValidateXcodeVersion=false` because their only purpose is
"verify whether the whole closure compiles", and which Xcode is used does not matter. **Do not take this detour for a
normal iOS head build**: the matching Xcode is usually already installed side by side under `/Applications/`, and
pointing `DEVELOPER_DIR` at it is the right fix (for the criteria, see "Before building iOS, check which Xcode you are
using" in `.claude/rules/apple-mobile-trim.md`):

```bash
DEVELOPER_DIR=/Applications/<matching-version>.app/Contents/Developer dotnet build <iOS project> -c Release
```

Once it is set, **`xcrun` / `simctl` in the same round must carry the same `DEVELOPER_DIR` too**, otherwise the
toolchain splits across two versions.

A device-target build that stops at signing can add `-p:EnableCodeSigning=false` to complete the AOT build (for
verification).

### Pitfall: Apple app bundles do not handle incremental rebuilds

After changing a framework assembly and building the same output tree again, the bundle may still hold **the old
managed assembly**, while `dotnet build` reports success without any warning. There are two symptoms, and neither
points at the real cause:

1. **SIGABRT on launch, `Main` never runs, not a single line of output** (the AOT container and the managed assembly
   do not match). The crash report gives it away: `mono_jit_init` → `mini_init` → `mono_aot_get_method`
   → `load_container_amodule` → `load_aot_module` → `abort`, all before any managed code.
2. **The app runs, but behaves like the old version**: for example the client still talks to the new server in the
   old wire format, and the error message is wrapped by the framework boundary into a vague "An error occurred during
   the data decoding process.", which looks like the backend is broken. Actually hit on 2026-08-10: the
   `Polhem.Api.Core.dll` in the bundle was six days old.

**Verify before investigating the code**: compare the timestamps of the assemblies in the bundle; it settles the
question in a second:

```bash
ls -la <sim device>/.../<App>.app/Polhem.Api.Core.dll   # iOS simulator
```

**The fix is a `rm -rf bin obj` rebuild.** Mind the order: after a clean you must `dotnet build` first and then
`-t:Run`; running `-t:Run` directly stops at
`The app must be built before the arguments to launch the app using mlaunch can be computed`.

### When you need "really no Emit": use NativeAOT, no device needed

Under the desktop reproduction the runtime is still a JIT. The cheapest environment that **really** has no
`Reflection.Emit` is a local NativeAOT console:

```bash
dotnet publish -c Release -r osx-arm64 -p:Aot=true -o ./aotout   # the csproj turns on PublishAot conditioned on $(Aot)
```

> `PublishAot` must be switched on inside the csproj through a custom property; **do not pass `-p:PublishAot=true`
> directly**. A command-line property flows into every `ProjectReference`, and `Polhem.Analyzers`
> (netstandard2.0) rejects it with `NETSDK1207`.

**But NativeAOT ≠ Mono full-AOT**: NativeAOT is stricter about runtime generic instantiation
(`MakeGenericMethod` / `MakeGenericType` simply have no native code), while Mono has shared instantiations for
reference types. So failures on NativeAOT fall into two groups: purely managed logic (such as a resolver refusing to
produce a formatter because of the switch) is bound to behave the same on Mono; failures of generic instantiation are
not necessarily.

---

## 4. Interpreting build warnings: the iOS head is never at 0 warnings

### Current numbers (measured 2026-09-10)

| Build | Reference form | MSBuild reports |
|------|---------|-------------|
| `apps/Polhem.Northwind.iOS` (this repository) | ProjectReference | 0 errors, **67 warnings** |
| The iOS head of `bee-northwind-avalonia` | PackageReference 4.30.0 | 0 errors, **21 warnings** |

The 21 warnings are made up of **13 × `IL2104`, 7 × `IL2026`, 1 × `IL2057`**.

### ⚠️ These two numbers cannot be compared with each other

**21 < 67 does not mean the standalone repository has fewer problems.** `TrimmerSingleWarn` defaults to `true`, which
collapses "all trim warnings produced by the same assembly" into **one `IL2104`**. Turn it off and build the same
standalone repository again:

```bash
dotnet build <iOS project> -c Release -p:TrimmerSingleWarn=false
```

Expanded, the measurement is **116 × `IL2026` + 62 × `IL2070` + 34 × `IL2075` + …**, **far more** than the 67 in this
repository. The assembly closures of the two sides are **identical, one by one** (`Polhem.Api.Client` /
`Api.Contracts` / `Api.Core` / `Base` / `Definition` / `Expressions` / `UI.Avalonia` / `UI.Core`); only the degree of
collapsing differs.

> On 2026-09-10, "the reference build has 67 > 21" was taken as evidence that "this change added no warnings". **That
> inference is invalid**, because the two numbers do not measure the same thing at all. The valid evidence is the
> closure comparison in the next section.

### Where the warnings actually come from

The 13 assemblies named by `IL2104`: `Polhem.Base`, `Polhem.Definition`, `Polhem.Api.Core`, `Polhem.Api.Client`,
`Polhem.UI.Core`, `Polhem.UI.Avalonia`, `Avalonia.Controls.DataGrid`, `Avalonia.DesignerSupport`,
`DynamicExpresso.Core`, `MessagePack`, `MessagePack.Annotations`, `System.Private.CoreLib`,
`System.Private.Xml`. It is only a summary saying "this assembly has trim warnings"; by itself it does not point at any
line of code.

Of the 7 `IL2026` (calls to members marked `RequiresUnreferencedCode`), **6 are the BCL's own code**:

```
System.Data.DataSet.IXmlSerializable.{GetSchema, ReadXml, WriteXml}
System.Data.DataTable.IXmlSerializable.{GetSchema, ReadXml, WriteXml}
```

`DataSet` / `DataTable` implement `IXmlSerializable`, and the implementation internally uses reflection-based XML
serialization; Microsoft itself marked those members `RequiresUnreferencedCode`. Because `DataSet` is the framework's
cross-layer DTO and goes onto the wire through the MessagePack formatter in `Polhem.Api.Core`, the trimmer sees these
interface implementations as reachable and reports them faithfully.
The 7th is `DefaultValueAttribute(Type, String)`, which uses a `TypeConverter` and is likewise not trim-safe.

The single `IL2057` is `Polhem.Northwind.UI.ViewLocator.Build(Object)`: Avalonia's standard ViewLocator pattern, which
turns the ViewModel type name string into a View type name and calls `Type.GetType`. The string is computed, so the
trimmer cannot guarantee that the View type is not stripped.

### This is not a defect, but you need to know why

These are **static analysis warnings** from the trimmer; they do not mean anything breaks at runtime. The failure of
`XmlSerializer` under Apple Release trimming was solved on 2026-06-27 with
`src/Polhem.Definition/ILLink.Descriptors.xml` and verified by measurement (see section 1): the descriptor already
roots the types that must be kept, and the trimmer does not withdraw the warnings just because a descriptor keeps the
types.

**Therefore: do not make "0 warnings" the acceptance threshold for the iOS head.** It never has been and never will be
at 0 warnings. (The acceptance criteria for the Northwind sync on 2026-09-10 got exactly this wrong once, and were
corrected when it was noticed.)

### A misleading coincidence

One entry in the warning list:

```
IL2026: System.Data.DataSet.IXmlSerializable.GetSchema():
        Using member 'System.Data.DataSet.WriteXmlSchema(DataSet, XmlWriter)'
```

`WriteXmlSchema` is exactly the method that `Polhem.Business.AuditLog.AuditDiffGram` uses since 4.30.0 to write the
audit payload, so it looks as if that change introduced it. **It did not.** It is called by the BCL's internal
`IXmlSerializable.GetSchema()` itself, and it appears on a head whose closure **does not even contain
`Polhem.Business`**.

**To decide (far more reliable than counting warnings)**: when you suspect a change introduced a trim warning, first
check whether that assembly is in the head's closure:

```bash
grep -o '"Polhem\.[A-Za-z.]*"' <iOS project>/obj/project.assets.json | sort -u
ls <iOS project>/bin/Release/net10.0-ios/*/Polhem.*.dll | xargs -n1 basename | sort -u
```

If it is not in the closure, the causal chain stops there; there is no need to dig further.

---

## 5. Inventory technique

How to inventory the whole definition layer for "collection property type shape" problems (scan everything at once;
do not look file by file): use reflection to list every `CollectionBase<>` / `KeyCollectionBase<>` property, and filter
those that "have `[XmlElement]`, no public setter, and are not marked `[XmlIgnore]`".
Scan result on 2026-08-10: only `LanguageEnum.Entries` in the whole repository, now fixed.

The hard requirements on type shape themselves are always loaded from `.claude/rules/apple-mobile-trim.md`.

---

## 6. History

Originally recorded in `.claude/rules/maui.md`. `src/Polhem.UI.Maui` was removed on 2026-07-28 (Avalonia already
covers iOS/Android), but this trim / AOT knowledge **does not expire with it**: it describes the behavior of the Mono
linker and AOT, not the behavior of MAUI.
