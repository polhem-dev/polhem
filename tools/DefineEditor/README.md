# Polhem.DefineEditor

**English** | [繁體中文](README.zh-TW.md)

A desktop tool for maintaining the Polhem definition files (the XML under DefinePath). Avalonia 12 + .NET 10 + CommunityToolkit.Mvvm, cross-platform (Windows / macOS / Linux).

## Positioning

- **A development-time tool**: not a framework library and not a sample; it lives in `tools/` and has its own `Polhem.Tools.slnx`. It is published to NuGet as the dotnet tool `Polhem.DefineEditor` (see [Installing it in a project](#installing-it-in-a-project)). Its unit tests, `tests/Polhem.DefineEditor.UnitTests`, are part of the main `Polhem.slnx`, so CI builds the tool and runs them.
- **Offline**: reads and writes the XML under DefinePath directly; it connects to no remote server and carries no database driver. Database work, when asked for, is done by the database manager tool (see [Database work](#database-work)).
- **In sync with the framework**: references `Polhem.Definition`, `Polhem.Core` and `Polhem.UI.Avalonia` (for `ObjectTreeView` and `PropertyGridControl`) through ProjectReference, and every read and write goes through `XmlCodec.SerializeToFile` / `DeserializeFromFile`, with zero serialization conversion.

## Supported definition types

After a solution is opened, the activity bar on the far left switches the sidebar between three views of the same files
(clicking the icon of the view already shown collapses the sidebar):

- **Explorer**: every file under DefinePath, grouped by type.
- **Programs**: each ProgId registered in `ProgramSettings.xml`, with the files that exist for it: its FormSchema and
  FormLayout, the TableSchema of each table its FormSchema declares, and its Language file in each language.
- **Table Schemas**: each database category in `DbCategorySettings.xml`, with the registered tables that have a TableSchema file. A category is a schema, not a connection: in a multi-tenant setup one `company` category stands for every company database in `DatabaseSettings.xml`.

Select a file in any view to load the matching editor on the right:

| Type | Editor | Main features |
|------|--------|---------------|
| **SystemSettings** (singleton) | [SystemSettingsDocumentView](Views/SystemSettingsDocumentView.axaml) | A tree of the Configuration sections and the BackendConfiguration options, plus free key/value ExtendedProperties. Some options have no node yet (for example `AuditLogOptions` and `SessionCleanupOptions`); edit those in the XML |
| **DbCategorySettings** (singleton) | [DbCategorySettingsDocumentView](Views/DbCategorySettingsDocumentView.axaml) | Two levels, Categories → Tables; validates duplicate Id / TableName |
| **ProgramSettings** (singleton) | [ProgramSettingsDocumentView](Views/ProgramSettingsDocumentView.axaml) | A flat list of ProgramItems (`<Items>`); each holds ProgId / DisplayName / BusinessObject / Repository; validates empty or duplicate ProgIds |
| **PermissionModels** (singleton) | [PermissionModelsDocumentView](Views/PermissionModelsDocumentView.axaml) | Two levels, Models → Rules; Action offers single actions and Scope its strategies; integrates `PermissionModels.Validate()` |
| **MenuSettings** (singleton) | [MenuSettingsDocumentView](Views/MenuSettingsDocumentView.axaml) | MenuFolder → MenuEntry tree |
| **DatabaseSettings** (singleton) | [DatabaseSettingsDocumentView](Views/DatabaseSettingsDocumentView.axaml) | Two groups, Servers + Items; a server's or item's context menu **pastes and parses a connection string** (SQL Server / PostgreSQL / MySQL / Oracle) in a dialog; an item's ServerId suggests the servers; static validation (`Services/DatabaseSettingsValidator.cs`) |
| **FormSchema** (multiple) | [FormSchemaDocumentView](Views/FormSchemaDocumentView.axaml) | Tables → Fields → Relation mappings. Selecting a Relation group shows the field's Relation properties. On a stored field, the **…** button beside RelationProgId (or RelationFieldMappings) opens the relation dialog: pick the related form from the solution's ProgramSettings, filtered by ProgId or name as you type; tick the master fields to carry back, name each destination (`ref_<base>_<field>` by default) and mark the ones that are LookupOnly (written back on a lookup selection but not used by the query join), and choose whether the field calls back the server when it changes (ValueChangeCallback, ticked by default when a source is not in the related form's LookupFields, so the server fills it); destinations the table lacks are added after the foreign key, and **Sync TableSchema and FormLayout** then brings them into the derived files. A mapping can still be edited on its own in the tree, where its fields suggest the fields of both forms; and LangEnumName suggests the enums of the solution's Language files. A field's ListItems are edited in the property grid's collection dialog. Validation, and Save before it writes, parses every field and rule expression with the framework's evaluator and lists the ones that would fail at run time, such as `amount * 1.05` on a decimal field. Right-click a schema node to **Generate FormLayout**, **Export to Excel**, **Sync TableSchema and FormLayout** (adds the fields the form gained to its TableSchemas and FormLayout, each next to its neighbour on the form, after showing the files it changes) or **Upgrade This Form's Tables** (also in the **File** menu while the FormSchema is the active tab); **File → Import Form from Excel** turns a form workbook into its definition files after showing the files it changes (see [Modeling Forms in Excel](../../docs/en/guides/excel-form-modeling.md)) |
| **TableSchema** (multiple) | [TableSchemaDocumentView](Views/TableSchemaDocumentView.axaml) | Two groups, Fields + Indexes; IndexField includes SortDirection; validates that the PrimaryKey is unique; a CREATE TABLE panel under the property grid shows the statement for SQL Server, PostgreSQL, MySQL, Oracle or SQLite, produced by the framework's own dialect builders |
| **FormLayout** (multiple) | [FormLayoutDocumentView](Views/FormLayoutDocumentView.axaml) | Sections (→ LayoutField) + Details (LayoutGrid → LayoutColumn). The layout is produced and saved at design time — the runtime only reads it, and opening a form fails when the file is missing. The **Preview** view, beside Design, XML and Split above the editor, shows the tree beside a live preview that redraws the form as you edit; the property grid is not on the side there, so **Properties…** (on the toolbar or a node's context menu) edits the selected node in a dialog. The preview has two engines, chosen in its header: **Avalonia** draws the form with the framework's Avalonia controls, the way `FormView` renders it in a desktop or mobile app; **Web** draws it with the MudBlazor components (`Polhem.Web.Blazor.Mud`), the way an app built from the `polhem-blazor-server` template shows it, in a web view drawn by the system's web engine (WebView2 on Windows, WKWebView on macOS, WPE WebKit or WebKitGTK on Linux). The editor remembers the engine chosen last. Neither preview has data. Both follow the same options: View mode, phone width, and each field's name in empty fields. The globe button in the preview's header (**Open in browser**) opens the same layout in the default web browser; unsaved edits and the preview options follow along, and the address can be pasted into another browser to compare. The page is served by the editor itself on 127.0.0.1, with a random token in the address, and needs no backend or data. The web view's engine is the operating system's, so for the final check of how a browser shows a form, use **Open in browser**. When the engine is missing, the Web preview says so and offers the browser instead. In either preview, a click on a section, a field, a detail grid or one of its column headers selects that node in the tree, and the node selected in the tree is outlined in the preview; a browser opened with **Open in browser** follows the tree the same way. To give the preview more room, hide the side bar (View menu, ⌘B) |
| **Language** (multiple) | [LanguageDocumentView](Views/LanguageDocumentView.axaml) | Items (Key/Value) + Enums (→ Entry code/text) |

The right-hand pane is a property grid (`PropertyGridControl`) built from the annotations on each definition type:
the properties are grouped by `[Category]`, the bar under them shows the selected property's `[Description]`, a value
that differs from its default is in bold and its context menu resets it, a password is masked, and a collection opens
in a dialog. Categories and descriptions follow the UI language; property names stay as written, since they are the
attribute names of the define files.

A property that names something the solution already has offers it in a drop-down: a FormSchema's `CategoryId`
(the database categories), `PermissionModelId` (the models in `PermissionModels.xml`) and `CurrencyField`, a field's
`RelationProgId`, `LangEnumName`, `CurrencyField` and `UnitField`, a layout's `ProgId`, a layout field's or column's
`FieldName` and a layout grid's `TableName` (read from the layout's FormSchema), an index field's `FieldName`, and a
menu entry's `ProgId` (the programs in `ProgramSettings.xml`). The drop-down only suggests: a typed value is kept, and
validation reports one that names nothing. A FormSchema's `ListFields` and `LookupFields` open a field picker from the
**…** button, where the master table's fields are ticked and put in order.

Importing a workbook asks how to treat a TableSchema or a FormLayout the form already has (merge, keep or overwrite the
TableSchema; keep or regenerate the layout), lists the files it will change, and ends with a dialog that offers to
upgrade the database for the form's tables.

**Import Forms from Excel (Batch)** is a wizard for several workbooks: choose them and their order, choose those two
answers once for the whole batch and whether to upgrade the database, confirm, and the workbooks are imported one at a
time in that order, each followed by an upgrade of its own tables. A form that relates to another goes after it, since
each workbook is planned only once the ones before it are written. The batch stops at the first workbook that has an
error or whose upgrade fails; the ones imported before it stay, and **Back** lists the workbooks still to import.

Each editor has a toolbar above its tree with the commands of the selected node as icon buttons, each named in its
tooltip, the same commands its context menu lists: the Add buttons, with those of the enclosing nodes as well so a sibling can be added without selecting the parent
first, then Move Up, Move Down, Duplicate and Delete, then the node's other commands (generating a layout, exporting,
upgrading the tables) behind a **⋯** button. A command that cannot run on the selected node stays on the toolbar,
greyed out. Duplicate inserts a copy after the node; a keyed object gets a free key ending in `_copy`.

While the tree has the focus, keys run the same commands:

| Key (macOS / Windows and Linux) | Command |
|---------------------------------|---------|
| ⌫ / Delete | Delete the selected node |
| ⌘D / Ctrl+D | Duplicate it |
| ⌥↑ ⌥↓ / Alt+Up Alt+Down | Move it among its siblings |
| Enter or F2 | Go to the first value in the property grid; Esc there comes back to the tree |

**Undo** (⌘Z / Ctrl+Z) and **Redo** (⇧⌘Z / Ctrl+Y), also the arrows at the right of the toolbar, take back and
reapply the document's edits: property values, added, deleted, duplicated and moved nodes, up to the last 100. Inside a
text box the keys undo its own typing instead. Delete does not ask first, since Undo brings the node back. A document
whose edits are all undone back to the saved file shows no unsaved marker.

Three ways to find things:

- **Filter** at the top of the sidebar keeps the nodes whose name or key contains the text, with the groups above them;
  the three views share it, and Esc clears it.
- **Go to File** (⌘P / Ctrl+P, the **Search files…** field in the title bar on macOS, the magnifier at the top of
  the sidebar on Windows and Linux, and the **File** menu) lists the solution's files by name, key or path, best match
  first; the letters may be apart (`ordfs` finds `Order.FormSchema.xml`). The arrow keys move through the list and
  Enter opens the file.
- **Find in tree** (⌘F / Ctrl+F, the box at the right of an editor's toolbar) selects the first node whose label
  contains the text and expands what is above it; Enter and Shift+Enter go to the next and previous match, and Esc
  goes back to the tree.

**Design / XML / Split** at the top of each editor (with **Preview** for a FormLayout, described above) switches between the tree, the document's XML (highlighted and with
line numbers), and both, the tree above the XML. In Split the two follow each other: an edit in the tree shows in the
XML at once, typing in the XML reaches the tree after a pause, and selecting a node scrolls the XML to its element; while
the XML cannot be read, the tree takes no edits, so they cannot overwrite the text being fixed. A document with no
unsaved edit shows its file as it is, comments included; otherwise the XML is what a save from the tree would write.
The caret starts on the element of the node that was selected, and ⌘F / Ctrl+F finds text. Each kind of document opens in the view the last one of its kind was left in.

The XML can be edited. Half a second after typing stops it is read into the document, and the tree, the property grid
and Undo follow it. XML that cannot be read keeps the document as it was: the problems are underlined and listed below
the text (an element or attribute the definition does not have counts as one, since the file would otherwise lose it
silently), and the document cannot be saved until they are fixed or discarded by switching back to Design. A document
edited only as XML since the XML was shown is saved exactly as typed, so comments and layout stay; an edit in the tree
makes the next save write the file from the definition again.

A save from the tree keeps the file's comments: each stays before the element it stood before (a field is recognised
by its name, so the comment moves with it), or at the end of the element it ended; a comment whose element was deleted
moves to the end of the element that held it. The rest of the file is written as the serializer writes it, attributes
in declaration order.

Typing in the XML is helped the way a XAML editor helps: `<` lists the elements the definition allows at that point and
the end tag of the enclosing one, a space inside a start tag lists the attributes it does not have yet (with their type,
default and description in the tooltip), the quotes of an attribute list its values, ⌃Space (also ⌘I on macOS, where
⌃Space usually switches the input source) lists whatever fits the caret, and `>` closing a start tag puts its end tag
after the caret. The elements and attributes come from the same mapping the file is read with. The values are an
enum's or a boolean's own, or the ones the property grid offers for that attribute: the solution's ProgIds, the fields
of the table concerned, and so on. Those are looked up in the document as it was last read, so an element typed since
gets them once the text has been read again.

**File → Export XML Schemas...** writes the schema of each kind of definition file (`FormSchema.xsd`, `TableSchema.xsd`,
`SystemSettings.xsd` and the others) into a folder you choose, so other XML editors can check and complete the files
too. Next to them it writes `xml.fileAssociations.json`, the `xml.fileAssociations` setting of VS Code's XML extension
(Red Hat) that ties each file name, such as `**/*.FormSchema.xml`, to its schema; copy it into your VS Code settings.
The schemas are exported from the definition types, so export them again after upgrading Polhem.

Each editor also has a validation results panel and an unsaved-changes marker on its tab; the window has a status bar at the bottom. Save, Save All, Validate and Close Tab are commands of the **File** menu, with Open Folder, Open Recent, Add Missing Framework Defaults, Import Form from Excel, Import Forms from Excel (Batch), Test Database Connections, Upgrade Database, Preview Database Upgrade and the active FormSchema's Sync TableSchema and FormLayout, Upgrade This Form's Tables and Preview Upgrade of This Form's Tables; the **View** menu shows or hides the side bar (⌘B), switches the theme and the UI language (English / 繁體中文), and a tab's context menu closes groups of tabs.

> The File and View menus are native menus, which Avalonia renders in the macOS menu bar only. On Windows and Linux the window shows its own **File** menu with the same commands except Open Folder and Open Recent; the welcome page's "Open Folder" button opens a folder. Open Recent and the View menu are macOS-only; on Windows and Linux, Ctrl+B shows or hides the side bar.

## Running during development

Start it directly from source:

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug
```

After it starts, choose "Open Folder..." and pick a DefinePath folder. Opening a folder only reads it; to seed a new DefinePath with the framework's default definitions (the `st_*` table schemas, the Department, Employee and AuditRule forms, and the settings files), choose File → Add Missing Framework Defaults..., which lists the files it will write and leaves existing ones alone. The editor saves in place, so to explore with the test fixtures in `tests/Define/`, copy the folder first: the tests share those files and must not see them change.

To open a folder at startup, pass it on the command line; a relative path is resolved against the current directory:

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj -- path/to/Define
```

## Installing it in a project

A project pins the editor in its local tool manifest, so everyone who clones it gets the same version, and opens its
own definitions with it:

```bash
dotnet new tool-manifest            # once per project, when it has no .config/dotnet-tools.json yet
dotnet tool install Polhem.DefineEditor
dotnet polhem-define Define         # opens the project's Define folder
```

After a clone, `dotnet tool restore` installs it again. The package carries the native libraries for macOS, 64-bit
Windows and 64-bit Linux only. On macOS a tool runs as a plain executable rather than an `.app`; when the editor has to
be an application (for example for automation that addresses apps by bundle id), build the `.app` described below and
start it with `open -a Polhem.DefineEditor.app --args <DefinePath>`. An `.app` that is already running only comes to the
front; it does not open the folder.

## Database work

The editor carries no database driver. Testing the connections, upgrading the tables and previewing an upgrade are done
by the database manager [Polhem.DbManager](../DbManager/README.md) (`polhem-db`), which carries the drivers of every
supported database. The editor runs it through `dotnet tool exec` at exactly its own version, against the open
DefinePath, so nothing has to be installed and no project is needed. The first run asks before the tool is downloaded,
naming the package, the version and the package source; later runs reuse the copy in the NuGet global packages folder.
`dotnet tool exec` needs the .NET SDK. The tool runs in a folder of the user's own (`Polhem.DefineEditor/dbmanager` in
the local application data folder), with its package source (nuget.org) given on the command line and the global
packages folder pinned, so a `NuGet.Config` or `global.json` in or above the opened folder cannot choose where the tool
comes from or which SDK runs it.

- **Test Connections**, at the top of the DatabaseSettings editor and in the **File** menu, opens a connection to each
  database item.
- **File → Upgrade Database** builds or upgrades every registered table; **Preview Database Upgrade** lists the
  statements without running them. After an import or a sync, the completion dialog upgrades only that form's tables
  (`--table` for each), since a database can hold thousands of tables; a FormSchema's context menu does the same at any
  time.
- A window shows the command line and its output as it is written, and reads the exit code: 0 done, 1 an item failed,
  2 a command line that was not understood. Closing the window or **Cancel** stops the tool. Password values in the
  output are shown as `********`.
- The tool reads the files on disk, so unsaved edits are offered for saving first.

When the editor runs from source, no tool of its version is published yet. Pack one with
`dotnet pack tools/DbManager/Polhem.DbManager.csproj -o <folder>` and name the folder in the `POLHEM_DBMANAGER_SOURCE`
environment variable; the editor passes it as a second package source.

### Headless smoke

The `--smoke <FormSchema-fixture-path>` mode runs the editors' round-trips without opening a window. It copies the fixture to a temporary directory before editing it, prints one line per check, and ends with a line starting `[smoke] OK` and exit code 0 when everything passed. Which checks run is not listed here (that would drift); the authoritative source is `Smoke.Run` in [`Smoke.cs`](Smoke.cs).

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug \
    -- --smoke tests/Define/FormSchema/Employee.FormSchema.xml
```

A failing check prints `[smoke] FAIL(<code>)` and the process exits with that code.

## Publish (framework-dependent)

By default it is packaged framework-dependent — without the .NET runtime, so the target machine needs the .NET 10 runtime and the ASP.NET Core 10 runtime installed first (the browser preview runs in the editor's own process; without the ASP.NET Core runtime the editor does not start at all). A .NET 10 SDK includes both. The **Web** preview inside the editor also needs the system's web engine: the WebView2 Runtime on Windows (part of Windows 11, a separate install on some Windows 10 machines) and WebKitGTK 4.1 on Linux (`libwebkit2gtk-4.1-0` on Debian and Ubuntu); macOS has it built in. Without it only that option is unavailable. Each RID is about 31 MB (including Avalonia's native dependencies for that platform).

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

`--app-bundle` implies `--single-file`: the bundle carries the executable, the native libraries and the browser preview's web assets, so the managed assemblies have to be inside the executable.

Output location `bin/Release/net10.0/<osx-rid>/publish/Polhem.DefineEditor.app`, with this internal structure:

```
Polhem.DefineEditor.app/
└── Contents/
    ├── Info.plist              ← bundle description (the version is the project's evaluated Version, from Version.props at the repo root)
    ├── MacOS/
    │   ├── Polhem.DefineEditor ← main executable
    │   ├── lib*.dylib          ← Avalonia native libraries
    │   ├── wwwroot/, *.staticwebassets.endpoints.json ← the browser preview's scripts, stylesheets and their manifest
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

Omitting `-r` to build a portable app is also framework-dependent, but Avalonia's native dependencies (Skia / Avalonia.Native and so on) then bring along `runtimes/<rid>/native/*` for every platform, and the result is actually larger than self-contained (measured at 564 MB). So even for framework-dependent builds, specify the RID. The dotnet tool package is the exception: it has to be portable, so the project file keeps only the desktop runtimes' native libraries (`PolhemKeepDesktopNativeRuntimes`, in `tools/Directory.Build.targets`).

### Options known to stay off

| Option | Why it stays off |
|--------|------------------|
| `PublishTrimmed=true` | The framework relies heavily on reflection expansion in XmlSerializer; trimming easily strips the metadata of nested define types, which makes deserialization fail at runtime |

## Out of the tool's scope

- Connecting to a remote Polhem server / JSON-RPC API (local files only)
- Multi-user collaboration / locking
- Editing Customize-layer overrides (the solution tree currently does not mark overrides)
