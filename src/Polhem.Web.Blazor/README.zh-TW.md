# Polhem.Web.Blazor

> Polhem Blazor head 的共用層：表單資料物件、欄位編輯器基底與使用者內容。

[English](README.md)

應用程式不會單獨使用這個套件，而是參考一個宿主套件（例如 [`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.zh-TW.md)），由它帶入本套件。拆分的理由見 [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md)。

## 內容

- `FormLiveComputation`（`Polhem.Web.Blazor.DataObjects`）。元件綁定的是各 head 共用、位於
  [`Polhem.UI.Core`](../Polhem.UI.Core/README.zh-TW.md) 的 `FormDataObject`。
- `FieldEditorBase` 與 `FormScope`（`Polhem.Web.Blazor.Controls.Editors`），所有欄位編輯器的基底，實作 `Polhem.UI.Core.Editing`
  的 `IFieldEditor`。
- `TextEditorBase`、`NumericEditorBase`、`CheckEditorBase`、`DropDownEditorBase`、`DateEditorBase`、`TimeEditorBase` 與 `DateTimeEditorBase`，各類編輯器的取值與提交規則；markup 由 UI 套件提供。
- `ButtonEditorBase`、`LookupPanelBase` 與 `LookupDialogBase`（`Polhem.Web.Blazor.Controls.Editors`），查詢流程。
- `RowEditPanelBase`，編輯表單的工作階段。
- `FormPageBase`（`Polhem.Web.Blazor.Components`），表單頁的狀態與命令；`BlazorDefinitions`、`PolhemBlazorText` 與 `IBlazorDefinitionOptions`，UI 套件透過它們載入定義與文字。
- `FormLayoutPresenterBase`（`Polhem.Web.Blazor.Controls`），依 `FormLayout` 呈現紀錄的規則。
- `RelatedRecordDialogBase`、`LoginPanelBase` 與 `CompactViewportObserver`（`Polhem.Web.Blazor.Components`）。
- `PolhemShellOptions`、`PolhemShellHome`、`PolhemShellSignInLinks`、`ShellLayoutBase`、`ShellNavMenuBase`、`ShellSignInBase` 與 `IShellLanguageSwitcher`（`Polhem.Web.Blazor.Shell`），外殼的狀態；語系切換由宿主套件提供。
- `GridControlBase` 與 `GridEditorHost`（`Polhem.Web.Blazor.Controls`），Grid 的狀態與命令，以及讓 Grid 只開一個編輯器；
  編輯模型是 `Polhem.UI.Core.Editing` 的 `GridEditMode`。
- `BlazorUserContext`（`Polhem.Web.Blazor.Identity`），單一 scope 內已登入使用者的內容。

## 資料物件與使用者脈絡

- `BlazorUserContext` -- 單一 circuit 的登入使用者、進入的公司與能力。host 以 `EnterCompanyAsync` 進入公司、以 `SignOut`
  登出，讓 context 與 client 保持一致。直接透過 client 進入公司的話，context 沒有能力快照，頁面會把使用者視為不受限。
- `FormDataObject`（`Polhem.UI.Core.DataObjects`）-- 頁面以 circuit 的 client 所建立的連接器，以及 circuit 自己的時區
  （`Client.Session.Credentials.UserTimeZoneId`）建構它，新資料列的日期預設值依此決定。自訂頁面也照做；不可傳整個
  process 共用的時區。
- `FormLiveComputation` -- 重算表單的計算欄位並評估新資料列的預設值運算式，規則與 Avalonia 端相同（`Polhem.UI.Core`
  的 `FormLiveComputationEngine`）。自訂頁面訂閱 `FieldValueChanged` 並呼叫 `Recompute`；時區要傳 circuit 自己的，
  不可用整個 process 共用的。

## 相依限制

本套件不參考任何 UI 框架或 ASP.NET Core 宿主，因此可以在 WebAssembly 載入。由 `POLHEM9001` 把關。

它參考 `Polhem.Api.Client`、`Polhem.Expressions` 與 [`Polhem.UI.Core`](../Polhem.UI.Core/README.zh-TW.md)；後者提供各 head
共用的表單值規則（`FormValueBinding`、`FormDataGuard`、元素能力解析器）。`Polhem.UI.Core` 的行程層級部分不可使用：Blazor
Server 網站在同一行程服務多位使用者，因此任何 Blazor 套件都不得使用 `UIServices`，也不得參考 `Polhem.UI.ApiBridge`，兩者都在
每個行程只持有一位使用者。`UIServicesUsageGateTests`（`tests/Polhem.Web.Blazor.Server.UnitTests`）會讀取共用層、宿主與 UI
套件的參考，違反時測試失敗。見 [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)。

## 授權

MIT
