# Polhem.Web.Blazor.Mud

> Polhem Blazor head 的 MudBlazor UI 套件。仍在開發中，尚未發佈。

[English](README.md)

分層方式見 [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md)。

## 內容

- `FieldEditorFactory` 與各欄位編輯器（`Polhem.Web.Blazor.Mud.Controls.Editors`），建立在 `Polhem.Web.Blazor` 的編輯器基底上。
- 同一命名空間的 `ButtonEdit`、`LookupPanel` 與 `LookupDialog`，查詢編輯器與它的選取對話框。
- `GridControl`（`Polhem.Web.Blazor.Mud.Controls`），依版面定義繪製的 `MudDataGrid`。`InCell` 模式下，格子平時顯示文字，取得焦點才開編輯器，一張 Grid 同時只開一個。在編輯器按 Esc 會放棄輸入的內容，格子回到文字。列表 Grid 接受 `OnSortRequested` 與 `SortFieldName`：它請宿主依新順序讀取資料，自己不排序。
- `RowEditPanel` 與 `RowEditDialog`，`EditForm` 模式的編輯表單。
- `FormLayoutPresenter`（`Polhem.Web.Blazor.Mud.Controls`），依 `FormLayout` 排列紀錄。欄位維持 CSS Grid，因為 `MudGrid` 做不到 `RowSpan`，也處理不了除不盡 12 的 `ColumnCount`。
- `FormPage` 與 `RelatedRecordDialog`（`Polhem.Web.Blazor.Mud.Components`），建立在 `Polhem.Web.Blazor` 的基底上；`ConfirmDialog` 在刪除前詢問，焦點預設在取消鈕。
- 在窄 viewport 或設定 `IsCompact` 時，`FormPage` 以卡片顯示清單，捲動時讀取下一頁，最多 5000 筆。每張卡片都會保持渲染，所以捲得越長，circuit 的記憶體與傳輸量越高。
- `PolhemLoginPanel`（`Polhem.Web.Blazor.Mud.Components`），登入表單。
- `PolhemShellLayout`、`PolhemShellNavMenu` 與 `PolhemShellSignIn`（`Polhem.Web.Blazor.Mud.Shell`），建立在 `MudLayout` 上的外殼。版面會自行放置 `PolhemMudProviders`。選項由宿主套件註冊（Blazor Server 用 `AddPolhemShell`）。
- `PolhemMudProviders` 與 `PolhemMudTheme`（`Polhem.Web.Blazor.Mud.Theming`），MudBlazor 的 provider 與預設佈景。
- `AddPolhemMud`（`Polhem.Web.Blazor.Mud.DependencyInjection`），註冊 MudBlazor 的服務。

## 使用方式

呼叫 `AddPolhemMud`，在版面放一個 `PolhemMudProviders`（使用外殼的版面時不用放），並在宿主頁面加入：

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Mud/polhem-mud.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

桌面畫面以帶 `polhem-dense` class 的元素包住，就會使用高密度控制項。手機尺寸的畫面不要套用，否則控制項會太小。
