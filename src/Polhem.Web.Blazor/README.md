# Polhem.Web.Blazor

> The shared layer of the Polhem Blazor head: form data objects, field editor bases and the user context.

[繁體中文](README.zh-TW.md)

An application does not use this package alone. It references one host package, such as
[`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.md), which brings this one in. The reasons for the split are in
[ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md).

## Contents

- `FormLiveComputation` (`Polhem.Web.Blazor.DataObjects`). The components bind the `FormDataObject` of
  [`Polhem.UI.Core`](../Polhem.UI.Core/README.md), which every head shares.
- `FieldEditorBase` and `FormScope` (`Polhem.Web.Blazor.Controls.Editors`), the base of every field editor, which implements
  `IFieldEditor` from `Polhem.UI.Core.Editing`.
- `TextEditorBase`, `NumericEditorBase`, `CheckEditorBase`, `DropDownEditorBase`, `DateEditorBase`, `TimeEditorBase` and `DateTimeEditorBase`, the value and commit rules of each editor kind. A UI package supplies the markup.
- `ButtonEditorBase`, `LookupPanelBase` and `LookupDialogBase` (`Polhem.Web.Blazor.Controls.Editors`), the lookup flow.
- `RowEditPanelBase`, the edit form's session.
- `FormPageBase` (`Polhem.Web.Blazor.Components`), the state and commands of a form page; `BlazorDefinitions`, `PolhemBlazorText` and `IBlazorDefinitionOptions`, which a UI package loads definitions and text through.
- `GridControlBase` and `GridEditorHost` (`Polhem.Web.Blazor.Controls`), the state and commands of a grid, and keeping one
  editor open in it; its editing model is `GridEditMode` from `Polhem.UI.Core.Editing`.
- `FormLayoutPresenterBase` (`Polhem.Web.Blazor.Controls`), the rules of a record rendered from a `FormLayout`.
- `RelatedRecordDialogBase`, `LoginPanelBase` and `CompactViewportObserver` (`Polhem.Web.Blazor.Components`).
- `PolhemShellOptions`, `PolhemShellHome`, `PolhemShellSignInLinks`, `ShellLayoutBase`, `ShellNavMenuBase`, `ShellSignInBase` and `IShellLanguageSwitcher` (`Polhem.Web.Blazor.Shell`), the shell's state; a host package supplies the language switch.
- `BlazorUserContext` (`Polhem.Web.Blazor.Identity`), the signed-in user's context for one scope.

## Data objects and the user context

- `BlazorUserContext` -- the signed-in user, entered company and capabilities of one circuit. The host enters the
  company with `EnterCompanyAsync` and signs out with `SignOut`, which keep the context and the client in step.
  Entering the company through the client directly leaves the context without capabilities, so the pages treat the
  user as unrestricted.
- `FormDataObject` (`Polhem.UI.Core.DataObjects`) -- the pages construct it with a connector of the circuit's
  client and the circuit's time zone (`Client.Session.Credentials.UserTimeZoneId`), which a new row's dates default
  to. A page of its own does the same; never pass a process-wide time zone.
- `FormLiveComputation` -- recomputes a form's computed fields and evaluates new-row default expressions, with the
  same rules as the Avalonia head (`FormLiveComputationEngine` in `Polhem.UI.Core`). A page of its own subscribes
  `FieldValueChanged` and calls `Recompute`; pass the circuit's time zone, never a process-wide one.

## Dependency Constraints

This package references no UI framework and no ASP.NET Core host, so it can load in WebAssembly. `POLHEM9001` enforces it.

It references `Polhem.Api.Client`, `Polhem.Expressions` and [`Polhem.UI.Core`](../Polhem.UI.Core/README.md), which
supplies the form value rules every head shares (`FormValueBinding`, `FormDataGuard`, the element capability
resolver) among the rest of its contracts and rules. Its process-wide parts are off limits: a Blazor Server site serves
several users from one process, so the Blazor packages must not use `UIServices` or reference `Polhem.UI.ApiBridge`,
both of which hold one user per process. `UIServicesUsageGateTests` (`tests/Polhem.Web.Blazor.Server.UnitTests`)
reads the references of this package, the Server host and both UI packages and fails when one does. The WebAssembly
host is not in that scan: `POLHEM9001` keeps `Polhem.UI.ApiBridge` out of it, and nothing checks its use of
`UIServices`. See
[ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md).

## License

MIT
