# Polhem.DefineEditor

[繁體中文](README.zh-TW.md)

A desktop tool for maintaining Polhem definition files (the 9 kinds of XML under DefinePath). Avalonia 12 + .NET 10 + CommunityToolkit.Mvvm, cross-platform (Windows / macOS / Linux).

## Positioning

- **A development-time tool**: not a published framework package and not a sample; it lives in `tools/`, has its own `Polhem.Tools.slnx`, is not published to NuGet and is not run in CI.
- **Purely offline**: reads and writes the XML under DefinePath directly; it connects to no remote server and no database.
- **In sync with the framework**: references `Polhem.Definition` and `Polhem.Base` through ProjectReference, and every read and write goes through `XmlCodec.SerializeToFile` / `DeserializeFromFile`, with zero serialization conversion.

## Supported definition types

After a solution is opened, the solution tree on the left lists every file under DefinePath, grouped by type; select one to load the matching editor on the right:

| Type | Editor | Main features |
|------|--------|---------------|
| **SystemSettings** (singleton) | [SystemSettingsDocumentView](Views/SystemSettingsDocumentView.axaml) | 5 Configuration child nodes + 4 embedded options of BackendConfiguration + free key/value ExtendedProperties |
| **DbCategorySettings** (singleton) | [DbCategorySettingsDocumentView](Views/DbCategorySettingsDocumentView.axaml) | Two levels, Categories → Tables; validates duplicate Id / TableName |
| **ProgramSettings** (singleton) | [ProgramSettingsDocumentView](Views/ProgramSettingsDocumentView.axaml) | Two levels, Categories → Programs; a ProgramItem holds ProgId / DisplayName / BusinessObject |
| **PermissionModels** (singleton) | [PermissionModelsDocumentView](Views/PermissionModelsDocumentView.axaml) | Two levels, Models → Rules; Action / Scope are drop-downs; integrates `PermissionModels.Validate()` |
| **MenuSettings** (singleton) | [MenuSettingsDocumentView](Views/MenuSettingsDocumentView.axaml) | MenuFolder → MenuEntry tree; separate property panels for folders and entries |
| **DatabaseSettings** (singleton) | [DatabaseSettingsDocumentView](Views/DatabaseSettingsDocumentView.axaml) | Two groups, Servers + Items; includes **parsing a pasted connection string** (SQL Server / PostgreSQL / MySQL / Oracle) + 4 kinds of static validation |
| **FormSchema** (multiple) | [FormSchemaDocumentView](Views/FormSchemaDocumentView.axaml) | Tables → Fields → Relation / Lookup mappings; RelationProgId candidates come from the other FormSchemas in the solution. Right-click a schema node to **Generate FormLayout** |
| **TableSchema** (multiple) | [TableSchemaDocumentView](Views/TableSchemaDocumentView.axaml) | Two groups, Fields + Indexes; IndexField includes SortDirection; validates that the PrimaryKey is unique |
| **FormLayout** (multiple) | [FormLayoutDocumentView](Views/FormLayoutDocumentView.axaml) | Sections (→ LayoutField) + Details (LayoutGrid → LayoutColumn). The layout is produced and saved at design time — the runtime only reads it, and opening a form fails when the file is missing |
| **Language** (multiple) | [LanguageDocumentView](Views/LanguageDocumentView.axaml) | Items (Key/Value) + Enums (→ Entry code/text) |

Every editor has the shared toolbar (Save / Add / Validate / Delete), a status bar at the bottom, a validation results panel and an `IsDirty` indicator.

## Running during development

Start it directly from source:

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug
```

After it starts, choose "Open Folder..." at the top left and pick a DefinePath folder. `tests/Define/` contains test fixtures that can be opened directly.

### Headless smoke

The `--smoke <FormSchema-fixture-path>` mode runs every round-trip without opening a window. **Which ones actually run is not listed here** (that would drift) — the authoritative source is `Smoke.Run`, which calls each `Run*Smoke` in turn:

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug \
    -- --smoke tests/Define/FormSchema/Employee.FormSchema.xml
```

Expected output:

```
[smoke:formschema] OK
[smoke:permission]  OK (0 non-error issues)
[smoke:db]          OK
[smoke:program]     OK
[smoke:system]      OK
[smoke:db-settings] OK
[smoke:parser]      OK (SQL Server + PostgreSQL + dialect-mismatch warning)
[smoke:table-schema] OK
[smoke:form-layout] OK
[smoke:language]    OK
[smoke] OK — FormSchema + 8 multi-instance editors + ConnectionStringParser all green.
```

