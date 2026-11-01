# Polhem.Web.Blazor

> Polhem Blazor head 的共用層：表單資料物件、欄位編輯器基底與使用者內容。

[English](README.md)

應用程式不會單獨使用這個套件，而是參考一個宿主套件（例如 [`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.zh-TW.md)），由它帶入本套件。拆分的理由見 [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md)。

## 內容

- `FormDataObject` 與 `FormLiveComputation`（`Polhem.Web.Blazor.DataObjects`）。
- `FieldEditorBase`、`IFieldEditor` 與 `FormScope`（`Polhem.Web.Blazor.Controls.Editors`），所有欄位編輯器的基底。
- `TextEditorBase`、`NumericEditorBase`、`CheckEditorBase`、`DropDownEditorBase`、`DateEditorBase`、`TimeEditorBase` 與 `DateTimeEditorBase`，各類編輯器的取值與提交規則；markup 由 UI 套件提供。
- `FormPageBase`（`Polhem.Web.Blazor.Components`），表單頁的狀態與命令；`BlazorDefinitions`、`PolhemBlazorText` 與 `IBlazorDefinitionOptions`，UI 套件透過它們載入定義與文字。
- `GridControlBase`、`GridEditMode` 與 `GridEditorHost`（`Polhem.Web.Blazor.Controls`），Grid 的狀態與命令，以及讓 Grid 只開一個編輯器。
- `BlazorUserContext`（`Polhem.Web.Blazor.Identity`），單一 scope 內已登入使用者的內容。

## 相依限制

本套件不參考任何 UI 框架或 ASP.NET Core 宿主，因此可以在 WebAssembly 載入。由 `POLHEM9001` 把關。

## 授權

MIT
