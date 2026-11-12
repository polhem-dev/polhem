# Polhem.Web.Blazor.Fluent

> Polhem Blazor head 的 Fluent UI UI 套件。應用程式與宿主套件（例如 [`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.zh-TW.md)）一起參考它。

[English](README.md)

分層方式見 [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md)，這個套件的做法與理由見 [ADR-061](../../maintainers/adr/adr-061-fluent-ui-second-ui-package.md)。

## 內容

型別名稱與 [`Polhem.Web.Blazor.Mud`](../Polhem.Web.Blazor.Mud/README.zh-TW.md) 相同，位於 `Polhem.Web.Blazor.Fluent` 底下，行為也相同，因為規則都在 `Polhem.Web.Blazor` 的基底裡。元件說明請看該 README。由定義產生的頁面，只要換套件和 `@using` 就能在兩者之間切換；直接用 Fluent UI 元件寫的頁面則帶不過去。

`PolhemFluentProviders` 對應 `PolhemMudProviders`，`AddPolhemFluent` 對應 `AddPolhemMud`。

## 使用方式

呼叫 `AddPolhemFluent`；除非使用外殼的版面，否則在版面放一個 `PolhemFluentProviders`，並在宿主頁面加入：

```html
<link href="_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css" rel="stylesheet" />
<link href="_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Fluent/polhem-fluent.css" rel="stylesheet" />
```

本套件鎖定 Fluent UI 的精確版本，請勿參考其他版本。

## 與 MudBlazor 套件的差異

- 登入、欄位篩選和查詢搜尋使用原生輸入框，並調整樣式以求一致。原因見 ADR。
- 對話框渲染在放置的位置，而不是 provider 裡。它們看不到外層的 `FormScope`。
- 日期以儲存格式 `yyyy-MM-dd` 顯示，年月為 `yyyy-MM`。
- 欄位要等表格的腳本載入後才能拖曳，也就是頁面載入後稍等一下。
- 欄位篩選按鈕使用文字符號，不用圖示；工具列按鈕是圖示。

Northwind 在兩種宿主上都使用本套件：Blazor Server 是 `Polhem.Northwind.BlazorFluent`，Blazor WebAssembly 是 `Polhem.Northwind.BlazorFluentWasm`。
