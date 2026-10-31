# 版本變更記錄

[English](CHANGELOG.md)

Polhem 套件的重要變更，每個版本只列幾項重點。每個版本都連到 [`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下的
發佈說明，那裡完整描述每一項變更，並說明每項破壞性變更影響誰、如何遷移。格式依循
[Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。

> **1.x 的 minor 版可能含破壞性變更**，在這裡以 **破壞性：** 標示，遷移步驟寫在該版的發佈說明。patch 版不會。
> 版號自 2.0.0 起依循[語意化版本](https://semver.org/lang/zh-TW/)。引用套件時請使用不跨 minor 版的範圍，例如
> `[1.6,1.7)`，不要用 `1.*`。這項政策與理由見 [ADR-052](maintainers/adr/adr-052-breaking-changes-in-1-x-minors.md)。

## [Unreleased]

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.12.0.md](docs/zh-TW/changelogs/1.12.0.md)

- 在窄螢幕上，Avalonia 的 `ListView` 會在使用者捲動卡片時接續讀取下一頁，最多 5000 筆。
- 點選 Avalonia `ListView` 或 Blazor Server `FormPage` 的清單欄位標題時，會由伺服器排序全部資料，而不只是畫面上這一頁。
- Avalonia 與 Blazor Server 的開窗在候選資料超過顯示筆數時會提示，並請使用者輸入關鍵字。
- Avalonia 的 `ListView` 與 Blazor Server 的 `FormPage` 改為逐頁顯示表單資料，提供分頁按鈕與每頁筆數選項，超過伺服器單頁上限的資料不再被無提示地略過。
- `GetLookup` 改依第一個開窗欄位排序候選資料，不再依 `sys_no`。分頁的 `GetList` 會在缺少唯一欄位的排序後補上唯一欄位，跨頁不再重複或漏筆；
  頁碼大到無法計算時回傳空結果，不再失敗或回傳錯誤的資料。
- Blazor Server 的 `FormPage` 與 Avalonia 的 `FormView` 在唯讀記錄上，使用者雙擊關聯欄位或明細格的關聯儲存格且能讀取關聯表單時，以對話框開啟它指向的資料。
- Avalonia 的 `FormView` 對使用者可以處理的訊息（必填欄位未填、驗證訊息、無權開啟的資料）改用警告樣式，不再用錯誤樣式；真正的失敗仍用錯誤樣式。
- Avalonia 的 `ListView` 與 Blazor Server 的 `FormPage` 在刪除記錄前會先確認。將 `ConfirmDelete` 設為 `false` 可關閉。

## [1.11.0] - 2026-10-28

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.11.0.md](docs/zh-TW/changelogs/1.11.0.md)

- 新範本 `polhem-blazor-server` 建立一個在自身行程內執行後端、提供 Polhem 外殼的 Blazor Server 網站。
- `Polhem.Hosting` 新增 `AddPolhemBackend`，在 `IHostApplicationBuilder` 上一次呼叫就從 `Define` 資料夾啟動後端；
  `polhem-avalonia` 的伺服器改用它。
- 各範本的名稱、說明與選項翻譯成繁體中文，在 `dotnet new` 與 C# Dev Kit 使用該語言時顯示。
- `Polhem.Web.Blazor.Server` 新增現成的外殼（`PolhemShellLayout`，以 `AddPolhemShell` 設定）：登入與公司、頂端列、伺服器的選單、
  窄螢幕上可收合的選單，以及英文／繁體中文切換，對應 `Polhem.UI.Avalonia.Shell`。選單裡還沒有表單時，根頁面與選單會說明這一點。
  套件也附帶一份選用的預設樣式表 `polhem.css`。
- 框架預設的 `Department` 與 `Employee` 表單會在清單列出編號、名稱與關聯名稱，先前清單沒有任何欄位；在預設 Employee 表單選取部門
  也不再失敗。`Define` 資料夾已有這兩個 schema 的應用程式會保留舊檔：請手動更新，或刪除後讓它重新寫入。
- `LayoutField` 的 `RowSpan` 與 `ColumnSpan` 會在每個前端的表單生效；表單渲染的版面複本先前把兩者都重設為 1。
- 在 Avalonia 表單與 DefineEditor 的預覽中，跨多列欄位之後的欄位會排在它下方，不再與它重疊。
- DefineEditor 也能在網頁瀏覽器預覽表單版面，由 Blazor Server 元件繪製，尚未存檔的修改會同步；編輯器因此需要 ASP.NET Core runtime。
- 在還沒有 `DbCategorySettings.xml` 的 `Define` 資料夾匯入活頁簿時，會以框架的預設檔為起點，框架的資料表因此維持登記；
  先前若在第一次初始化資料庫之前匯入，建置會對預設表單回報 POLHEM2001。

## [1.10.0] - 2026-10-27

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.10.0.md](docs/zh-TW/changelogs/1.10.0.md)

- **破壞性：** `Polhem.Web.Blazor.Server` 以 `FormLayoutPresenter` 取代 `DynamicForm`。
- **破壞性：** `IExpressionEvaluator` 新增 `Validate`，依變數型別解析運算式而不執行；在框架以外自行實作的 evaluator
  必須實作它。
- **破壞性：** `Polhem.Web.Blazor.Server` 的 `FormPage` 改為一次顯示清單或單筆，以 View、New、Edit、Save、Cancel、Back
  切換，與 Avalonia 的 view 相同；動作失敗時改以提示顯示，不再以錯誤取代表單。
- **破壞性：** 移除 `FormField.LookupProgId` 與 `FormField.LookupFieldMappings`。查詢視窗開啟 `RelationProgId`，
  並依 `RelationFieldMappings` 回寫；仍帶有這兩者的定義檔讀取時會略過它們。
- `FieldMapping.LookupOnly` 標記「查詢選取時回寫、查詢 JOIN 不讀」的關聯對應，讓選取時可以一併帶回其他值，
  例如把員工的部門帶進請假單。
- 標記 `FormField.ValueChangeCallback` 的欄位在使用者變更時，會呼叫所屬表單新增的 `FormBusinessObject.FieldValueChange`，
  Avalonia 與 Blazor 兩端都會把伺服器回傳的值寫回資料列。
- `Polhem.Web.Blazor.Server` 新增欄位編輯器，名稱、契約與行為都和 `Polhem.UI.Avalonia` 的相同；`FormPage` 現在會依表單模式與各欄位的 `AllowEditModes` 決定可否編輯。
- `Polhem.Web.Blazor.Server` 新增查詢流程：`ButtonEdit`、`LookupDialog`、`LookupPanel`。
- `Polhem.Web.Blazor.Server` 透過新的 `FormLiveComputation` 即時重算計算欄位；其 `FormDataObject` 新增新資料列預設值、資料列事件與資料列編輯暫存。
- `Polhem.Web.Blazor.Server` 呈現明細資料表：`GridControl`（儲存格內編輯或編輯表單）、`RowEditPanel`、`RowEditDialog`，由 `FormLayoutPresenter` 與 `FormPage` 顯示；`FormPage` 也會依公司、幣別與單位的 rounding context 預覽數值。
- `Polhem.Web.Blazor.Server` 的 `FormPage` 在 `ProgId` 變更時重新載入、可開啟真實後端列出的資料列，並依新的 per-circuit `CircuitUserContext`
  上的能力隱藏指令、降級敏感欄位。
- Northwind 示範新增在同一個行程執行後端的 Blazor Server head、會回呼帶回商品單價的訂單明細，以及配合新 Order 權限模型的
  唯讀帳號。
- `Polhem.Web.Blazor.Server` 的 `FormPage` 在窄 viewport 上改用精簡版面（主檔單欄、明細以表單編輯），寬度由新的
  `CompactViewportObserver` 在瀏覽器量測。
- 即時計算不再無聲停止：運算式無法評估時，Avalonia 的 `FormView` 與 Blazor 的 `FormPage` 會指出欄位與原因，原因以 `LiveComputationDegradation` 公開。
- `Polhem.UI.Core` 新增兩端共用的欄位規則：`FieldEditRules`、`FieldValueFormatter`、`NumericFieldFormat`、`DateTimeFieldFormat`、`LookupFieldRules`、`LookupDisplay`、`GridCellFormatter`、`FieldCaptionRules`，以及即時計算引擎 `FormLiveComputationEngine`。
- DefineEditor 驗證或存檔表單時，會列出執行時會失敗的 FormSchema 運算式（例如 decimal 欄位寫成 `amount * 1.05`），
  透過新的 `FormExpressionCalculator.ValidateExpressions`。

- DefineEditor 可在對話框中設定存檔欄位的關聯：關聯表單取自方案的 `ProgramSettings`，其主檔欄位各自對應到一個目的欄位
  （資料表缺少時會新增），並可標記 `LookupOnly` 或 `ValueChangeCallback`；套用前會先列出結果會破壞什麼。
- DefineEditor 可從樹狀結構新增與刪除 FormSchema 的規則。
- 同步已有 `zh-TW` 文字的表單時，會為每個新欄位（含關聯欄位）補上可翻譯的標題 key。
- DefineEditor 的最近使用方案改以方案資料夾命名、顯示所在位置、資料夾消失時變暗，並可逐項移除。
- DefineEditor 的 **Databases** 檢視更名為 **Table Schemas**，macOS 的標題列新增側邊欄切換按鈕。
- `Polhem.UI.Avalonia` 的表單與清單檢視改以可關閉的訊息列顯示被拒絕的存檔或規則訊息，並把游標移到第一個未填的必填欄位；
  必填標題也改為依 FormSchema 與 layout 兩者判定。
- `GridControl` 的儲存格內文字編輯改為取代原值：進入編輯時選取整段文字，在已選取的儲存格上按鍵即以該字元開始編輯，
  F2 保留游標在尾端，Escape 放棄編輯。
- `Polhem.UI.Avalonia.Shell` 在手機上把內容保持在螢幕鍵盤上方，並把取得焦點的編輯器捲回可見範圍。
- `PropertyGridControl.ValueEditorProvider` 讓文字或建議輸入的屬性列帶一個按鈕，開啟由宿主提供的編輯器，
  與 `CollectionEditorProvider` 之於集合相同。
- `ListItem.Text` 在沒有設定文字時回傳項目的值，下拉因此顯示值而不是空白選項。
- `polhem-avalonia` 範本的 **Server + Browser** 啟動設定會一併帶起瀏覽器用戶端，不再只起伺服器。

## [1.9.0] - 2026-10-23

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.9.0.md](docs/zh-TW/changelogs/1.9.0.md)

- DefineEditor 可把 FormSchema 新增的欄位同步到它的 TableSchema 與 FormLayout，並只升級該表單的資料表；版面部分由新的 `FormLayoutGenerator.Merge` 處理，表單活頁簿匯入也能使用。
- DefineEditor 的 FormLayout 編輯器可選擇顯示屬性格、預覽或兩者，隱藏屬性格時以對話框編輯屬性；**View › Toggle Side Bar**（⌘B）可收合側欄。
- 在 iOS 與 Android 上，`Polhem.UI.Avalonia.Shell` 一次只顯示一個表單，選單選好後自動收起；新的 `ShellOptions.DefaultThemeVariant` 設定啟動時的主題，新建的範本專案與 Northwind 預設為深色。
- 殼的選單鈕與分頁關閉鈕在瀏覽器端可正常顯示；透過無障礙介面選取的選單項目與樹狀清單列，現在會開啟並跟著切換（`TreeViewExtensions.KeepSelectionSingle`）。

## [1.8.0] - 2026-10-22

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.8.0.md](docs/zh-TW/changelogs/1.8.0.md)

- 表單可以在 Excel 活頁簿中描述，再以 `dotnet polhem forms` 或 DefineEditor 匯入成可執行表單的定義檔；DefineEditor 的精靈可依序匯入多份活頁簿並升級各表單的資料表。
- 新的範本套件 `Polhem.Templates` 以 `dotnet new polhem-avalonia` 建立應用程式：伺服器、Avalonia 桌面用戶端（可另加瀏覽器、iOS 與 Android 用戶端）、定義資料夾、工具，以及初始化資料庫、開啟 DefineEditor、一次啟動全部的 VS Code 工作。
- 新套件 `Polhem.UI.Avalonia.Shell` 是各端共用的現成 Avalonia 前端：連線、登入、開啟伺服器的表單，登出再登入即看到變更過的定義；`Polhem.UI.Avalonia` 新增 `VisualStudioTheme`。
- DefineEditor 以 dotnet tool `Polhem.DefineEditor` 發佈；新的 dotnet tool `Polhem.DbManager` 不需專案即可對定義資料夾執行 `db` 指令，並新增 `db check` 與 `db upgrade --table <名稱>`。
- DefineEditor 即時預覽正在編輯的 FormLayout（透過新的 `FormLayoutPresenter`）、顯示 TableSchema 的 CREATE TABLE 語法、側欄可切換檔案／功能／資料表，並改用 VS Code 的配色。
- 由明細表產生的 TableSchema 會替 `sys_master_rowid` 建立索引（`fk_{0}_master`），與手寫的一致。

## [1.7.0] - 2026-10-16

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.7.0.md](docs/zh-TW/changelogs/1.7.0.md)

- **破壞性：** 前端不再參考 API client：`ClientInfo` 與端點儲存移到新套件 `Polhem.UI.ApiBridge`，表單檢視改透過
  `UIServices` 取得連接器與定義。
- **破壞性：** `Polhem.Api.Core` 的伺服端（dispatch、授權、存取控制）移到新套件 `Polhem.Api.Server`；wire 不變。
- **破壞性：** 移除 `FileHashValidator` 與 `IDisplayName`。
- `FormView` 與 `ListView` 不需要 Polhem 後端也能執行，依 host 提供的 schema、layout 與連接器運作。

## [1.6.0] - 2026-10-11

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.6.0.md](docs/zh-TW/changelogs/1.6.0.md)

- **破壞性：** `Polhem.Api.Client` 只有一個進入點 `PolhemApiClient`，由它擁有連線與已登入的身分，並發出連接器。
- **破壞性：** `ApiClientInfo` 與 ambient 的 `ApiSessionContext` 已移除，其設定改放在 `PolhemApiClient`、`ClientInfo` 與 `ApiSessionCredentials`。
- **破壞性：** Blazor Server 為每個 circuit 注入一個 `PolhemApiClient`，取代 `PolhemApiConnectorFactory`，`FormPage` 則以該 client 上已登入的身分呼叫。
- **破壞性：** 連接器以所屬 client 目前的登入身分呼叫，連線檢查不再送出 API 金鑰，`ClientInfo.ApplyLoginResult` 一律丟棄快取的定義。
- 從這一版起，1.x 的次版號可以包含破壞性變更（[ADR-052](maintainers/adr/adr-052-breaking-changes-in-1-x-minors.md)），引用套件時請使用不跨次版號的範圍，例如 `[1.6,1.7)`；wire 與 1.5.0 相同。
- 同一個行程可以呼叫多台伺服器，每個 `PolhemApiClient` 各自擁有端點、API 金鑰與身分。

## [1.5.0] - 2026-10-09

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.5.0.md](docs/zh-TW/changelogs/1.5.0.md)

- **破壞性：** 在 SQLite、MySQL 與 Oracle 上，in-process 的表單讀取以 FormSchema 宣告的型別回傳數值與布林欄，AutoIncrement 的值超過 `int.MaxValue` 時也會讓讀取失敗，與經過 wire 時原本的行為一致。
- 表單讀取不再捨去 SQLite decimal 的小數，其 JSON 回應也能被依欄位型別解碼每個 cell 的客戶端（例如 polhem-connector-js）解碼（[#99](https://github.com/polhem-dev/polhem/issues/99)）。
- `DbCommandSpec.ColumnTypes` 依欄名宣告 `DataTable` 命令的結果欄位要以哪個 CLR 型別建立。

## [1.4.0] - 2026-10-08

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.4.0.md](docs/zh-TW/changelogs/1.4.0.md)

- **破壞性：** 參考 Polhem 套件的專案需要 .NET SDK 10.0.400 以上，因為 `Polhem.Definition` 的分析器改以 Microsoft.CodeAnalysis 5.9 建置。
- **破壞性：** 移除改用 Polhem.JsonRpc 後已無用途的 `PayloadFormat`（`Polhem.Api.Core.Messages`）、`Gzip` 與 `JsonRpcException`。
- **破壞性：** `[TreeNode]` 與 `[TreeNodeIgnore]` 從 `Polhem.Core` 移到 `Polhem.Definition`，`DatabaseItem` 改以 `Id` 而非 `DbName` 為標籤。
- `ObjectTreeBuilder` 與 `ObjectTreeView` 從物件圖建出並顯示樹，支援命令與拖放排序。
- `PropertyGridControl` 與 `CollectionEditDialog` 依 ComponentModel 標註編輯物件，DefineEditor 現在改用它們與物件樹。
- 搭配 Avalonia 12.1 時，登入者的 culture 不再退回作業系統的設定。

## [1.3.1] - 2026-10-05

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.3.1.md](docs/zh-TW/changelogs/1.3.1.md)

- **破壞性：** Encoded 或 Encrypted 呼叫的回應沒有結果時（只有非 Polhem 的伺服端會這樣回應），`ApiConnector` 改擲出 `InvalidPayloadException`，不再是 `InvalidOperationException`。
- `ApiConnector` 改透過新套件 `Polhem.JsonRpc.Payload.Client` 的 `PayloadConnector` 封裝呼叫，需要 Polhem.JsonRpc 1.2.0 以上；線路格式與 1.3.0 相同。
- 文件說明共用 API 金鑰（`StaticApiEncryptionKeyProvider`）留下的缺口：攔截到的呼叫可以在另一個 session 重放。

## [1.3.0] - 2026-10-05

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.3.0.md](docs/zh-TW/changelogs/1.3.0.md)

- **破壞性（wire）：** Polhem 改建在 Polhem.JsonRpc 1.1 上，它把每個 Encrypted payload 綁定到方法與方向，並以請求的格式回應結果，所以伺服端與所有遠端用戶端（包括 polhem-connector-js 的建置）必須一起升級。
- **破壞性：** 套件需要 Polhem.JsonRpc 1.1.1 以上，伺服端旁邊有 `Polhem.JsonRpc.AspNetCore` 1.0 或其他以 `Polhem.JsonRpc.Server` 1.0 編譯的程式碼時無法啟動。
- **破壞性（wire）：** 開啟 `RequireFrame` 時，宣告 `ReplayProtection = UniqueSequence` 的方法（包括 `Save` 與 `Delete`）拒絕遠端的 Plain 與 Encoded 呼叫。
- **破壞性（wire）：** 沒有值可繫結的請求會在方法執行前以 `-32602 Invalid params` 回應。
- **破壞性（wire）：** 找不到定義時丟出新的 `DefinitionNotFoundException`，遠端呼叫端以使用者訊息（`-32099`）收到。
- 連線到遠端端點時不再於 ping 之前送 HTTP `HEAD` 請求。

## [1.2.0] - 2026-10-03

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.2.0.md](docs/zh-TW/changelogs/1.2.0.md)

- **破壞性（wire）：** 錯誤回應改為符合 JSON-RPC 2.0（內部錯誤為 -32603、回應不再帶 `method` 成員、格式錯誤的方法名稱回 `MethodNotFound`），因此 polhem-connector-js 等非 .NET 用戶端要與伺服器一起升級。
- **破壞性：** JSON-RPC 改建在 `Polhem.JsonRpc` 套件上：移除 `Polhem.Api.AspNetCore`、`ApiServiceController`、`IJsonRpcProvider`、`ApiServiceOptions` 與 `Polhem.Api.Core` 的 payload 型別，是 1.x 內第二次語意化版本例外（[ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md)，英文）。
- **破壞性：** API key 或 `Authorization` header 被拒時回 HTTP 200 與 JSON-RPC 錯誤，不再回 401；`format` 未知的 payload 外殼會被拒絕。
- **破壞性：** in-process 呼叫與遠端呼叫一樣會序列化參數，被遮蔽的失敗與 API key 啟動檢查的 log 改記在新的 category。
- `AddPolhemPayload()`、`ApiClientInfo.PayloadOptions` 與 `PolhemPayload` 負責組出 payload 選項，`AddPolhemApiKeyGateCheck()` 為 HTTP host 加入 API key 啟動檢查。

## [1.1.0] - 2026-09-30

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.1.0.md](docs/zh-TW/changelogs/1.1.0.md)

- **破壞性：** `Polhem.Base` 改名為 `Polhem.Core`（套件 ID 與所有命名空間），是 1.x 內一次性的語意化版本例外（[ADR-048](maintainers/adr/adr-048-rename-base-to-core-in-1-1.md)，英文）。
- **破壞性：** 內建的 JSON-RPC 型別命名空間白名單改列 `Polhem.Core`，不再是 `Polhem.Base`。
- 使用者文件在 `docs/<lang>/` 下依主題分資料夾，以舊路徑連到文件的連結將無法開啟。
- 移除 `Web.Js.Demo` 範例；瀏覽器端改用 [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js)。

## [1.0.0] - 2026-09-28

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.0.0.md](docs/zh-TW/changelogs/1.0.0.md)

- **破壞性：** Polhem 以新名稱延續 Bee.NET 4.33.0：套件、命名空間、型別、CLI、analyzer 代號、MSBuild 屬性與其他所有 `Bee` 名稱都改為 `Polhem`，沒有相容層。
- **破壞性：** 首發前審查把公開型別改名、搬移、改為 sealed 或移除，用戶端 API 改為接收服務提供者與取消權杖。
- **破壞性：** 不再儲存存取權杖、舊的密碼雜湊格式不再通過驗證，因此既有 session 全部結束，舊雜湊的帳號需要重設密碼。
- **破壞性：** 伺服端更嚴格地檢查記錄範圍、請求本文與匿名呼叫，儲存時強制檢查必填欄位，未分頁的 `GetList` 只回傳一頁。
- **破壞性（wire）：** wire 改用 `Polhem.*` 型別名稱與調整後的合約，因此 Bee.NET 用戶端與 Polhem 伺服端無法互通。
- 內建文字以英文為基底並隨附依使用者文化切換的 `zh-TW` 翻譯，Avalonia head 可在 iOS、Android 與瀏覽器上執行。