## Publish (framework-dependent)

By default it is packaged framework-dependent — without the .NET runtime, so the target machine needs .NET 10 installed first. Each RID is about 31 MB (including Avalonia's native dependencies for that platform).

```bash
# macOS Apple Silicon
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r osx-arm64 --self-contained false -p:PublishTrimmed=false

# macOS Intel
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r osx-x64 --self-contained false -p:PublishTrimmed=false

# Windows x64
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r win-x64 --self-contained false -p:PublishTrimmed=false

# Linux x64
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r linux-x64 --self-contained false -p:PublishTrimmed=false
```

The output is in `tools/DefineEditor/bin/Release/net10.0/<rid>/publish/`. Or run [publish.sh](publish.sh) to package all 4 platforms at once.

### Self-contained is also possible

If installing the .NET runtime on the target machine is inconvenient, the runtime can be packaged in (about 100–210 MB per platform):

```bash
./publish.sh --self-contained
# or a single RID
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r osx-arm64 --self-contained true -p:PublishTrimmed=false
```

### Single-file is also possible

Embeds every managed dll in the main executable, so the output is just the main exe + 3 Avalonia native `.dylib` / `.so` / `.dll` files (the .NET single-file specification does not allow native libraries to be bundled):

```bash
./publish.sh --single-file
# or a single RID
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r osx-arm64 --self-contained false -p:PublishTrimmed=false -p:PublishSingleFile=true
```

Measured on osx-arm64: a 12 MB main executable + about 18 MB for the three native dylibs (HarfBuzz, Skia, Avalonia.Native). It can be combined with `--self-contained` (producing a single exe that includes the runtime, but the native libraries stay separate).

### macOS `.app` bundle (recommended for external distribution)

For macOS users, adding `--app-bundle` wraps the publish output of the osx-* RIDs into a `Polhem.DefineEditor.app` directory: it opens on double-click, can be dragged into `/Applications`, and the Dock shows the correct name:

```bash
./publish.sh --single-file --app-bundle           # 4 RIDs (osx-* are wrapped as .app, win/linux stay as they are)
./publish.sh --single-file --app-bundle osx-arm64 # osx-arm64 only
```

Output location `bin/Release/net10.0/<osx-rid>/publish/Polhem.DefineEditor.app`, with this internal structure:

```
Polhem.DefineEditor.app/
└── Contents/
    ├── Info.plist          ← bundle description (the version is taken from Version.props at the repo root)
    └── MacOS/
        ├── Polhem.DefineEditor   ← main executable
        └── lib*.dylib × 3     ← Avalonia native
```

### First run: clearing the Gatekeeper quarantine flag

An `.app` without Apple Developer ID signing and notarization is blocked by macOS Gatekeeper on its first run after being downloaded from the internet or sent to someone else over AirDrop (the prompt says it cannot be opened because it is from an unidentified developer). Pick one of two fixes:

1. **Finder right-click → Open** (GUI): right-click the `.app` → choose "Open" → click "Open" in the dialog → after that, double-clicking works
2. **Clear the quarantine flag in Terminal** (one command):

   ```bash
   xattr -d com.apple.quarantine /path/to/Polhem.DefineEditor.app
   ```

An `.app` you build yourself on the local machine has no quarantine flag and does not run into this; only "cross-machine transfer" (download, AirDrop, copying over USB and extracting) adds the flag.

### Why the RID cannot be omitted

Omitting `-r` to build a portable app is also framework-dependent, but Avalonia's native dependencies (Skia / Avalonia.Native and so on) then bring along `runtimes/<rid>/native/*` for every platform, and the result is actually larger than self-contained (measured at 564 MB). So even for framework-dependent builds, specify the RID.

### Options known to stay off

| Option | Why it stays off |
|--------|------------------|
| `PublishTrimmed=true` | The framework relies heavily on reflection expansion in XmlSerializer; trimming easily strips the metadata of nested define types, which makes deserialization fail at runtime |

## Out of the tool's scope

- Connecting to a remote Polhem server / JSON-RPC API (local files only)
- Live connection tests for DatabaseSettings (left to server-side health checks)
- Multi-user collaboration / locking
- Editing Customize-layer overrides (to be discussed after Phase 6 if needed; the solution tree currently does not mark overrides)
