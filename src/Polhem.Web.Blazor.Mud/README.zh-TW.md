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
- `PolhemMudProviders` 與 `PolhemMudTheme`（`Polhem.Web.Blazor.Mud.Theming`），MudBlazor 的 provider 與預設佈景。
- `AddPolhemMud`（`Polhem.Web.Blazor.Mud.DependencyInjection`），註冊 MudBlazor 的服務。

## 使用方式

呼叫 `AddPolhemMud`，在版面放一個 `PolhemMudProviders`，並在宿主頁面加入：

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Mud/polhem-mud.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

桌面畫面以帶 `polhem-dense` class 的元素包住，就會使用高密度控制項。手機尺寸的畫面不要套用，否則控制項會太小。
