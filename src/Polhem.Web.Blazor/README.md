# Polhem.Web.Blazor

> The shared layer of the Polhem Blazor head: form data objects, field editor bases and the user context.

[繁體中文](README.zh-TW.md)

An application does not use this package alone. It references one host package, such as
[`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.md), which brings this one in. The reasons for the split are in
[ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md).

## Contents

- `FormDataObject` and `FormLiveComputation` (`Polhem.Web.Blazor.DataObjects`).
- `FieldEditorBase`, `IFieldEditor` and `FormScope` (`Polhem.Web.Blazor.Controls.Editors`), the base of every field editor.
- `TextEditorBase`, `NumericEditorBase`, `CheckEditorBase`, `DropDownEditorBase`, `DateEditorBase`, `TimeEditorBase` and `DateTimeEditorBase`, the value and commit rules of each editor kind. A UI package supplies the markup.
- `FormPageBase` (`Polhem.Web.Blazor.Components`), the state and commands of a form page; `BlazorDefinitions`, `PolhemBlazorText` and `IBlazorDefinitionOptions`, which a UI package loads definitions and text through.
- `GridControlBase`, `GridEditMode` and `GridEditorHost` (`Polhem.Web.Blazor.Controls`), the state and commands of a grid, and keeping one editor open in it.
- `BlazorUserContext` (`Polhem.Web.Blazor.Identity`), the signed-in user's context for one scope.

## Dependency Constraints

This package references no UI framework and no ASP.NET Core host, so it can load in WebAssembly. `POLHEM9001` enforces it.

## License

MIT
