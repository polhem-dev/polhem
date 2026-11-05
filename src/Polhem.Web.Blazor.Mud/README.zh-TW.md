# Polhem.Web.Blazor.Mud

> Polhem Blazor head 的 MudBlazor UI 套件。應用程式與宿主套件（例如 [`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.zh-TW.md)）一起參考它。

[English](README.md)

分層方式見 [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md)。

## 內容

- `FieldEditorFactory` 與各欄位編輯器（`Polhem.Web.Blazor.Mud.Controls.Editors`），建立在 `Polhem.Web.Blazor` 的編輯器基底上。
- 同一命名空間的 `ButtonEdit`、`LookupPanel` 與 `LookupDialog`，查詢編輯器與它的選取對話框。
- `GridControl`（`Polhem.Web.Blazor.Mud.Controls`），依版面定義繪製的 `MudDataGrid`。`InCell` 模式下，格子平時顯示文字，取得焦點才開編輯器，一張 Grid 同時只開一個。在單行編輯器按 Enter 會寫回內容、格子回到文字；按 Esc 會放棄輸入的內容，格子回到文字。列表 Grid 接受 `OnSortRequested` 與 `SortFieldName`：它請宿主依新順序讀取資料，自己不排序。
- `RowEditPanel` 與 `RowEditDialog`，`EditForm` 模式的編輯表單。
- `FormLayoutPresenter`（`Polhem.Web.Blazor.Mud.Controls`），依 `FormLayout` 排列紀錄。欄位維持 CSS Grid，因為 `MudGrid` 做不到 `RowSpan`，也處理不了除不盡 12 的 `ColumnCount`。
- `FormPage` 與 `RelatedRecordDialog`（`Polhem.Web.Blazor.Mud.Components`），建立在 `Polhem.Web.Blazor` 的基底上；`ConfirmDialog` 在刪除前詢問，焦點預設在取消鈕。
- `FormPage` 把 `ListFilterState` 傳給清單的 `GridControl`，每個欄位的篩選由 `GridColumnFilter` 提供。自行使用 `GridControl` 時，設定 `Filters` 與 `OnFilterChanged`，並自行重新讀取資料。
- 在寬螢幕上，`GridControl` 的欄位可以用滑鼠調整寬度與順序，調整結果不會被記住。
- 在窄 viewport 或設定 `IsCompact` 時，`FormPage` 以卡片顯示清單，捲動時讀取下一頁，最多 1000 筆。每張卡片都會保持渲染，所以捲得越長，circuit 的記憶體與傳輸量越高。
- `PolhemLoginPanel`（`Polhem.Web.Blazor.Mud.Components`），登入表單。
- `PolhemShellLayout`、`PolhemShellNavMenu` 與 `PolhemShellSignIn`（`Polhem.Web.Blazor.Mud.Shell`），建立在 `MudLayout` 上的外殼。版面會自行放置 `PolhemMudProviders`。選項由宿主套件註冊（Blazor Server 用 `AddPolhemShell`）。
- `PolhemMudProviders` 與 `PolhemMudTheme`（`Polhem.Web.Blazor.Mud.Theming`），MudBlazor 的 provider 與預設佈景。
- `AddPolhemMud`（`Polhem.Web.Blazor.Mud.DependencyInjection`），註冊 MudBlazor 的服務。

## 元件

這裡的編輯器、Grid 與頁面在每個 UI 套件都相同：規則在 `Polhem.Web.Blazor` 的基底，`tests/Polhem.Web.Blazor.BehaviorTests` 讓各套件遵守同一組行為。

- `FormPage` -- 單一程式的清單（逐頁顯示）與記錄，一次顯示其中之一，與 Avalonia 的 `ListView`、`FormView` 相同：清單有 View、New、Edit、
  Delete（點選選取資料列、雙擊以檢視開啟），檢視中的記錄有 Back，編輯中的記錄有 Save 與 Cancel；儲存成功後回到重新載入的清單。Delete 會先詢問，除非 `ConfirmDelete` 參數為 `false`。預設經由 `FormDefinitionLoader`
  載入定義，因此標題依 circuit 的 UI culture 呈現，租戶客製的版面也會套用；`DefinitionLoader` 參數可覆寫單一頁面的
  載入器。儲存前會檢查標記為 `Required` 的欄位，有空白時列出這些欄位，不送出儲存。使用者編輯時透過 `FormLiveComputation`
  即時重算計算欄位（`ValueExpression`）；部署了幣別與單位主檔時，數值預覽會依登入公司的小數位數與這些主檔決定，儲存時由
  伺服器做權威的四捨五入。記錄的明細資料表以 grid 呈現在主檔欄位下方。viewport 寬度小於 `CompactWidthThreshold`（預設 600 CSS
  像素）時，表單改用精簡版面，寬度由 `CompactViewportObserver` 量測；設定 `IsCompact` 則固定版面、不量測，
  `DetailEditMode` 決定非精簡版面時的明細編輯模式。`ProgId` 變更時頁面會重新載入，所以一個路由頁面（`/form/{ProgId}`）
  可以服務所有程式。載入記錄、儲存或刪除失敗時，訊息以提示顯示在表單上方，使用者輸入的內容保留；只有無法載入定義的頁面
  才以錯誤取代表單。circuit 的 `IUserContext` 有能力快照時，使用者缺少表單權限模型的 `Read`、`Create`、`Update`、
  `Delete` 時分別隱藏 View、New、Edit、Delete，`Create` 與 `Update` 都沒有時隱藏 Save，敏感欄位依其類別隱藏或唯讀，經由 `Polhem.UI.Core` 的 `LayoutCapabilityApplier`；
  數值預覽採用登入公司的小數位數。後端仍會檢查每一個呼叫。
- `CompactViewportObserver` -- 不輸出任何標記，瀏覽器 viewport 跨過 `CompactWidthThreshold` 時觸發 `CompactChanged`。
  直接使用 `FormLayoutPresenter` 的頁面在旁邊放一個，把值傳給 `IsCompact`。它透過套件內附的一支小型 JavaScript module
  量測 viewport，以 JavaScript isolation 載入，host 頁面不必加 `<script>`；host 必須提供 static web assets
  （`UseStaticFiles` 或 `MapStaticAssets`）。prerender 的頁面先以寬版呈現，circuit 連上後才切換。
- `FormLayoutPresenter` -- 渲染 `FormLayout` 的主檔 section，每個欄位一個欄位編輯器，由 `FieldEditorFactory` 依
  `ControlType` 選擇。`FormMode` 參數決定哪些編輯器可輸入，並依各欄位的 `ReadOnly` 與 `AllowEditModes`。
  版面的每個明細資料表接著以 `GridControl` 呈現，編輯模式依 `DetailEditMode`。`IsCompact` 為手機寬度呈現：主檔欄位
  不論版面的 `ColumnCount` 都排成一欄，明細 grid 改用編輯表單。
- `GridControl` -- 呈現 `FormDataObject` 的明細資料表，或 list 模式下由呼叫端提供的 `DataTable`，編輯規則與 Avalonia 的
  `GridControl` 相同：表單模式與版面的 `AllowEditModes`、`AllowActions` 允許時顯示 Add / Edit / Delete 工具列；
  `EditMode` 為 `InCell`（每個可編輯的儲存格顯示欄位編輯器）或 `EditForm`（在 `RowEditDialog` 編輯資料列，它是包著
  `RowEditPanel` 的強制回應視窗，按 OK 前都只暫存編輯）。儲存格文字依共用的 `GridCellFormatter`；唯讀欄位的表頭加括號，
  必填欄位的表頭會顯示必填標示。
- 欄位編輯器（`Polhem.Web.Blazor.Mud.Controls.Editors`）-- `TextEdit`、`MemoEdit`、`NumericEdit`、`TimeEdit`、
  `DateTimeEdit`、`DateEdit`、`YearMonthEdit`、`CheckEdit`、`DropDownEdit`、`ButtonEdit`，名稱、契約（`IFieldEditor`）與行為
  都和 `Polhem.UI.Avalonia` 的欄位編輯器相同。編輯器可透過 `DataObject` 參數、外層的 `FormScope` 或明確呼叫
  `Bind` 綁定 `FormDataObject`；它自行套用 schema 欄位的中繼資料（例如 `MaxLength`、`ListItems`），其他來源寫入
  該欄位時會自動更新。新的編輯器繼承 `FieldEditorBase`。
- 查詢 -- 綁定到有 `RelationProgId` 欄位的 `ButtonEdit` 會顯示該欄位的顯示欄位，按鈕開啟 `LookupDialog`（包著
  `LookupPanel` 的強制回應視窗：搜尋、清單、OK / Cancel），把選中資料列的 id 與對應欄位透過
  `FormDataObject.ApplyLookupSelection` 寫回，按 Delete 則清除。對話框依序從參數、容器中註冊的
  `IFormDefinitionSource` 與 `IFormApiConnectorFactory`（以 scoped 註冊，每個 circuit 各一份）、circuit 的
  `PolhemApiClient` 取得目標 schema 與連接器；有 Polhem 後端的應用程式不需註冊任何東西。
- 關聯資料 -- 唯讀表單上有值的 `ButtonEdit` 不顯示按鈕，改顯示手形游標與提示；雙擊時把它指向的資料傳給
  `FormLayoutPresenter.OpenRelation`；明細 `GridControl` 的關聯儲存格也一樣。`FormPage` 以 `RelatedRecordDialog` 處理：使用者對關聯表單的權限模型有 `Read`
  權限時以唯讀顯示那筆資料，否則由頁面在提示中指出該表單；伺服器端由關聯表單的 `GetData` 再檢查一次。共用規則是
  `Polhem.UI.Core` 的 `RelationOpenRules`。
- `PolhemLoginPanel` -- 精簡的登入表單；`OnLoggedIn` 收到 `LoginResponse`。登入成功時也會記錄到 circuit 的
  `BlazorUserContext`。`InitialUserId` 與 `InitialPassword` 在面板初始化時填入輸入框一次。

## 使用方式

呼叫 `AddPolhemMud`，在版面放一個 `PolhemMudProviders`（使用外殼的版面時不用放），並在宿主頁面加入：

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Mud/polhem-mud.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

本套件鎖定 MudBlazor 的確切版本，請勿參考其他版本。要調整外觀，把 `MudTheme` 傳給 `PolhemMudProviders`。應用程式直接用 MudBlazor 元件寫的頁面，換 UI 套件時不會帶過去；定義驅動的頁面會。選型理由見 [ADR-060](../../maintainers/adr/adr-060-mudblazor-first-ui-package.md)。

桌面畫面以帶 `polhem-dense` class 的元素包住，就會使用高密度控制項。手機尺寸的畫面不要套用，否則控制項會太小。
