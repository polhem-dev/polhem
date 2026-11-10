# Polhem.DefineEditor

[English](README.md) | **繁體中文**

Polhem 定義檔（DefinePath 下的 XML）的桌面維護工具。Avalonia 12 + .NET 10 + CommunityToolkit.Mvvm，跨平台（Windows / macOS / Linux）。

## 定位

- **開發期工具**：非框架函式庫、非 sample；放在 `tools/`，獨立 `Polhem.Tools.slnx`。以 dotnet tool `Polhem.DefineEditor` 發佈到 NuGet（見[安裝到專案](#安裝到專案)）。它的單元測試 `tests/Polhem.DefineEditor.UnitTests` 屬於主 `Polhem.slnx`，所以 CI 會建置此工具並執行這些測試。
- **offline**：直接讀寫 DefinePath 下的 XML，不連遠端 server，也不帶資料庫驅動。需要資料庫相關工作時，交給資料庫管理工具處理（見[資料庫相關工作](#資料庫相關工作)）。
- **與框架同步**：以 ProjectReference 連 `Polhem.Definition`、`Polhem.Core` 與 `Polhem.UI.Avalonia`（用於 `ObjectTreeView` 與 `PropertyGridControl`），所有讀寫走 `XmlCodec.SerializeToFile` / `DeserializeFromFile`，零序列化轉換。

## 支援的定義型別

開啟方案後，最左側的 Activity Bar 可切換側欄的三種檢視（再點一次目前檢視的圖示會收合側欄）：

- **檔案總管**：按型別分組列出 DefinePath 下所有檔案。
- **功能**：`ProgramSettings.xml` 登記的每個 ProgId，列出它實際存在的檔案：FormSchema 與 FormLayout、
  FormSchema 宣告的每個資料表的 TableSchema，以及各語系的 Language 檔。
- **資料表結構**：`DbCategorySettings.xml` 的每個資料庫分類，列出有 TableSchema 檔的已登記資料表。分類是結構而非連線：多租戶環境下，一個 `company` 分類代表 `DatabaseSettings.xml` 裡的所有公司資料庫。

在任一檢視點選檔案，右側載入對應編輯器：

| 型別 | 編輯器 | 主要功能 |
|------|--------|---------|
| **SystemSettings**（單例） | [SystemSettingsDocumentView](Views/SystemSettingsDocumentView.axaml) | Configuration 各區段與 BackendConfiguration 各 options 的樹狀結構，加上 ExtendedProperties 自由 KV。部分 options 尚無節點（例如 `AuditLogOptions` 與 `SessionCleanupOptions`），請直接改 XML |
| **DbCategorySettings**（單例） | [DbCategorySettingsDocumentView](Views/DbCategorySettingsDocumentView.axaml) | Categories → Tables 兩層；驗證重複 Id / TableName |
| **ProgramSettings**（單例） | [ProgramSettingsDocumentView](Views/ProgramSettingsDocumentView.axaml) | 扁平的 ProgramItem 清單（`<Items>`）；每項含 ProgId / DisplayName / BusinessObject / Repository；驗證空白或重複的 ProgId |
| **PermissionModels**（單例） | [PermissionModelsDocumentView](Views/PermissionModelsDocumentView.axaml) | Models → Rules 兩層；Action 下拉只列單一動作，Scope 下拉列出策略；含 `PermissionModels.Validate()` 整合 |
| **MenuSettings**（單例） | [MenuSettingsDocumentView](Views/MenuSettingsDocumentView.axaml) | MenuFolder → MenuEntry 樹狀結構 |
| **DatabaseSettings**（單例） | [DatabaseSettingsDocumentView](Views/DatabaseSettingsDocumentView.axaml) | Servers + Items 兩個 group；Server 或 Item 的右鍵選單可在對話框中**貼上並拆解連線字串**（SQL Server / PostgreSQL / MySQL / Oracle）；Item 的 ServerId 會列出可用的 Server；含靜態驗證（`Services/DatabaseSettingsValidator.cs`） |
| **FormSchema**（多份） | [FormSchemaDocumentView](Views/FormSchemaDocumentView.axaml) | Tables → Fields → Relation 對應。選取 Relation 群組時，右側只顯示該欄位的 Relation 屬性。實體欄位的 RelationProgId（或 RelationFieldMappings）旁有 **…** 按鈕，可開啟關聯設定對話框：從方案的 ProgramSettings 選擇關聯表單，輸入 ProgId 或名稱即可過濾；勾選要帶回的主檔欄位、指定各自的目的欄位（預設為 `ref_<base>_<欄位>`），標記哪些是 LookupOnly（只在查詢選取時回寫，查詢的 JOIN 不使用），並選擇欄位值異動時是否回呼後端（ValueChangeCallback；有來源不在被關聯表單的 LookupFields 時預設勾選，由後端帶回）；本表缺少的目的欄位會新增在外鍵之後，再執行**同步 TableSchema 與 FormLayout** 就會補進衍生檔。單筆對應仍可在樹狀節點中個別編輯，對應的欄位會列出兩張表單的欄位；LangEnumName 會列出方案內語系檔的列舉。欄位的 ListItems 在屬性方格的集合對話框中編輯。驗證（以及存檔前的檢查）會用框架的 evaluator 解析每個欄位與規則的運算式，列出執行時會失敗的那些，例如 decimal 欄位寫成 `amount * 1.05`。schema 節點右鍵可**產生 FormLayout**、**匯出成 Excel**、**同步 TableSchema 與 FormLayout**（先列出要變更的檔案，再把表單新增的欄位補進 TableSchema 與 FormLayout，放在表單上相鄰欄位的旁邊）或**升級此表單的資料表**（FormSchema 為作用中分頁時，**File** 選單也有這兩個指令）；**File → 從 Excel 匯入表單**會先列出要變更的檔案，再把表單活頁簿轉成定義檔（見 [用 Excel 建模表單](../../docs/zh-TW/guides/excel-form-modeling.md)） |
| **TableSchema**（多份） | [TableSchemaDocumentView](Views/TableSchemaDocumentView.axaml) | Fields + Indexes 兩個 group；IndexField 含 SortDirection；驗證 PrimaryKey 唯一性；屬性方格下方的 CREATE TABLE 面板顯示 SQL Server、PostgreSQL、MySQL、Oracle 或 SQLite 的建表語法，由框架本身的方言產生器產出 |
| **FormLayout**（多份） | [FormLayoutDocumentView](Views/FormLayoutDocumentView.axaml) | Sections（→ LayoutField）+ Details（LayoutGrid → LayoutColumn）。版面於設計階段產出並存檔——執行階段只讀它，缺檔開表單即失敗。編輯器上方除了設計、XML、分割，還有**預覽**檢視：樹狀結構旁顯示即時預覽，隨編輯重畫表單；這個檢視不顯示屬性方格，以**屬性設定…**（工具列或節點的右鍵選單）在對話框中編輯所選節點。預覽有三種引擎，在預覽標題列切換：**Avalonia** 以框架的 Avalonia 控制項繪製，與桌面或行動應用程式中 `FormView` 的呈現相同；**MudBlazor** 與 **Fluent UI** 分別以 `Polhem.Web.Blazor.Mud`、`Polhem.Web.Blazor.Fluent` 的元件繪製，與採用該套件的網頁應用程式呈現方式相同（`polhem-blazor-server` 範本採用 MudBlazor），在編輯器內以作業系統的網頁引擎顯示（Windows 為 WebView2、macOS 為 WKWebView、Linux 為 WPE WebKit 或 WebKitGTK）。編輯器會記住上次選的引擎。各種預覽都不帶資料，也共用同樣的選項：檢視模式、手機寬度，以及在空白欄位顯示欄位名稱。在 MudBlazor 或 Fluent UI 預覽時，標題列的地球按鈕（**在瀏覽器預覽**）會在預設網頁瀏覽器開啟同一個頁面，由同一個套件繪製；Avalonia 預覽沒有網頁，所以不顯示這個按鈕；尚未存檔的修改與預覽選項都會同步，網址也可以貼到其他瀏覽器比對。頁面由編輯器自己在 127.0.0.1 提供，網址帶隨機 token，不需要後端或資料。編輯器內的網頁引擎隨作業系統而定，要最終確認瀏覽器中的呈現，請用**在瀏覽器預覽**。缺少引擎時，MudBlazor 與 Fluent UI 預覽會說明原因並提供改用瀏覽器的按鈕。在任一種預覽中點區塊、欄位、明細格線或格線的欄位標題，樹狀結構會選到對應節點；在樹上選取的節點，也會在預覽中以外框標出。以**在瀏覽器預覽**開啟的瀏覽器頁面也同樣跟著樹。要讓預覽更寬，可以隱藏側欄（View 選單、⌘B） |
| **Language**（多份） | [LanguageDocumentView](Views/LanguageDocumentView.axaml) | Items（Key/Value）+ Enums（→ Entry code/text） |

右側是依各定義型別的標註建出的屬性方格（`PropertyGridControl`）：屬性依 `[Category]` 分組，下方說明列顯示所選屬性的
`[Description]`，值與預設值不同時以粗體顯示、可用右鍵選單重設，密碼會遮蔽，集合在對話框中編輯。分組與說明會跟著介面語言切換；
屬性名稱維持原文，因為它們就是定義檔的屬性名。

指向方案中既有項目的屬性會提供下拉選項：FormSchema 的 `CategoryId`（資料庫類別）、`PermissionModelId`（`PermissionModels.xml`
裡的模型）與 `CurrencyField`；欄位的 `RelationProgId`、`LangEnumName`、`CurrencyField`、`UnitField`；版面的 `ProgId`、
版面欄位與欄的 `FieldName`、版面表格的 `TableName`（取自該版面的 FormSchema）；索引欄位的 `FieldName`；選單項目的 `ProgId`
（`ProgramSettings.xml` 裡的程式）。下拉只是建議，手動輸入的值會保留，指向不存在項目的值由驗證回報。
FormSchema 的 `ListFields` 與 `LookupFields` 可從 **…** 按鈕開啟欄位挑選器，勾選主表欄位並調整順序。
規則的 `When`、`Condition` 與欄位的 `ValueExpression`、`DefaultValueExpression` 可從 **…** 按鈕開啟運算式編輯器：
運算式可分行撰寫、不需 XML 跳脫，旁邊列出所屬資料表的欄位、helper 函式與可用的型別（`Math`、`Convert`、`DateTime` 等，
含其靜態成員）可供插入，輸入時即時解析，並在解析中斷處畫底線。輸入名稱的第一個字母時會建議欄位、函式與型別，
輸入句點時建議前面那個值的成員（`note.`、`Today().`、`Math.`），在 `==` 後輸入引號時建議下拉欄位的值；
Ctrl+Space（macOS 為 Cmd+I）列出游標處可用的項目。
規則的訊息與條件在同一個視窗編輯。

從活頁簿匯入時，若表單已有 TableSchema 或 FormLayout，會先詢問如何處理（TableSchema 合併、保留或覆寫；版面保留或重新產生），
再列出會變更的檔案，最後的對話框可接著替這張表單的資料表升級資料庫。

**從 Excel 批次匯入表單**是一次匯入多份活頁簿的精靈：選擇活頁簿與順序，整批一次回答上述兩個問題並決定是否升級資料庫，確認後依序逐份匯入，
每份匯入後只升級該表單的資料表。每份活頁簿都在前面的寫入之後才規劃，所以關聯到其他表單的表單要排在它後面。任何一份有錯誤或升級失敗，
批次就停在那一份；在它之前匯入的會保留，按**上一步**會列出還沒匯入的活頁簿。

每個編輯器的樹上方有一排工具列，以圖示按鈕列出所選節點的指令（滑鼠停在按鈕上會顯示名稱），與右鍵選單相同：先是新增按鈕，連同上層節點的新增指令一起列出，
所以不必先選取上層就能加入同層的項目；接著是上移、下移、複製、刪除；節點的其他指令（產生版面、匯出、升級資料表）收在
**⋯** 按鈕裡。所選節點目前無法執行的指令仍留在工具列上，以灰色顯示。複製會把副本插在原節點之後；有鍵值的物件會取一個
`_copy` 結尾、尚未使用的鍵值。

樹有焦點時，可以用按鍵執行同樣的指令：

| 按鍵（macOS／Windows 與 Linux） | 指令 |
|-------------------------------|------|
| ⌫／Delete | 刪除所選節點 |
| ⌘D／Ctrl+D | 複製 |
| ⌥↑ ⌥↓／Alt+Up Alt+Down | 在同層之間移動 |
| Enter 或 F2 | 跳到屬性面板的第一個值；在那裡按 Esc 回到樹 |

**復原**（⌘Z／Ctrl+Z）與**重做**（⇧⌘Z／Ctrl+Y），也就是工具列右側的兩個箭頭，可以撤回與重新套用文件的編輯：屬性值、
新增、刪除、複製與移動的節點，最多保留最近 100 步。焦點在文字框裡時，這兩個按鍵只作用於該文字框的輸入。因為可以復原，
刪除不再先跳確認。編輯全部復原回存檔時的狀態後，分頁上的未存檔標記會消失。

三種尋找方式：

- **篩選**：側欄上方的篩選框只保留名稱或鍵值含有該文字的節點，以及它們上層的群組；三種檢視共用同一段文字，按 Esc 清除。
- **前往檔案**（⌘P／Ctrl+P；macOS 標題列的「搜尋檔案…」欄位，Windows 與 Linux 側欄上方的放大鏡按鈕，**File** 選單也有）：依名稱、鍵值或路徑列出方案裡的檔案，最符合的排最前面；字母可以不連續
  （`ordfs` 找得到 `Order.FormSchema.xml`）。用方向鍵移動，Enter 開啟。
- **在樹中尋找**（⌘F／Ctrl+F，編輯器工具列右側的框）：選取第一個標題含有該文字的節點並展開它的上層；Enter 與 Shift+Enter
  跳到下一個與上一個符合項目，Esc 回到樹。

每個編輯器上方的 **Design／XML／Split**（FormLayout 另有 **Preview**，見上文）可在樹、該文件的 XML（有語法上色與行號）以及兩者上下並排之間切換。並排時兩邊互相同步：
在樹上修改會立即反映在 XML，在 XML 輸入的內容停頓後會讀回樹，選取節點時 XML 會捲到對應元素；XML 無法讀取時樹會暫停編輯，
以免覆蓋正在修正的文字。沒有未儲存修改的文件顯示檔案原樣，含註解；
否則顯示從樹存檔時會寫出的內容。游標會停在原本選取節點的元素上，⌘F／Ctrl+F 可尋找文字。每種文件會以同類文件上次停留的檢視開啟。

XML 可以直接編輯。停止輸入半秒後會讀入文件，樹、屬性面板與復原都會跟著更新。無法讀取的 XML 不會改動文件：問題會加上底線並列在文字下方
（定義沒有的元素或屬性也算問題，否則檔案會悄悄遺失它），修正之前或切回「設計」捨棄之前都不能存檔。顯示 XML 之後只在 XML 中修改的文件，
會照輸入的原樣存檔，保留註解與排版；在樹上做過修改後，下次存檔會依定義重新寫出檔案。

從樹存檔也會保留檔案中的註解：註解會留在原本所在元素的前面（欄位依名稱辨識，所以註解會跟著欄位移動），或留在原本所在元素的結尾；
元素被刪除時，註解移到外層元素的結尾。其餘內容依序列化器的格式寫出，屬性依宣告順序排列。

在 XML 中輸入時有和 XAML 編輯器一樣的提示：`<` 列出此處定義允許的元素與外層元素的結束標籤；開始標籤內按空白列出尚未設定的屬性
（提示中顯示型別、預設值與說明）；屬性的引號內列出可用的值；⌃Space（macOS 上 ⌃Space 通常用來切換輸入法，也可按 ⌘I）依游標位置
列出適用的項目；`>` 關閉開始標籤時會在游標後補上結束標籤。元素與屬性來自讀取檔案時所用的同一份映射；值則是列舉或布林本身的值，
或屬性面板為該屬性提供的值，例如方案中的 ProgId、相關資料表的欄位等。後者依最後一次讀入的文件查詢，因此剛輸入的元素要等文字讀回後才有。

**File → Export XML Schemas...（匯出 XML 結構描述）** 會把每種定義檔的結構描述（`FormSchema.xsd`、`TableSchema.xsd`、
`SystemSettings.xsd` 等）寫到你選的資料夾，讓其他 XML 編輯器也能檢查與提示這些檔案。同一處還會寫出 `xml.fileAssociations.json`，
也就是 VS Code XML 擴充套件（Red Hat）的 `xml.fileAssociations` 設定，把 `**/*.FormSchema.xml` 這類檔名對應到結構描述；
複製到 VS Code 設定即可。結構描述由定義型別匯出，升級 Polhem 後請重新匯出。

每個編輯器也有驗證結果面板，分頁上有未儲存標記；視窗底部有狀態列。儲存、全部儲存、驗證、關閉分頁是 **File** 選單的指令，另有開啟資料夾、最近開啟、加入缺少的框架預設檔、從 Excel 匯入表單、從 Excel 批次匯入表單、測試資料庫連線、升級資料庫、預覽資料庫升級，以及作用中 FormSchema 的同步 TableSchema 與 FormLayout、升級此表單的資料表、預覽此表單的資料表升級；**View** 選單可顯示或隱藏側欄（⌘B），也切換佈景主題與介面語言（English / 繁體中文），Avalonia 預覽的日期提示也跟著切換，分頁的右鍵選單可成批關閉分頁。

> File 與 View 是原生選單，Avalonia 只在 macOS 的選單列上呈現。在 Windows 與 Linux 上，視窗內會顯示自己的 **File** 選單，指令相同，但沒有開啟資料夾與最近開啟；歡迎頁的「Open Folder」按鈕可開啟資料夾。最近開啟與 View 選單只在 macOS 上有；在 Windows 與 Linux 上，Ctrl+B 可顯示或隱藏側欄。

## 開發期跑法

直接從原始碼啟動：

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug
```

啟動後以「Open Folder...」選一個 DefinePath 資料夾即可。開啟資料夾只會讀取；要替新的 DefinePath 補上框架的預設定義（`st_*` 資料表結構、Department、Employee 與 AuditRule 表單，以及各設定檔），請用 File → 加入缺少的框架預設檔...，它會先列出要寫入的檔案，既有檔案不會變動。編輯器會就地存檔，所以要拿 `tests/Define/` 的測試 fixture 來試，請先複製該資料夾：測試共用那些檔案，不能讓它們被改動。

啟動時要直接開啟某個資料夾，把路徑放在命令列；相對路徑以目前目錄解析：

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj -- path/to/Define
```

## 安裝到專案

專案把編輯器鎖在自己的本機工具清單裡，clone 的人都拿到同一版，再用它開啟專案的定義：

```bash
dotnet new tool-manifest            # 每個專案一次，還沒有 .config/dotnet-tools.json 時
dotnet tool install Polhem.DefineEditor
dotnet polhem-define Define         # 開啟專案的 Define 資料夾
```

clone 之後以 `dotnet tool restore` 重新安裝。套件只帶 macOS、64 位元 Windows 與 64 位元 Linux 的原生程式庫。
在 macOS 上，tool 以一般執行檔執行而不是 `.app`；需要它是應用程式時（例如以 bundle id 指定 app 的自動化），
請建置下文的 `.app`，以 `open -a Polhem.DefineEditor.app --args <DefinePath>` 啟動。已在執行的 `.app` 只會切到前景，
不會開啟該資料夾。

## 資料庫相關工作

編輯器不帶資料庫驅動。測試連線、升級資料表與預覽升級，由資料庫管理工具 [Polhem.DbManager](../DbManager/README.zh-TW.md)
（`polhem-db`）執行，它帶有所有支援資料庫的驅動。編輯器以 `dotnet tool exec` 執行與自己版號相同的工具，對象是目前開啟的
DefinePath，所以不必事先安裝，也不需要專案。第一次執行前會詢問是否下載，並列出套件、版號與套件來源；之後重複使用 NuGet
全域套件資料夾中的那一份。`dotnet tool exec` 需要 .NET SDK。工具在使用者自己的資料夾（本機應用程式資料夾下的
`Polhem.DefineEditor/dbmanager`）執行，套件來源（nuget.org）由命令列指定，全域套件資料夾也固定，所以開啟的資料夾或其上層的
`NuGet.Config`、`global.json` 都無法決定工具從哪裡來、由哪個 SDK 執行。

- **測試連線**：在 DatabaseSettings 編輯器上方與 **File** 選單，逐一連線每個資料庫項目。
- **File → 升級資料庫**建立或升級所有已登記的資料表；**預覽資料庫升級**只列出陳述式、不執行。匯入或同步後的完成對話框只升級
  該表單的資料表（每張表一個 `--table`），因為資料庫可能有上千個資料表；FormSchema 的右鍵選單隨時都能做同樣的事。
- 視窗顯示指令列與即時輸出，並依結束碼判讀：0 完成、1 有項目失敗、2 指令列無法解讀。關閉視窗或按**取消**會停止工具。
  輸出中的密碼值顯示為 `********`。
- 工具讀的是磁碟上的檔案，所以有未儲存的編輯時會先詢問是否儲存。

從原始碼執行編輯器時，還沒有與它同版號的已發佈工具。以 `dotnet pack tools/DbManager/Polhem.DbManager.csproj -o <資料夾>`
打包，並把該資料夾設在環境變數 `POLHEM_DBMANAGER_SOURCE`；編輯器會把它當成第二個套件來源傳入。

### Headless smoke

`--smoke <FormSchema-fixture-path>` 模式在不開視窗的情況下跑各編輯器的 round-trip。它會先把 fixture 複製到暫存目錄再編輯，每項檢查印一行，全部通過時最後一行以 `[smoke] OK` 開頭、結束碼為 0。實際跑哪幾項不列在這裡（那會漂），權威來源是 [`Smoke.cs`](Smoke.cs) 的 `Smoke.Run`。

```bash
dotnet run --project tools/DefineEditor/Polhem.DefineEditor.csproj --configuration Debug \
    -- --smoke tests/Define/FormSchema/Employee.FormSchema.xml
```

某項檢查失敗時印出 `[smoke] FAIL(<代碼>)`，程序以該代碼結束。

## Publish（framework-dependent）

預設打包 framework-dependent — 不含 .NET runtime，目標機要先裝 .NET 10 runtime 與 ASP.NET Core 10 runtime（瀏覽器預覽在編輯器自己的行程內執行；沒有 ASP.NET Core runtime 時編輯器整個無法啟動）。安裝 .NET 10 SDK 即包含兩者。編輯器內的 **MudBlazor** 與 **Fluent UI** 預覽另外需要作業系統的網頁引擎：Windows 需要 WebView2 Runtime（Windows 11 內建，部分 Windows 10 需另行安裝），Linux 需要 WebKitGTK 4.1（Debian、Ubuntu 的 `libwebkit2gtk-4.1-0`），macOS 已內建。缺少時只有這兩種預覽無法使用。每個 RID 約 31 MB（含 Avalonia 該平台原生依賴）。

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

輸出在 `tools/DefineEditor/bin/Release/net10.0/<rid>/publish/`。或執行 [publish.sh](publish.sh) 一次打包上面每個 RID。

### 也可以 self-contained

若目標機不便裝 .NET runtime，可內含 runtime 一起打包（約 100–210 MB／平台）：

```bash
./publish.sh --self-contained
# 或單一 RID
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r osx-arm64 --self-contained true -p:PublishTrimmed=false
```

### 也可以 single-file

把所有 managed dll 嵌進主執行檔，輸出剩主 exe + 3 個 Avalonia 原生 `.dylib` / `.so` / `.dll`（native 受 .NET single-file 規格限制不可 bundle）：

```bash
./publish.sh --single-file
# 或單一 RID
dotnet publish tools/DefineEditor/Polhem.DefineEditor.csproj -c Release \
    -r osx-arm64 --self-contained false -p:PublishTrimmed=false -p:PublishSingleFile=true
```

osx-arm64 實測：12 MB 主執行檔 + 約 18 MB 三個 native dylib（HarfBuzz、Skia、Avalonia.Native）。可與 `--self-contained` 組合（產一個含 runtime 的單一 exe，但 native 仍分離）。

### macOS `.app` bundle（推薦對外發佈）

對 macOS 使用者，加 `--app-bundle` 會把 osx-* RID 的 publish 內容包成 `Polhem.DefineEditor.app` 目錄，雙擊就開、可拖進 `/Applications`、Dock 顯示正確名稱：

```bash
./publish.sh --app-bundle           # 每個 RID（osx-* 包 .app，win/linux 維持原樣）
./publish.sh --app-bundle osx-arm64 # 只 osx-arm64
```

`--app-bundle` 隱含 `--single-file`：bundle 只帶主執行檔、原生函式庫與瀏覽器預覽的網頁資產，所以 managed 組件必須嵌在主執行檔內。

產出位置 `bin/Release/net10.0/<osx-rid>/publish/Polhem.DefineEditor.app`，內部結構：

```
Polhem.DefineEditor.app/
└── Contents/
    ├── Info.plist              ← bundle 描述（版號為專案評估後的 Version，來自 repo 根的 Version.props）
    ├── MacOS/
    │   ├── Polhem.DefineEditor ← 主執行檔
    │   ├── lib*.dylib          ← Avalonia 原生函式庫
    │   ├── wwwroot/、*.staticwebassets.endpoints.json ← 瀏覽器預覽的腳本、樣式表與其清單
    │   └── *.pdb、*.xml        ← 符號檔與 XML 文件（publish 有產出時）
    └── Resources/
        └── AppIcon.icns        ← App 圖示（Assets/AppIcon.icns）
```

### 第一次跑：解 Gatekeeper 隔離旗標

未做 Apple Developer ID 簽章與公證的 `.app`，從網路下載或 AirDrop 傳給其他人後，macOS Gatekeeper 會擋第一次執行（提示「無法打開，因為它來自身分不明的開發者」）。解法二擇一：

1. **Finder 右鍵 → 打開**（GUI 操作）：右鍵點 `.app` → 選「打開」→ 對話框點「打開」→ 之後雙擊就行
2. **Terminal 解隔離旗標**（一行指令）：

   ```bash
   xattr -d com.apple.quarantine /path/to/Polhem.DefineEditor.app
   ```

從本機自己 build 的 `.app` 沒有 quarantine 旗標，不會遇到這問題；只有「跨機器傳輸」（下載、AirDrop、USB 拷貝後解壓）才會被加旗標。

### RID 為何不可省略

省略 `-r` 改打 portable 雖然也是 framework-dependent，但 Avalonia 的原生依賴（Skia / Avalonia.Native 等）會把所有平台的 `runtimes/<rid>/native/*` 全帶上，結果反而比 self-contained 還大（實測 564 MB）。所以即使是 framework-dependent，仍指定 RID。 dotnet tool 套件是例外：它必須是 portable，因此專案檔只保留桌面平台的原生程式庫（`tools/Directory.Build.targets` 的 `PolhemKeepDesktopNativeRuntimes`）。

### 已知不打開的選項

| 選項 | 不開的原因 |
|------|-----------|
| `PublishTrimmed=true` | 框架重度依賴 XmlSerializer 的反射展開；trim 容易把 nested define type 的 metadata 砍掉導致 runtime 解序列化失敗 |

## 不在工具範圍

- 連遠端 Polhem server / JSON-RPC API（純本機檔）
- 多人協作 / 鎖定機制
- Customize 層覆蓋編輯（目前在方案樹上不標示覆蓋）
