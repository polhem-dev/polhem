# Polhem.Web.Blazor.Mud

> Polhem Blazor head 的 MudBlazor UI 套件。仍在開發中，尚未發佈。

[English](README.md)

分層方式見 [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md)。

## 內容

- `FieldEditorFactory` 與各欄位編輯器（`Polhem.Web.Blazor.Mud.Controls.Editors`），建立在 `Polhem.Web.Blazor` 的編輯器基底上。
- 同一命名空間的 `ButtonEdit`、`LookupPanel` 與 `LookupDialog`，查詢編輯器與它的選取對話框。
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
