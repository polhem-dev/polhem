# Polhem.DefineEditor

**English** | [繁體中文](README.zh-TW.md)

A desktop tool for maintaining the Polhem definition files (the XML under DefinePath). Avalonia 12 + .NET 10 + CommunityToolkit.Mvvm, cross-platform (Windows / macOS / Linux).

## Positioning

- **A development-time tool**: not a published framework package and not a sample; it lives in `tools/`, has its own `Polhem.Tools.slnx` and is not published to NuGet. Its unit tests, `tests/Polhem.DefineEditor.UnitTests`, are part of the main `Polhem.slnx`, so CI builds the tool and runs them.
- **Purely offline**: reads and writes the XML under DefinePath directly; it connects to no remote server and no database.
- **In sync with the framework**: references `Polhem.Definition` and `Polhem.Base` through ProjectReference, and every read and write goes through `XmlCodec.SerializeToFile` / `DeserializeFromFile`, with zero serialization conversion.

## Supported definition types

After a solution is opened, the solution tree on the left lists every file under DefinePath, grouped by type; select one to load the matching editor on the right:

| Type | Editor | Main features |
|------|--------|---------------|
| **SystemSettings** (singleton) | [SystemSettingsDocumentView](Views/SystemSettingsDocumentView.axaml) | A tree of the Configuration sections and the BackendConfiguration options, plus free key/value ExtendedProperties. Some options have no node yet (for example `AuditLogOptions` and `SessionCleanupOptions`); edit those in the XML |
| **DbCategorySettings** (singleton) | [DbCategorySettingsDocumentView](Views/DbCategorySettingsDocumentView.axaml) | Two levels, Categories → Tables; validates duplicate Id / TableName |
| **ProgramSettings** (singleton) | [ProgramSettingsDocumentView](Views/ProgramSettingsDocumentView.axaml) | A flat list of ProgramItems (`<Items>`); each holds ProgId / DisplayName / BusinessObject / Repository; validates empty or duplicate ProgIds |
| **PermissionModels** (singleton) | [PermissionModelsDocumentView](Views/PermissionModelsDocumentView.axaml) | Two levels, Models → Rules; Action / Scope are drop-downs; integrates `PermissionModels.Validate()` |
| **MenuSettings** (singleton) | [MenuSettingsDocumentView](Views/MenuSettingsDocumentView.axaml) | MenuFolder → MenuEntry tree; separate property panels for folders and entries |
| **DatabaseSettings** (singleton) | [DatabaseSettingsDocumentView](Views/DatabaseSettingsDocumentView.axaml) | Two groups, Servers + Items; includes **parsing a pasted connection string** (SQL Server / PostgreSQL / MySQL / Oracle) and static validation (`Services/DatabaseSettingsValidator.cs`) |
| **FormSchema** (multiple) | [FormSchemaDocumentView](Views/FormSchemaDocumentView.axaml) | Tables → Fields → Relation / Lookup mappings; RelationProgId candidates come from the other FormSchemas in the solution. Right-click a schema node to **Generate FormLayout** |
| **TableSchema** (multiple) | [TableSchemaDocumentView](Views/TableSchemaDocumentView.axaml) | Two groups, Fields + Indexes; IndexField includes SortDirection; validates that the PrimaryKey is unique |
| **FormLayout** (multiple) | [FormLayoutDocumentView](Views/FormLayoutDocumentView.axaml) | Sections (→ LayoutField) + Details (LayoutGrid → LayoutColumn). The layout is produced and saved at design time — the runtime only reads it, and opening a form fails when the file is missing |
| **Language** (multiple) | [LanguageDocumentView](Views/LanguageDocumentView.axaml) | Items (Key/Value) + Enums (→ Entry code/text) |

Each editor has its own add and delete commands, a validation results panel and an unsaved-changes marker on its tab; the window has a status bar at the bottom. Save, Save All, Validate and Close Tab are commands of the **File** menu, with Open Folder and Open Recent; the **View** menu switches the theme and the UI language (English / 繁體中文), and a tab's context menu closes groups of tabs.

> The File and View menus are native menus, which Avalonia renders in the macOS menu bar only. On Windows and Linux the window shows its own **File** menu with Save, Save All, Validate and Close Tab, which run the same commands; the welcome page's "Open Folder" button opens a folder. Open Recent and the View menu are macOS-only.

## Running during development

Start it directly from source:

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug
```

After it starts, choose "Open Folder..." and pick a DefinePath folder. The editor saves in place, so to explore with the test fixtures in `tests/Define/`, copy the folder first: the tests share those files and must not see them change.

### Headless smoke

The `--smoke <FormSchema-fixture-path>` mode runs the editors' round-trips without opening a window. It copies the fixture to a temporary directory before editing it, prints one line per check, and ends with a line starting `[smoke] OK` and exit code 0 when everything passed. Which checks run is not listed here (that would drift); the authoritative source is `Smoke.Run` in [`Smoke.cs`](Smoke.cs).

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug \
    -- --smoke tests/Define/FormSchema/Employee.FormSchema.xml
```

A failing check prints `[smoke] FAIL(<code>)` and the process exits with that code.

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

The output is in `tools/DefineEditor/bin/Release/net10.0/<rid>/publish/`. Or run [publish.sh](publish.sh) to package every RID above at once.

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
./publish.sh --app-bundle           # every RID (osx-* are wrapped as .app, win/linux stay as they are)
./publish.sh --app-bundle osx-arm64 # osx-arm64 only
```

`--app-bundle` implies `--single-file`: the bundle carries the executable and the native libraries, so the managed assemblies have to be inside the executable.

Output location `bin/Release/net10.0/<osx-rid>/publish/Polhem.DefineEditor.app`, with this internal structure:

```
Polhem.DefineEditor.app/
└── Contents/
    ├── Info.plist              ← bundle description (the version is the project's evaluated Version, from Version.props at the repo root)
    ├── MacOS/
    │   ├── Polhem.DefineEditor ← main executable
    │   ├── lib*.dylib          ← Avalonia native libraries
    │   └── *.pdb, *.xml        ← symbols and XML docs, when the publish produced them
    └── Resources/
        └── AppIcon.icns        ← app icon (Assets/AppIcon.icns)
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
- Editing Customize-layer overrides (the solution tree currently does not mark overrides)
