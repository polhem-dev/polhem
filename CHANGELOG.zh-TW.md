# 版本變更記錄

[English](CHANGELOG.md)

Polhem 套件的重要變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號自 2.0.0 起依循
[語意化版本](https://semver.org/lang/zh-TW/)。每個版本在這裡以一行列一項變更；理由與背景寫在
[`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下該版本的明細。

> **1.x 的 minor 版可能含破壞性變更**，列在「破壞性 API 變更」或「破壞性線路變更」下，並附遷移步驟；
> patch 版不會。引用套件時請使用不跨 minor 版的範圍，例如 `[1.5,1.6)`，不要用 `1.*`。這項政策與理由見
> [ADR-052](maintainers/adr/adr-052-breaking-changes-in-1-x-minors.md)。

## [Unreleased]

## [1.6.0] - 2026-10-11

📄 完整說明與背景：[docs/zh-TW/changelogs/1.6.0.md](docs/zh-TW/changelogs/1.6.0.md)

### 破壞性 API 變更

- `Polhem.Api.Client` 只有一個進入點：`PolhemApiClient`，由它擁有連線與已登入的身分
  （[ADR-053](maintainers/adr/adr-053-api-client-composition-root.md)）。以 `PolhemApiClient.CreateRemote(endpoint, apiKey)`
  或 `PolhemApiClient.CreateLocal(services)` 建立一次，透過 `client.System.LoginAsync` 登入，再從它取得連接器
  （`client.System`、`client.AuditLog`、`client.Form(progId)`）：之後每個連接器都以該次登入的權杖、傳輸金鑰與時區呼叫，
  不必再到處傳遞權杖。接收端點或服務提供者加存取權杖的連接器建構子已移除，每個連接器只剩一個接收 client 的建構子。
  `ApiConnector.AccessToken` 與 `ApiConnector.Provider` 已移除，`LocalApiProvider` 與 `RemoteApiProvider` 改為 internal。
- `ApiClientInfo` 已移除。`Endpoint` 與 `ConnectType` → `PolhemApiClient.Endpoint` 與 `IsLocal`（桌面：
  `ClientInfo.ConnectType`）；`ApiKey` → `CreateRemote` 的參數，或 `PolhemApiClient.ApiKey`；`PayloadOptions` →
  `PolhemApiClient.PayloadOptions`；`SupportedConnectTypes` → `ClientInfo.SupportedConnectTypes`；`DefaultLanguage` →
  `PolhemApiClient.DefaultLanguage`（Blazor：`PolhemBlazorOptions.DefaultLanguage`）。
  `ApiConnectValidator.ValidateAsync` 的第二個參數改為允許的連線方式。
- `ApiSessionContext.Ambient` 以及 `ApiSessionContext.ApiEncryptionKey`、`UserTimeZoneId` 的 setter 已移除。session
  改持有一個 `ApiSessionCredentials`，由 `SignIn` 與 `SignOut` 整份替換。
- Blazor Server：`PolhemApiConnectorFactory` 已移除。`AddPolhemBlazor` 為每個 circuit 註冊一個 scoped 的
  `PolhemApiClient`，改注入它來取代 factory。`UseRemoteProvider` 的第二個參數改為 API 金鑰，取代設定
  `ApiClientInfo.ApiKey`。`FormPage` 不再有 `AccessToken` 參數：它以 circuit 的 client 上已登入的身分呼叫。
- 桌面 head 的 `ClientInfo` 簽章不變；把 `ApiClientInfo.SupportedConnectTypes` 與 `ApiClientInfo.ConnectType`
  改為 `ClientInfo` 上的同名成員。`LoginAsync` 之後，可用同一個 `ClientInfo.SystemApiConnector` 呼叫
  `EnterCompanyAsync`，不必重新讀取。

### 新增

- `PolhemApiClient`、`ApiSessionCredentials`、`ClientInfo.ApiClient`，以及 `PolhemBlazorOptions.ApiKey` 與
  `DefaultLanguage`。同一個行程可以呼叫多台伺服器，每個 client 各自擁有端點、API 金鑰與身分。

## [1.5.0] - 2026-10-09

📄 完整說明與背景：[docs/zh-TW/changelogs/1.5.0.md](docs/zh-TW/changelogs/1.5.0.md)

### 新增

- `DbCommandSpec.ColumnTypes`：依欄名指定結果欄位要以哪個 CLR 型別建立，取代 provider 回報的型別。欄位在讀取資料列之前就建好，
  每個值在載入時就轉成宣告的型別。與 `DateColumns` 相同，只適用於 `DataTable` 命令。

### 修正

- 表單讀取（`GetList`、`GetData` 與依 row id 的讀取）在所有 provider 上，都以 FormSchema 欄位宣告的型別回傳數值與布林欄。
  SQLite 上，第一列是整數值的 decimal 欄會被讀成 `long`，之後每一列的小數都被靜默捨去；Boolean、Short、Integer 與
  AutoIncrement 欄讀成 `long`。MySQL 把 AutoIncrement 讀成 `long`，Oracle 讀成 `decimal`。這些欄位經 JSON codec 送出時是字串，
  與宣告的型別不符，依欄位型別解碼的客戶端（例如 polhem-connector-js）因此拒收回應，而 `Save` 在那之前已經提交。
  AutoIncrement 的值超過 `int.MaxValue`（MySQL、Oracle 與 SQLite 的 64 位元 identity 欄存得下）時，in-process 的表單讀取現在也會失敗，
  與經過 wire 時原本的行為一致。（[#99](https://github.com/polhem-dev/polhem/issues/99)）

## [1.4.0] - 2026-10-08

📄 完整說明與背景：[docs/zh-TW/changelogs/1.4.0.md](docs/zh-TW/changelogs/1.4.0.md)

### 破壞性 API 變更

- 移除 `Polhem.Api.Core.Messages.PayloadFormat`。各 connector 的 `ExecuteAsync` 與 `ApiCallContext` 改用成員與值都相同的
  `Polhem.JsonRpc.Payload.PayloadFormat`：用到 `PayloadFormat` 的地方把 `using Polhem.Api.Core.Messages;` 換成
  `using Polhem.JsonRpc.Payload;` 後重新編譯即可。線路格式不變。
  （[ADR-051](maintainers/adr/adr-051-remove-jsonrpc-leftovers-in-1-x.md)）
- 移除 `Polhem.Core.Serialization.Gzip`，框架自 1.2.0 起就沒有用到它。請直接使用 `GZipStream`。
  （[ADR-051](maintainers/adr/adr-051-remove-jsonrpc-leftovers-in-1-x.md)）

### 移除

- `Polhem.Api.Core.JsonRpc.JsonRpcException`。框架自 1.2.0 起就不再 throw 它。原本 throw 它來顯示訊息的 business
  object 請改 throw `UserMessageException`，它以同一個錯誤碼（`-32099`）傳送。exception 型別可以在 minor 版變動
  （[ADR-046](maintainers/adr/adr-046-api-evolution-policies-for-1-0.md) decision 4）。

### 新增

- `Polhem.Definition.ObjectTree`：`ObjectTreeBuilder` 依 `[TreeNode]` 與 `[TreeNodeIgnore]`，從物件圖建出與 UI 無關的
  `ObjectTreeNode` 樹。節點會發出變更通知，UI head 可以直接繫結；走訪時每個物件只產生一個節點，並在
  `ObjectTreeOptions.MaxDepth` 停止；消費端可用 `PropertyFilter`、`NodeBuilt`、`LabelTranslator` 調整樹。
- `TreeNodeAttribute.GetDisplayText(object, Func<string, string>?)` 會在填入屬性值之前先翻譯顯示格式。
- `ObjectTreeView`（`Polhem.UI.Avalonia`）：顯示 `ObjectTreeNode` 樹的 `TreeView`。設定 `RootNode` 即可，可選
  `IconSelector` 在每個標籤前顯示圖示；每個項目的 `IsExpanded` 與節點雙向繫結。
- `ObjectTreeNode.IsExpanded` 與 `ObjectTreeOptions.ExpandDepth`，後者設定建出的樹一開始展開幾層。
- `ITreeNodeCommandProvider` 與 `TreeNodeCommand`（`Polhem.Definition.ObjectTree`）：與 UI 無關的樹節點命令。
  `ObjectTreeView.CommandProvider` 在每次開啟右鍵選單時，以選取節點的命令填入選單。
- `ITreeNodeDragDropHandler` 與 `TreeNodeDropPosition`（`Polhem.Definition.ObjectTree`）：決定哪些樹節點可以拖曳、可以放在哪裡。
  `ObjectTreeView.DragDropHandler` 讓使用者把節點拖到另一個節點之前或之後，並顯示落點；handler 搬動物件後，控件再搬動樹上的節點。
- `ObjectTreeNode(object, Func<object, string>, bool)`：標籤由值計算、`Refresh` 時重算的節點，用於消費端自行加入、需要跟著切換顯示語言的節點。
- `PropertyGridControl`（`Polhem.UI.Avalonia`）：依 `[Category]`、`[Description]`、`[DisplayName]`、`[Browsable]`、
  `[DefaultValue]` 與 `[TypeConverter]`，把 `SelectedObject` 的屬性顯示成標籤與編輯器。值與預設值不同時以粗體顯示，標籤的右鍵選單可重設；
  每次寫回都會引發 `PropertyValueChanged`。`LabelTranslator` 翻譯標籤：每段文字以 `PropertyGridText` 傳入，附上
  `PropertyGridTextKind`（分組、顯示名稱、說明、項目標籤）、所屬型別與屬性名稱，因此說明可以依屬性查詢；`Refresh` 重新讀取物件並重新翻譯。
  標有 `[PasswordPropertyText(true)]` 的屬性會遮蔽顯示；`ValueSuggestionProvider` 讓字串屬性改為列出建議值、仍可自行輸入的下拉清單；`PropertyFilter` 可縮小顯示的列，例如只顯示一個分組。
  集合屬性以 `CollectionEditDialog`（或 `CollectionEditorProvider` 提供的編輯器）開啟；巢狀物件只唯讀顯示。
  控件透過 `TypeDescriptor` 讀取型別，定位為桌面工具使用，不適用於經過 trim 的行動端。
- `CollectionEditDialog` 與 `CollectionEditContext`（`Polhem.UI.Avalonia`）：編輯集合屬性的對話框，左邊是項目清單（新增、刪除、上移、下移），
  右邊以 `PropertyGridControl` 編輯選取的項目。對話框編輯項目的副本，按確定時以副本取代集合的項目，因此先前持有的項目參照要重新讀取；
  按取消則集合完全不變。有鍵值的集合中，新項目會取得未使用的鍵值，按確定時拒絕重複的鍵值。
- `PolhemUIText.ResetValue`、`CollectionSummary`、`MoveUp`、`MoveDown`、`CollectionKeyMissing`、`CollectionKeyDuplicate` 與
  `CollectionKeyTaken`：屬性方格與集合對話框使用的文字鍵。屬性方格寫入的鍵值已被集合中其他項目使用時，會以介面語言顯示
  `CollectionKeyTaken`，而不是集合擲出的英文例外訊息。

### 變更

- `TreeNodeAttribute` 與 `TreeNodeIgnoreAttribute` 從 `Polhem.Core.Attributes`（`Polhem.Core`）移到
  `Polhem.Definition.Attributes`（`Polhem.Definition`），與它們所標註的定義型別放在一起。框架目前沒有任何程式讀取它們，
  套用與否對行為沒有影響；自訂型別若有套用，把 `using Polhem.Core.Attributes;` 換成 `using Polhem.Definition.Attributes;` 即可。
- 定義型別的 `[TreeNode]` 標註改依同一條資料夾規則：擁有者有兩個以上帶標註的集合時，集合以資料夾呈現，否則直接掛在擁有者下。
  `FormLayout` 的 Sections 與 Details、`FormSchema` 的 Tables 與 Rules 改為資料夾；`FormTable` 的 Fields 與 `LanguageEnum` 的
  Entries 不再是資料夾。每個帶標註的物件型別都明確寫出標籤取自哪些屬性，與其 `ToString()` 一致；`IndexField` 與其集合也補上標註，索引底下會列出索引欄位。
  `TreeNodeAnnotationGateTests` 固定這兩項慣例。
- `PermissionRule.Action` 宣告了型別轉換器，其互斥的標準值是單一動作，因此屬性方格會給單一動作的下拉清單，而不是可組合旗標的文字框。
  規則的 XML 形式不變。
- 定義型別在屬性方格中顯示的每個屬性都宣告了 `[Category]` 與 `[Description]`，會依分組顯示並附上說明，不再落到 `Misc`；
  `EditorAnnotationGateTests` 固定這項慣例。編輯器以樹節點呈現的集合（表單結構描述的資料表與規則、各設定檔的項目、語系資源的項目與列舉）
  以及 `SessionUser.AccessToken` 標為 `[Browsable(false)]`。這兩種標註都不影響 XML 與 wire 的形式。
- `SecurityKeySettings` 的加密金鑰標上 `[PasswordPropertyText(true)]`，屬性方格會遮蔽顯示。`LanguageItem.Key` 重新標為可瀏覽：
  它覆寫的基底鍵值標了 `[Browsable(false)]`，覆寫時被一併繼承。
- `DatabaseItem` 的 `[TreeNode]` 標註與 `ToString()` 改以 `Id` 而非 `DbName` 為標籤：不指定資料庫名稱的項目（例如 SQLite）原本顯示空白名稱。
- `Polhem.Definition` 套件所附的 Roslyn 分析器改以 Microsoft.CodeAnalysis 5.9 建置，參考此套件的專案需要 .NET SDK
  10.0.400 以上。較舊的編譯器會回報 `CS9057` 而不執行分析器，在 `TreatWarningsAsErrors` 下建置會失敗。
- 套件相依升為 MessagePack 3.1.11、DynamicExpresso.Core 2.19.6，以及框架所參考的 `Microsoft.Extensions` 套件的 10.0.12 版。

### 修正

- 搭配 Avalonia 12.1 時，用戶端登入後的 culture 不再退回作業系統的設定。Avalonia 12.1 會在每次 dispatcher 作業後寫回 UI 執行緒的
  culture，而登入者的 culture 是在非同步處理常式中設定的，到下一個 `await` 就被還原：en-US 帳號會顯示作業系統的語言。
  `ClientInfo` 現在也會把 culture 投遞到呼叫端的同步內容。([#95](https://github.com/polhem-dev/polhem/pull/95))

### 範例與工具

- DefineEditor 的每一種文件樹都改用 `ObjectTreeBuilder` 建樹、以 `ObjectTreeView` 顯示，右鍵選單由各文件型別的
  `ITreeNodeCommandProvider` 提供，取代原本手寫的樹節點。標籤依 `[TreeNode]` 標註，資料夾不再顯示數量。每個編輯器都可以把節點拖到同層的另一個節點之前或之後，調整它所在集合的順序。資料夾、根節點與群組的標籤會依編輯器的語言顯示，切換語言時立即更新。
- DefineEditor 改以 `PropertyGridControl` 顯示選取的物件，取代手寫的屬性面板，因此每個可瀏覽的屬性都能編輯，並依 `[Category]` 分組、
  以 `[Description]` 說明。分組與說明已翻成繁體中文，會跟著編輯器的語言切換；屬性名稱維持原文。欄位的 ListItems 改在方格的集合對話框中編輯，
  不再是樹節點；選取欄位的 Relation 或 Lookup 群組時只顯示 Relation 屬性，表單代碼、對應欄位與方案內的語系列舉會列出建議值。貼上連線字串改為資料庫
  Server 或 Item 右鍵選單開啟的對話框，預覽不再顯示密碼。修改物件名稱後，樹上的標籤會立即更新。
- Avalonia.DemoCenter 新增 Property Grid 案例：以 `PropertyGridControl` 顯示 `FormField`、`DbField`、`DatabaseServer`、
  `PermissionRule`、`BackendConfiguration` 與一個「群組含項目」的示範物件，可切換顯示選項與表單代碼建議值，並記錄 `PropertyValueChanged`；
  FormField 的 `ListItems` 可在 `CollectionEditDialog` 中編輯，群組的項目會在第一層對話框之上再開第二層。
- Avalonia 的各個 head（DefineEditor、Avalonia.DemoCenter、Northwind 各 head）改用 Avalonia 12.1 與 Semi.Avalonia 12.1；
  `Polhem.UI.Avalonia` 仍以 Avalonia 12.0.0 為最低版本。Northwind 的 iOS head 改為需要 iOS 15.0，這是 .NET SDK 10.0.400 的
  iOS workload 所接受的最低版本。([#95](https://github.com/polhem-dev/polhem/pull/95)、[#96](https://github.com/polhem-dev/polhem/pull/96))

## [1.3.1] - 2026-10-05

📄 完整說明與背景：[docs/zh-TW/changelogs/1.3.1.md](docs/zh-TW/changelogs/1.3.1.md)

### 變更

- `ApiConnector` 改透過新套件 `Polhem.JsonRpc.Payload.Client` 的 `PayloadConnector` 封裝呼叫；`Polhem.Api.Client` 改參考該套件，
  取代 `Polhem.JsonRpc.Client`（仍經由它間接參考）。需要 Polhem.JsonRpc 1.2.0 以上。線路格式，以及呼叫採用的格式、金鑰與序號都不變。
  ([#63](https://github.com/polhem-dev/polhem/pull/63))

### 文件

- 說明共用 API 金鑰（`StaticApiEncryptionKeyProvider`）留下的缺口：在時間戳記的容許範圍內，於某個 session 攔截到的呼叫
  可以在另一個 session 重放。([#62](https://github.com/polhem-dev/polhem/pull/62))

## [1.3.0] - 2026-10-05

> Polhem 改建在 [Polhem.JsonRpc](https://github.com/polhem-dev/polhem-jsonrpc) 1.1（1.1.1 以上）上，它的 payload
> 線路格式與 Polhem 1.2.0 的不相容：每個 Encrypted payload 的 HMAC 也涵蓋它的方法與方向，結果也以請求的格式回應。
> **伺服端與所有遠端用戶端必須一起升級**：以 `Polhem.Api.Client` 建構的 .NET 用戶端，以及從
> [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js) `main` 上合併
> [polhem-dev/polhem-connector-js#11](https://github.com/polhem-dev/polhem-connector-js/pull/11) 的 commit 或之後建置的
> 瀏覽器用戶端。Polhem 自己的公開 .NET API 只有新增，因此以次版號發佈：繼 1.1.0 與 1.2.0 之後，1.x 內的第三次例外。
> 理由見 [ADR-050](maintainers/adr/adr-050-jsonrpc-1-1-wire-break-in-1-3.md)（英文）。

📄 完整說明與背景：[docs/zh-TW/changelogs/1.3.0.md](docs/zh-TW/changelogs/1.3.0.md)

### 安全性

- 在 1.2.0 上，攔截到 Encrypted 請求的人可以把它送給同一個 session 的另一個方法（例如把讀取變成刪除），也可以把結果
  當成請求送回去：HMAC 只涵蓋 payload 的位元組本身。1.3.0 把方法與方向納入 HMAC，封住這兩條路
  （[ADR-050](maintainers/adr/adr-050-jsonrpc-1-1-wire-break-in-1-3.md)，英文；
  [Polhem.JsonRpc 的 ADR-003](https://github.com/polhem-dev/polhem-jsonrpc/blob/main/maintainers/adr/adr-003-bind-method-into-payload-hmac.md)，英文）。
  這個修正改變了線路格式；它對升級的影響見下方「破壞性線路變更」。([#60](https://github.com/polhem-dev/polhem/pull/60))

### 破壞性線路變更

- 每個 Encrypted payload 的 HMAC 也涵蓋它的方法與方向（見上方「安全性」）。版本不相符的伺服端與用戶端，每個 Encrypted
  呼叫都會失敗，伺服端回 `-32603 Internal error`。([#60](https://github.com/polhem-dev/polhem/pull/60))
- 要求 wire frame（`RequireFrame`）時，宣告 `ReplayProtection = UniqueSequence` 的方法會以 `-32602 Invalid params`
  拒絕已登入 session 的遠端 Plain 或 Encoded 呼叫。Plain 呼叫過去不檢查序號而直接接受；Encoded 呼叫雖會檢查，但它的
  序號任何人都能偽造，因為只有 Encrypted 的 frame 受 HMAC 保護。框架自己的 `Save`、`Delete`、`ExecFunc`、
  `EnterCompany` 與 `LeaveCompany` 都是這類方法，所以沒有 session 金鑰的用戶端（例如 .NET 瀏覽器 head，即
  WebAssembly）在 `RequireFrame` 下無法再呼叫它們。([#60](https://github.com/polhem-dev/polhem/pull/60))

升級時，伺服端與它的用戶端在同一個時段一起升級，並把直接參考的每個 `Polhem.JsonRpc.*` 套件升到 1.1.1 以上。以 HTTP
提供 API 的 host 自己參考 `Polhem.JsonRpc.AspNetCore`；它的 1.0 版會讓 dispatcher 無法啟動：

```diff
- <PackageReference Include="Polhem.Hosting" Version="1.2.0" />
- <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="1.0.0" />
+ <PackageReference Include="Polhem.Hosting" Version="1.3.0" />
+ <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="1.1.1" />
```

`JsonRpcServerOptions.AllowCodeCompiledAgainst10` 無法跳過這個啟動錯誤：從 1.1.1 起它沒有作用，因為以 1.0 編譯、
讀取或設定傳輸種類的程式碼，可能把 HTTP 呼叫當成行程內呼叫，而 Polhem 對行程內呼叫不做存取檢查。自己以 `Polhem.JsonRpc.Server` 1.0 編譯的
程式碼（例如 filter）必須重新編譯。自己實作的 `IPayloadEncryptor` 必須實作接受 associated data 的多載，並驗證那份資料：
忽略它的實作會接受 1.2.0 的形式，讓兩種攻擊重新打開。Polhem.JsonRpc 自己的其餘變更見
[它的變更記錄](https://github.com/polhem-dev/polhem-jsonrpc/blob/main/CHANGELOG.md)。

### 新增

- `Polhem.Definition.Storage` 的 `DefinitionNotFoundException`：衍生自 `FileNotFoundException`，帶有定義類型與鍵。([#51](https://github.com/polhem-dev/polhem/pull/51))

### 行為變更

- **Wire 可見：** 結果一律以請求送出時的格式回應（`null` 結果也一樣），`ApiConnector` 收到其他格式的結果時會以
  `InvalidPayloadException` 拒絕。([#60](https://github.com/polhem-dev/polhem/pull/60))
- **Wire 可見：** 參數沒有可繫結的值（沒有 `params` 成員，或 payload 信封沒有 `value`、`value` 為 `null`）的請求，
  會在方法執行前以 `-32602 Invalid params` 回應。過去會以 `null` 引數呼叫方法：多數框架方法以 `ArgumentNullException`
  檢查它，回 `-32099` 與「The request is not valid.」；其餘方法則失敗並回 `-32603 Internal error`。
  此行為與 `Polhem.JsonRpc` 預設的 binder 一致。([#60](https://github.com/polhem-dev/polhem/pull/60))
- 行程內呼叫不檢查序號是否重複。([#60](https://github.com/polhem-dev/polhem/pull/60))
- 套件相依的 Polhem.JsonRpc 改為 `[1.1.1, 2.0.0)`，不再是 `1.0.0` 以上。1.1.1 是第一個只要旁邊有以
  `Polhem.JsonRpc.Server` 1.0 編譯、且在 dispatcher 建立時已載入的組件，dispatcher 就一律拒絕啟動的版本：`AllowCodeCompiledAgainst10` 在其中沒有作用。
  ([#60](https://github.com/polhem-dev/polhem/pull/60))
- 內建的檔案或資料庫儲存中應存在卻找不到的定義（表單結構描述、資料表結構描述、程式登錄、資料庫分類）改為丟出
  `DefinitionNotFoundException`；自訂的儲存沒有傳回表單結構描述時，`GetFormSchema` API 也丟出它。遠端呼叫端會以
  UserMessage 錯誤碼（-32099）收到它的訊息（例如 `FormSchema 'Employee' not found.`），不再是檔案儲存回的通用
  InternalError（-32603）或資料庫儲存回的固定訊息。訊息只含定義類型與它的鍵，不含任何路徑。資料庫儲存過去丟出
  `InvalidOperationException`；以 catch 該型別處理找不到定義的程式碼，必須改為 catch `DefinitionNotFoundException`
  （或 `FileNotFoundException`）。([#51](https://github.com/polhem-dev/polhem/pull/51))
- 連線到遠端端點時（`ApiConnectValidator`，以及 UI head 透過它呼叫的 `ClientInfo`）只以 ping 檢查端點，不再先送 HTTP
  `HEAD` 請求；端點每次連線都以 405 回應那個請求。連不上的主機仍回報為 `Endpoint not reachable`。([#50](https://github.com/polhem-dev/polhem/pull/50))

### 範例與工具

- Northwind 在瀏覽器與 iOS head 上，也會在選單從伺服端載入後開啟選單的第一個表單。([#49](https://github.com/polhem-dev/polhem/pull/49))

### 文件

- 從 Bee.NET 遷移的指南從 README 移到
  [`docs/zh-TW/guides/migrating-from-bee-net.md`](docs/zh-TW/guides/migrating-from-bee-net.md)。([#52](https://github.com/polhem-dev/polhem/pull/52))

## [1.2.0] - 2026-10-03

> JSON-RPC 改建在 [`Polhem.JsonRpc`](https://github.com/polhem-dev/polhem-jsonrpc) 套件上，payload 外殼、加密與重放 frame
> 改建在它的選用 payload 套件上。移除 `Polhem.Api.AspNetCore` 與 `ApiServiceOptions`，這會讓 host 編譯失敗，卻在次版號
> 發佈：繼 1.1.0 之後，1.x 內的第二次例外。
> 線路上的參數與結果外殼沒有改變；內部錯誤碼與回應的 `method` 成員改為符合 JSON-RPC 2.0，因此非 .NET 的用戶端
> 要與伺服器一起升級。理由，以及本版視為框架內部管線的型別，見
> [ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md)（英文）。

📄 完整說明與背景：[docs/zh-TW/changelogs/1.2.0.md](docs/zh-TW/changelogs/1.2.0.md)

### 破壞性 API 變更

- 移除 `Polhem.Api.AspNetCore` 套件，連同 `ApiServiceController` 與 `UsePolhemFramework()`。host 改用
  `Polhem.JsonRpc.AspNetCore` 提供 API。([#46](https://github.com/polhem-dev/polhem/pull/46))
- 移除 `IJsonRpcProvider`。`RemoteApiProvider` 與 `LocalApiProvider` 改實作套件的 `IJsonRpcTransport`，
  `ApiConnector.Provider` 也改為該型別。([#46](https://github.com/polhem-dev/polhem/pull/46))
- 移除 `Polhem.Api.Core.JsonRpc` 的 `JsonRpcExecutor`，以及訊息型別 `JsonRpcRequest`、`JsonRpcResponse`、`JsonRpcError`。([#46](https://github.com/polhem-dev/polhem/pull/46))
- 移除 `ApiServiceOptions`，連同 `Polhem.Api.Core` 的 payload 型別：transformer、serializer、壓縮器與加密器的介面及實作、
  `ApiPayloadOptionsFactory`、外殼型別（`ApiPayload`、`JsonRpcParams`、`JsonRpcResult`、`ApiPayloadConverter`）、
  `ApiPayloadFrame`、`IReplayWindowStore`、`MemoryReplayWindowStore` 與 `ReplayRejectedException`。替代品在
  `Polhem.JsonRpc.Payload`；用戶端捕捉重放呼叫的例外改為 `Polhem.JsonRpc.Payload.ReplayRejectedException`。([#47](https://github.com/polhem-dev/polhem/pull/47))
- `MessagePackPayloadSerializer` 改名為 `MessagePackPayloadCodec`，實作套件的 `IPayloadCodec`。([#47](https://github.com/polhem-dev/polhem/pull/47))
- `IApiAuthorizationValidator` 改從 service collection 取得，不再是 `ApiServiceOptions.AuthorizationValidator`；要替換預設值就
  註冊自己的實作。([#47](https://github.com/polhem-dev/polhem/pull/47))

以 HTTP 提供 API 的 host 升級方式：

```diff
- <PackageReference Include="Polhem.Api.AspNetCore" Version="1.1.0" />
+ <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="1.0.0" />
```

```diff
  builder.Services.AddPolhemFramework(configuration, paths);
- builder.Services.AddControllers();
+ builder.Services.AddJsonRpcServer();
+ builder.Services.AddPolhemApiKeyGateCheck();
  var app = builder.Build();
- app.UsePolhemFramework();
- app.MapControllers();
+ app.MapJsonRpc("/api");
```

刪除衍生自 `ApiServiceController` 的 controller。覆寫過其成員的檢查改寫成 filter，以
`AddJsonRpcServer(options => options.Filters.Add(...))` 加入。

每個 host 都要替換 `ApiServiceOptions` 的呼叫：

```diff
- ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);
- ApiServiceOptions.RequireWireFrame = true;
+ builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode,
+     options => options.RequireFrame = true);
```

.NET 用戶端把 `ApiClientInfo.PayloadOptions.RequireFrame` 設成與伺服器一致。其餘遷移步驟（codec、重放紀錄、授權驗證器、
同一行程也呼叫 API 的 host）見 [ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md)（英文）決策 5。

### 新增

- `Polhem.Hosting` 的 `AddPolhemApiKeyGateCheck()`：尚未發行 API key 時於啟動時記錄 log，供以 HTTP 提供 API 的 host 使用。([#46](https://github.com/polhem-dev/polhem/pull/46))
- `Polhem.Hosting` 的 `AddPolhemPayload()`、`Polhem.Api.Client` 的 `ApiClientInfo.PayloadOptions`，以及 `Polhem.Api.Core` 的
  `PolhemPayload`（以框架的方式組出 payload 選項）。([#47](https://github.com/polhem-dev/polhem/pull/47))

### 行為變更

- 內部錯誤改回錯誤碼 -32603（原為 -32000），`JsonRpcErrorCode.InternalError` 也改為此值。回應不再帶 `method` 成員。
  [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js) 用戶端需要對應 1.2.0 的版本。([#46](https://github.com/polhem-dev/polhem/pull/46))
- API key 或 `Authorization` header 被拒時，回 HTTP 200 與 JSON-RPC 錯誤，不再回 401；因此 .NET 用戶端丟出的是錯誤合約重建的
  例外，而不是 `HttpRequestException`。([#46](https://github.com/polhem-dev/polhem/pull/46))
- 方法名稱格式錯誤時回 `MethodNotFound`（-32601），不再回 `UserMessage`。找不到的方法以固定訊息回應，debug 模式也一樣，
  且不寫異常紀錄。([#46](https://github.com/polhem-dev/polhem/pull/46))
- in-process 呼叫與遠端呼叫一樣會序列化參數。([#46](https://github.com/polhem-dev/polhem/pull/46))
- `format` 不是 0、1 或 2 的 payload 外殼以無效參數拒絕。([#47](https://github.com/polhem-dev/polhem/pull/47))
- log category：被遮蔽的失敗記在 `Polhem.Api.Core.Dispatch.PolhemExceptionMapper`，API key 啟動檢查記在
  `Polhem.Hosting.ApiKeys.ApiKeyGateWarningService`。([#46](https://github.com/polhem-dev/polhem/pull/46))

## [1.1.0] - 2026-09-30

> `Polhem.Base` 改名為 `Polhem.Core`。依語意化版本，這應等到 2.0.0；它以一次性例外在 1.1.0 發佈，因為 1.0.0 沒有
> 已知的使用者，而且所有 1.0.0 套件都已下架（unlist）。從 1.1.0 起，1.x 依語意化版本演進，不再有例外。理由見
> [ADR-048](maintainers/adr/adr-048-rename-base-to-core-in-1-1.md)（英文）。

📄 完整說明與背景：[docs/zh-TW/changelogs/1.1.0.md](docs/zh-TW/changelogs/1.1.0.md)

### 破壞性 API 變更

- `Polhem.Base` 改名為 `Polhem.Core`：套件 ID 與所有命名空間。([#42](https://github.com/polhem-dev/polhem/pull/42))

從 1.0.0 升級（套件參考只在直接參考 `Polhem.Base` 時才需要改）：

```diff
- <PackageReference Include="Polhem.Base" Version="1.0.0" />
+ <PackageReference Include="Polhem.Core" Version="1.1.0" />
- using Polhem.Base.Serialization;
+ using Polhem.Core.Serialization;
```

### 行為變更

- 內建的 JSON-RPC 型別命名空間白名單列的是 `Polhem.Core`，不再是 `Polhem.Base`。([#42](https://github.com/polhem-dev/polhem/pull/42))

### 範例與工具

- 移除 `Web.Js.Demo` 範例；瀏覽器端改用
  [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js)。([#41](https://github.com/polhem-dev/polhem/pull/41))

### 文件

- 使用者文件在 `docs/<lang>/` 下依主題分資料夾，維護者文件與 ADR 移到 `maintainers/`。([#38](https://github.com/polhem-dev/polhem/pull/38)、[#39](https://github.com/polhem-dev/polhem/pull/39)、[#40](https://github.com/polhem-dev/polhem/pull/40))

## [1.0.0] - 2026-09-28

> Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library)。Polhem 1.0.0 是 Bee.NET 最後發佈的 4.33.0
> 改名之後，再加上以下各項變更。應用程式如何遷移，見 [從 Bee.NET 遷移](README.zh-TW.md#從-beenet-遷移)。

📄 完整說明與背景：[docs/zh-TW/changelogs/1.0.0.md](docs/zh-TW/changelogs/1.0.0.md)

### 由 Bee.NET 改名

改名在 repo 開始使用 pull request 之前完成，因此這些項目沒有連結。

- 套件 ID 與命名空間由 `Bee.*` 改名為 `Polhem.*`，版號從 1.0.0 重新起算。套件的切分不變。
- 以 Bee 命名的型別與成員改名：`AddBeeFramework` → `AddPolhemFramework`、`UseBeeFramework` →
  `UsePolhemFramework`、`AddBeeBlazor` → `AddPolhemBlazor`、`BeeBlazorOptions` → `PolhemBlazorOptions`、
  `BeeBlazorProviderMode` → `PolhemBlazorProviderMode`、`BeeApiConnectorFactory` → `PolhemApiConnectorFactory`、
  `BeeAccessTokenProvider` → `PolhemAccessTokenProvider`、`BeeLoginPanel` → `PolhemLoginPanel`，以及擴充方法類別
  `BeeFrameworkServiceCollectionExtensions`、`BeeFrameworkApplicationBuilderExtensions`、
  `BeeBlazorServiceCollectionExtensions` → `Polhem…`。`IBeeContext` / `BeeContext` 與 `BeeStringLocalizer<T>`
  改用不含框架名稱的新名稱，見〈破壞性 API 變更〉。
- 命令列工具 `Bee.Cli` 改為 `Polhem.Cli`，以 `dotnet polhem` 呼叫。
- analyzer 診斷代號由 `BEE` 改為 `POLHEM`，數字不變，例如 `BEE1008` → `POLHEM1008`。`POLHEM4001`–`POLHEM4004`
  保留不用：它們是 Bee.NET 的 `BEE4001`–`BEE4004`，永不重複使用。([#10](https://github.com/polhem-dev/polhem/pull/10))
- 定義檔檢查用的 MSBuild 屬性改名：`BeeDefinitionFilesGlob`、`BeeRequireDefinitionFiles`、`BeeAnalyzeDefinitionFiles`
  → `PolhemDefinitionFilesGlob`、`PolhemRequireDefinitionFiles`、`PolhemAnalyzeDefinitionFiles`。
- 主金鑰的預設環境變數由 `BEE_MASTER_KEY` 改為 `POLHEM_MASTER_KEY`。
- payload 與預設設定中的型別名稱改為 `Polhem.*`。Bee.NET 用戶端與 Polhem 伺服端（或反過來）無法互通。
- 衍生 session 加密金鑰的標籤由 `bee-api-*` 改為 `polhem-api-session-key` 與 `polhem-api-encryption-root-key`。
- Blazor 元件的 CSS class 由 `bee-` 開頭改為 `polhem-` 開頭。
- 記錄欄位宣告型別的 `DataColumn` 擴充屬性由 `Bee.FieldDbType` 改為 `Polhem.FieldDbType`；
  `SerializationErrorData.FilePath` 由 `Bee.FilePath` 改為 `Polhem.FilePath`。
- 套件中繼資料：作者與著作權人改為 Polhem contributors，repository 改為 `polhem-dev/polhem`，並換上新的圖示。
  `Polhem.Cli` 帶有相同的中繼資料、README 與符號套件。([#3](https://github.com/polhem-dev/polhem/pull/3))

### 安全性

- 不再儲存存取權杖：`st_session` 以 SHA-256 衍生的鍵值（`AccessTokenHasher`）為索引，記錄表改存簡短的權杖指紋
  （`token_fingerprint`），取代 `access_token` 欄位。([#5](https://github.com/polhem-dev/polhem/pull/5))
- 密碼以 PBKDF2-SHA256、600,000 次迭代雜湊；較弱的既存雜湊在下次登入成功時換新，沿用自 Bee.NET 的 PBKDF2-SHA1
  格式雜湊不再能通過驗證。([#5](https://github.com/polhem-dev/polhem/pull/5))
- 不存在的帳號也會執行一次誘餌雜湊，登入耗時不會透露帳號是否存在；登入嘗試追蹤器有容量上限。
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- 記錄範圍（record scope）也檢查更新或刪除實際指向的既存資料列、修改與刪除的明細列，以及儲存後主檔列留下的值。
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- `GetList` 與 `GetCount` 只接受表單宣告過的過濾與排序欄位，且不接受受保護欄位；`GetLookup` 套用讀取的記錄範圍
  （`FormBusinessObject.LookupAppliesRecordScope` 可退出）。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 遠端 `GetDefine` 只提供明列允許的定義類型。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 呼叫端無法靠改變 ProgId 的大小寫避開稽核規則：業務物件以 `ProgramSettings` 宣告的大小寫建立，稽核規則以不分大小寫
  的方式查找，稽核紀錄寫入 FormSchema 的 ProgId 寫法。([#4](https://github.com/polhem-dev/polhem/pull/4),
  [#26](https://github.com/polhem-dev/polhem/pull/26))
- Encoded 與 Encrypted 請求依解析出的 action 的參數型別解碼；兩種 codec 對巢狀過濾條件都套用深度上限；action
  只解析到公開、非泛型、單一參數且不是屬性存取子的執行個體方法（`JsonRpcExecutor.IsResolvableAction`，
  `POLHEM3001` 以同一規則檢查）。([#6](https://github.com/polhem-dev/polhem/pull/6))
- `Plain` 本文先讀成 action 的請求型別，再複製到其引數，與 Encoded 相同，因此 Plain 呼叫無法設定契約未宣告的成員。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- BCL 例外的訊息不再傳給遠端呼叫端；每個錯誤碼有固定訊息，原始訊息經 `JsonRpcExecutor.Logger` 記錄。
  ([#6](https://github.com/polhem-dev/polhem/pull/6))
- `CreateSession` 只接受本機呼叫。`CreateApiKey`、`SetApiKeyEnabled`、`SetApiKeyExpiry` 受重放防護。
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- 資料庫異常記錄只有部署管理員能讀取。([#5](https://github.com/polhem-dev/polhem/pull/5))
- 用戶端拒絕伺服端宣告的 "none" 加密器（除非自身處於除錯模式），也不再沿用伺服端的除錯旗標與型別命名空間。
  ([#6](https://github.com/polhem-dev/polhem/pull/6))
- 主金鑰檔與用戶端的 `apikey.txt` 以僅擁有者可存取的權限寫入；資料庫設定含密碼卻未設 `ConfigEncryptionKey` 時記錄警告。
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- DDL 會跳脫 SQL Server 的字串預設值，非字串預設值必須是該型別的字面值；連線字串的佔位字以
  `DbConnectionStringBuilder` 解析（`ConnectionStringTemplate`）。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 找不到定義檔時，錯誤訊息只寫檔名，不含伺服端上的路徑（`FileUtilities.EnsureFileExists`）。
  ([#26](https://github.com/polhem-dev/polhem/pull/26))

### 行為變更

- 不帶 `Authorization` 標頭的請求視為匿名呼叫：只由 `[ApiAccessControl]` 決定，需要 session 的方法回應 JSON-RPC
  `-32001`（Unauthorized），不再回 HTTP 401。用戶端把它轉成 `UnauthorizedAccessException`；`RemoteApiProvider` 在登入前不送 `Authorization` 標頭。
  ([#6](https://github.com/polhem-dev/polhem/pull/6), [#18](https://github.com/polhem-dev/polhem/pull/18))
- 未分頁的 `GetList` 回傳第一頁，上限為 `PagingOptions.MaxPageSize`。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 所有內建定義檔與 UI 文字以英文為基底語言；中文移到隨附的 `zh-TW` 語系資源。標題、列舉、選單、規則訊息與框架文字
  共用同一條回退鏈（`LanguageFallback`）：要求的文化、其上層文化、`CommonConfiguration.DefaultLanguage`，最後是基底文字。
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- 登入回應帶有使用者的文化，用戶端會採用。`CommonConfiguration.DefaultLang` 與 `BackendConfiguration.DefaultLanguage`
  合併為 `CommonConfiguration.DefaultLanguage`（預設 `zh-TW`）。([#15](https://github.com/polhem-dev/polhem/pull/15))
- 給終端使用者的框架訊息帶有鍵值與參數（`UserMessageException`），依 session 的文化解析；表單規則訊息解析
  `{ProgId}.Rule.{RuleId}.Message`。([#15](https://github.com/polhem-dev/polhem/pull/15))
- 數字與日期依使用者的文化顯示與剖析；wire 維持與文化無關。([#15](https://github.com/polhem-dev/polhem/pull/15))
- `ValueUtilities.CBool` 只把 `1`、`T`、`TRUE`、`Y`、`YES`（不分大小寫）視為 true；中文的「是」「真」不再被辨識。
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- `BackendComponents` 各項預設為空白，代表框架預設。`CacheProvider` 或元件型別名稱錯誤時，在啟動時失敗並指出是哪個設定。
  ([#8](https://github.com/polhem-dev/polhem/pull/8), [#11](https://github.com/polhem-dev/polhem/pull/11))
- 用戶端把端點與 API 金鑰存成每位使用者本機應用程式資料資料夾下的 `endpoint.txt` 與 `apikey.txt`
  （`FileEndpointStorage`，現在位於 `Polhem.UI.Core`），不再寫在組件旁的設定檔。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 經 MessagePack 傳輸的 `DataTable` 改寫成一份欄位表加上依位置排列的資料列。JSON 與 Plain 不變。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 初始值不是 CLR 預設值的 wire 成員一律寫出，因此不論哪種 codec，缺少的成員都代表 CLR 預設值。Plain 請求依 JSON
  種類繫結 `object` 型別的過濾與參數值。([#9](https://github.com/polhem-dev/polhem/pull/9))
- 序列化快取中的定義不再改動它：空集合改由唯讀的 `XSpecified` 屬性省略，不再使用每個物件的序列化狀態。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 方法要求 `ApiReplayProtection.UniqueSequence` 而 `RequireWireFrame` 關閉時，啟動時記錄警告，並列出未宣告權限模型的表單，
  包括 `ProgramSettings` 沒有列出的已儲存表單（`IDefineStorage.GetFormSchemaIds`）。
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6),
  [#26](https://github.com/polhem-dev/polhem/pull/26))
- `Short`、`Long`、`Decimal`、`Binary` 欄位的預設值具型別且不為 null。
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- 建立新列時以 `DefaultValueExpression` 為準：`GetNewData` 與用戶端新增的列會把運算式的值寫過字面值 `DefaultValue` 與依型別的初值。存檔時仍只填空的欄位。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- `ClientInfo.UseDefinitionLoader` 預設開啟，Avalonia 畫面不需設定即顯示在地化標題、租戶的版面與公司的數值格式。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 以 `AddPolhemFramework` 建立的 host 缺少 `common` 資料庫項目時不會啟動（`IDatabaseSettingsProvider.ValidateRequired`）。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 未知的 action 回應 JSON-RPC `-32601`，無法讀取的 Plain 本文回應 `-32602`，各有固定訊息。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 儲存時強制檢查 `FormField.Required`：新增或修改的資料列（主檔或明細）有必填欄位是空的，會以在地化訊息拒絕；Avalonia
  的 `FormView` 與 Blazor 的 `FormPage` 在送出儲存前列出所有這類欄位。請檢查各 FormSchema 的 `Required` 旗標：補上資料，
  或取消該旗標。([#26](https://github.com/polhem-dev/polhem/pull/26), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `ProgramSettings` 沒有列出的表單，其稽核紀錄改寫入 FormSchema 的 ProgId 寫法，而不是呼叫端的寫法；之前寫入的紀錄維持
  原樣，因此比對較舊的 `prog_id` 時請不分大小寫。([#26](https://github.com/polhem-dev/polhem/pull/26))
- `BackendComponents` 指定的自訂 `DefineAccess` 或 `DefineStorage` 改以 `ActivatorUtilities` 建立，建構子可以接收任何已註冊
  的服務，不再限於固定幾種簽章。([#26](https://github.com/polhem-dev/polhem/pull/26))
- Blazor 的 `FormPage` 預設透過定義載入器在地化定義（`PolhemBlazorOptions.UseDefinitionLoader`）；設為 `false` 則照原樣
  呈現定義。([#27](https://github.com/polhem-dev/polhem/pull/27))
- 巢狀 `<Categories>` 版面的 `ProgramSettings.xml` 不再被拒絕，而是載入成空的註冊表。Bee.NET 4.33.0 會拒絕這種檔案，
  所以只有從未在該版執行過的部署受影響：請先以 Bee.NET 的 `dotnet bee defines split-menu` 轉換該檔。
  ([#28](https://github.com/polhem-dev/polhem/pull/28))
- 隨附的 `AuditRule`、`Department`、`Employee` 版面依其 schema 重新產生（下拉選單、核取方塊與 lookup，取代文字框）。
  materialize 會略過已存在的檔案，因此已有這些版面的部署會保留舊版，直到自行覆寫或修改。
  ([#29](https://github.com/polhem-dev/polhem/pull/29))
- 產生的版面與 `Auto` 控制項類型，為 `DateTime` 欄位改用新的 `DateTimeEdit`，不再是 `DateEdit`。已儲存的版面維持
  `DateEdit`，它只編輯日期、會丟掉時間；時間有意義的欄位請改為 `DateTimeEdit`。
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

### 破壞性 API 變更

- 不是擴充點的公開類別改為 sealed：資料與定義型別、attribute、例外、快取、沒有掛勾的服務實作、資料庫方言與建構器，
  以及末端的 UI 控制項與 Blazor 元件。業務物件、repository 與集合基底類別、`TextEdit`、`DateEdit`、`ListView`、
  `FormView`、各 connector 與 `AuditRuleBusinessObject` 仍可繼承。`KeyCollectionBase<T>` 改為 abstract。
  ([#13](https://github.com/polhem-dev/polhem/pull/13))
- 改名：`IBeeContext` / `BeeContext` → `IBusinessObjectContext` / `BusinessObjectContext`；
  `BeeStringLocalizer<T>` → `LanguageResourceStringLocalizer<T>`；稽核記錄這一軸的 `LogBusinessObject`、
  `LogListResult`、`LogAggregateResult`、`LogApiConnector`、`LogListResponse`、`LogAggregateResponse`、
  `ILogListResponse`、`ILogAggregateResponse`、`LogActions` → `AuditLog…`；`PermissionAction` →
  `PermissionActions`；`NullAuditLogWriter` → `NullLogWriter`；`UserID` → `UserId`（參數 `userId` / `funcId`
  亦同）；`AuditEntry.AccessToken` → `TokenFingerprint`。
  ([#5](https://github.com/polhem-dev/polhem/pull/5), [#10](https://github.com/polhem-dev/polhem/pull/10),
  [#11](https://github.com/polhem-dev/polhem/pull/11))
- 搬移：`DeploymentAuthorizationService` 移到 `Polhem.Business.Security`、`EmployeeContextResolver` 移到
  `Polhem.Business.Session`、`ElementCapabilityResolver`、`IElementCapabilityResolver`、`FieldCapability` 移到
  `Polhem.Api.Client.Permissions`、`FileEndpointStorage` 移到 `Polhem.UI.Core`。
  ([#11](https://github.com/polhem-dev/polhem/pull/11), [#12](https://github.com/polhem-dev/polhem/pull/12))
- `LocalApiProvider` 與本機 connector 的建構子接收 `IServiceProvider`；移除 `ApiClientInfo.LocalServiceProvider`、
  `ApiClientInfo.ApiEncryptionKey`、`ApiClientInfo.UserTimeZoneId`（改用 `ApiSessionContext`）。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11))
- 用戶端與 UI 套件所有公開的非同步成員，以及 `JsonRpcExecutor.ExecuteAsync`，都多一個結尾的 `CancellationToken`；
  connector 的 action 方法皆為 virtual。`IUIViewService` 的實作，以及覆寫 `FormView`、`ListView` 受保護的
  `Resolve*Async` 掛勾者，簽章隨之改變。
  ([#12](https://github.com/polhem-dev/polhem/pull/12), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `IReplayWindowStore` 改為單一原子操作 `TryAcceptAsync`，因此可以實作多節點共用的儲存。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 自訂 payload codec 以 `ApiServiceOptions.RegisterPayloadCodec` 加入；未宣告時的預設仍是 MessagePack，不能替換。
  ([#10](https://github.com/polhem-dev/polhem/pull/10))
- 快取中依資料庫而來的型別（`CompanyInfo`、`DepartmentTree`、`ApiKeyInfo` 等）改為 init-only 屬性；session 的公司
  範圍改為單一不可變的 `SessionCompanyScope`。
  ([#8](https://github.com/polhem-dev/polhem/pull/8), [#10](https://github.com/polhem-dev/polhem/pull/10))
- `ICacheDataSourceProvider.GetCompanyAuditRules` 不再有預設實作。([#14](https://github.com/polhem-dev/polhem/pull/14))
- `PolhemLoginPanel` 的標籤參數與 `DynamicGrid.EmptyText` 改為 `string?`；`null` 顯示在地化文字。
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- wire：`CreateSessionRequest.userID` 改為 `userId`，並移除 `oneTime`；`LoginResponse` 新增 `culture`；稽核記錄的回應
  型別改名。`polhem-connector-js` 隨之更新。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11),
  [#15](https://github.com/polhem-dev/polhem/pull/15))
- `IFormRuleProcessor` 新增 `ApplyNewRowDefaults`；其他實作必須補上。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 可調整的上限與預設值由 `const` 改為 `static readonly`：`ApiKeyFormat.MinSysIdLength`、`ApiKeyFormat.MaxSysIdLength`、
  `LoginAttemptTracker.DefaultLockoutMinutes`、`DefaultMaxFailedAttempts`、`DefaultMaxTrackedAccounts`、
  `CurrencySettings.FallbackRounding`、`UnitSettings.FallbackDecimals`、`ApiKeyCache.AbsoluteMinutes`、`NegativeMinutes`、
  `RowEditPanel.CompactWidthThreshold`、`FormView.DefaultCompactWidthThreshold`。在常數運算式中使用它們的程式碼必須修改。
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `SystemApiConnector` 與 `AuditLogApiConnector` 的 `ExecuteAsync<T>` 改為 protected；`FormApiConnector.ExecuteAsync<T>`
  維持公開，供呼叫表單自己的 action。([#26](https://github.com/polhem-dev/polhem/pull/26))

### 移除

- 追蹤子系統（`Polhem.Base.Tracing`、`SysInfo.TraceListener`）。([#10](https://github.com/polhem-dev/polhem/pull/10))
- 序列化狀態：`IObjectSerialize`、`IObjectSerializeEmpty`、`SerializeState`、`SerializationUtilities`。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 相容性殘留：被忽略的建構子參數與多載、`JsonCodec` 的 `includeTypeName`、`TableSchemaBuilder.Compare` 與舊的結構
  比對路徑（`DbUpgradeAction`）、Hosting 各 factory 的建構子退路、一次性 session 旗標、同步的
  `JsonRpcExecutor.Execute`、`BusinessObject.SessionInfo`、`ClientInfo.ClientSettings`。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#12](https://github.com/polhem-dev/polhem/pull/12))
- 沒有呼叫端的型別與成員：`DateInterval`、`IPValidator`、`DataTableComparer`、`Dictionary<T>`、
  `DefaultBoTypeResolver`、`VersionInfo`、`SysInfo.IsToolMode`、`SysInfo.IsSingleFile` 與數個單純的包裝方法。
  ([#10](https://github.com/polhem-dev/polhem/pull/10))
- 改為 internal 的實作型別：Hosting 的背景服務、`NoEncryptionEncryptor`、`NoCompressionCompressor`、payload
  轉換器、`HttpUtilities`、`ILMapper<T>`、`XmlSerializerCache`、`ReplayWindow`、`BackendDefaultTypes`，以及框架
  repository 的實作（請用 `I*Repository` 介面）。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11),
  [#12](https://github.com/polhem-dev/polhem/pull/12))
- 未使用的設定型別 `ClientSettings`、`EndpointItem`、`EndpointItemCollection`。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- `StringHashSet` 與 `SysInfo.Version` 的公開 setter。([#26](https://github.com/polhem-dev/polhem/pull/26))
- `ProgramSettingsFormat` 與 `dotnet polhem defines split-menu`，它們用來轉換較早 Bee.NET 版本的巢狀 `ProgramSettings`
  版面。([#28](https://github.com/polhem-dev/polhem/pull/28))

### 新增

- `dotnet polhem keys protect`。([#10](https://github.com/polhem-dev/polhem/pull/10))
- connector 新增 `GetFormSchemaAsync`、`GetFormLayoutAsync`、`GetLanguageAsync`、`GetCommonConfigurationAsync`。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 在地化：`LanguageFallback`、`MenuLocalizer`、`FrameworkLanguageService`、`PolhemMessages`、`PolhemUIText`、
  `ILocalizableMessage`，以及 Avalonia 與 Blazor UI 文字的英文預設值與 `zh-TW` 翻譯。`FormView`、`ListView`、
  `LookupDialog` 預設透過 `ClientInfo.DefinitionLoader` 在地化定義。
  ([#15](https://github.com/polhem-dev/polhem/pull/15), [#22](https://github.com/polhem-dev/polhem/pull/22))
- `AuthenticationRequiredException`、可替換的服務 `IAuditLogSink`、`PagingOptions.MaxPageSize`、
  `DataRowExtensions.RewriteVersions`、`FormDataGuard.TryGetRowId`。
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6),
  [#8](https://github.com/polhem-dev/polhem/pull/8), [#14](https://github.com/polhem-dev/polhem/pull/14))
- ADR-046 記錄 1.0 的 API 政策。([#12](https://github.com/polhem-dev/polhem/pull/12))
- 伺服端與 UI head 共用的必填欄位規則 `RequiredFieldCheck` 與 `MissingRequiredField`，以及訊息
  `PolhemMessages.SaveFieldRequired`、`SaveDetailFieldRequired`、`PolhemUIText.RequiredFieldsEmpty`。
  ([#26](https://github.com/polhem-dev/polhem/pull/26), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `IDefineStorage.GetFormSchemaIds`、`FileUtilities.EnsureFileExists`。([#26](https://github.com/polhem-dev/polhem/pull/26))
- `PolhemBlazorOptions.UseDefinitionLoader`、`PolhemApiConnectorFactory.CreateDefinitionLoader`。
  ([#27](https://github.com/polhem-dev/polhem/pull/27))
- Avalonia 的 `FormDataObject.RowEditFieldChanged`。([#30](https://github.com/polhem-dev/polhem/pull/30))
- `ControlType.DateTimeEdit`，搭配 Avalonia 的 `DateTimeEdit` 編輯器與 Blazor 的 `datetime-local` 輸入框；
  `FormValueBinding.TryGetListItemText`、接收 `FormTable` 的 `GridControl.Bind` 多載，以及 `DynamicGrid.FormTable`。
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

### 效能

- 定義快取命中時，每個項目每秒最多檢查一次來源檔。([#14](https://github.com/polhem-dev/polhem/pull/14))
- Gzip 預設以最快等級壓縮；`AesCbcHmacCryptor` 改用 span API，輸出格式不變。
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- 每個請求的反射結果會快取；Avalonia 各 head 共用一個運算式求值器。([#14](https://github.com/polhem-dev/polhem/pull/14))
- 稽核批次寫入器以單一交易寫入一批。([#14](https://github.com/polhem-dev/polhem/pull/14))
- 經 MessagePack 傳輸的 `DataTable` 更小、序列化更快（見〈行為變更〉）。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))

### 平台支援

- Polhem 支援不修剪與部分修剪（partial trim）的建置。專案以 NativeAOT 發佈、以 `TrimMode=full` 修剪，或停用
  System.Text.Json 反射時，`POLHEM9004` 會發出警告（`PolhemSuppressTrimSupportWarning=true` 可關閉）。
  ([#17](https://github.com/polhem-dev/polhem/pull/17))
- `Polhem.Expressions` 隨附 ILLink descriptor，行動端預設的修剪會保留運算式用到的成員。
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- `LookupDialog` 與 `RowEditDialog` 只在桌面開原生視窗，其他平台改用覆蓋層（overlay host），因此 lookup 在 iOS、
  Android 與瀏覽器都能使用。([#7](https://github.com/polhem-dev/polhem/pull/7))
- MessagePack 值格式器不需動態程式碼即可處理 Polhem 列舉與巢狀的 `ParameterCollection`。
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- HTTP 用戶端在瀏覽器、Android 與 Apple 行動端 head 使用平台預設的處理器。
  ([#17](https://github.com/polhem-dev/polhem/pull/17))
- 超過兩個變數的運算式不再讓 iOS 與 Mac Catalyst 上的 App 終止：`DynamicExpressoEvaluator` 把每個運算式編譯成單一個以
  物件陣列為參數的委派，不需要動態程式碼。([#30](https://github.com/polhem-dev/polhem/pull/30))
- macOS 與 Linux 上的桌面 head 可以用 Local 模式連線：`FileUtilities.IsLocalPath` 接受目前作業系統上的完整路徑，
  不再只認 Windows 磁碟機與 UNC 路徑。([#29](https://github.com/polhem-dev/polhem/pull/29))
- 手機上的 lookup 與明細列編輯覆蓋層符合螢幕大小，避開安全區域與螢幕鍵盤，按鈕也留在捲動內容之外。
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

### 修正

- 帶值過濾條件的 Plain `GetList` 在 SQL 參數處失敗；用戶端把 Plain 的 `DataTable` 結果讀成空表。
  ([#9](https://github.com/polhem-dev/polhem/pull/9))
- 快取中的定義正在序列化時，並行的儲存與刪除可能失敗；與失效通知競爭的快取填入可能保留過期值。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 從用戶端儲存語系資源一律失敗。([#17](https://github.com/polhem-dev/polhem/pull/17))
- 隨附的 AuditRule 表單在沒有定義載入器時，模式下拉選單是空的。([#7](https://github.com/polhem-dev/polhem/pull/7))
- Blazor：登入時沒有設定 circuit 的時區，`FormDataObject` 沒有回到 circuit 的內容繼續執行。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- `SaveDatabaseSettings` 加密的是傳入執行個體本身的密碼，而不是副本。([#8](https://github.com/polhem-dev/polhem/pull/8))
- `KeyCollectionBase` payload 中的 nil 元素會被拒絕，不再略過。
  ([#9](https://github.com/polhem-dev/polhem/pull/9), [#14](https://github.com/polhem-dev/polhem/pull/14))
- 重建資料表（SQLite 上的任何欄位變更）時，未開啟 `UpgradeOptions.AllowColumnNarrowing` 也會縮小欄位。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 表單資料表未宣告 `DbTableName` 時，關聯欄位的 JOIN 使用空白的資料表名稱；現在改用 `TableName`。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- macOS 與 iOS 回報的文化是 `zh-Hant-TW`，原本找不到 `zh-TW` 資源；`LanguageFallback` 在 `zh-Hant` 之後接 `zh-TW`，
  在 `zh-Hans` 之後接 `zh-CN`。([#26](https://github.com/polhem-dev/polhem/pull/26))
- 明細列屬於此次儲存未包含的記錄而被拒絕時，訊息沒有在地化（`PolhemMessages.PermissionDetailOutOfScope`）。
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `FormView` 把權限能力套用在主機指定的 `Layout` 本身，而不是副本。([#27](https://github.com/polhem-dev/polhem/pull/27))
- Avalonia 手機寬度的卡片清單在日期上顯示時間部分，也不理會數值格式；現在與表格以相同方式格式化。
  ([#29](https://github.com/polhem-dev/polhem/pull/29))
- 照 Blazor Server 的 README 以 Remote 模式執行會得到 401；README 與 `UseRemoteProvider` 現在寫明必須設定
  `ApiClientInfo.ApiKey`。([#29](https://github.com/polhem-dev/polhem/pull/29))
- Avalonia 的明細列編輯覆蓋層要到確認該列才重算計算欄位；現在編輯時即重算，按取消則還原。
  ([#30](https://github.com/polhem-dev/polhem/pull/30))
- 清單（Avalonia 與 Blazor 的表格、卡片清單、lookup 與明細表格）顯示下拉欄位儲存的值；現在顯示在地化的清單項目文字，
  布林值在卡片清單顯示核取方塊，其他地方顯示在地化的是／否。([#33](https://github.com/polhem-dev/polhem/pull/33))
- Blazor 的 `DateEdit` 遇到帶時間的值時顯示空白；現在顯示日期。([#33](https://github.com/polhem-dev/polhem/pull/33))

### 範例與工具

- 範例的業務資料表放在公司範圍，登入後進入一家示範公司。([#23](https://github.com/polhem-dev/polhem/pull/23))
- Northwind 新增 zh-TW 示範帳號（`demo-tw`），README 的截圖改放在 repo 內。
  ([#23](https://github.com/polhem-dev/polhem/pull/23))
- DefineEditor 在 Windows 與 Linux 的視窗內顯示檔案選單。([#23](https://github.com/polhem-dev/polhem/pull/23))
- 範例的 `Employee` 與 `Department` 表單改名為 `Staff`（`ft_staff`、`ft_staff_phone`）與 `Team`（`ft_team`），不再取代
  框架同名的保留表單。([#25](https://github.com/polhem-dev/polhem/pull/25))
- Northwind 隨附訂單規則的 `zh-TW` 訊息。([#25](https://github.com/polhem-dev/polhem/pull/25))

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.5.0...HEAD
[1.5.0]: https://github.com/polhem-dev/polhem/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/polhem-dev/polhem/compare/v1.3.1...v1.4.0
[1.3.1]: https://github.com/polhem-dev/polhem/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.3.0
[1.2.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.2.0
[1.1.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
