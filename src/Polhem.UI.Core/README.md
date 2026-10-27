# Polhem.UI.Core

> Shared front-end layer for every Polhem front end: the service contracts a UI consumes, the form value rules every head shares, and the permission capability resolver. No transport.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: UI Layer (shared front-end foundation)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Every front end references it, the Blazor head (`Polhem.Web.Blazor.Server`) included. It reaches the backend only through the contracts in `Polhem.Api.Contracts`; it references neither `Polhem.Api.Client` nor `Polhem.Api.Core`. The reasons are in [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md).
- Connecting a single-user head to a Polhem backend is the job of [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.md), which implements the contracts below over the API client.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Overview

`Polhem.UI.Core` is the framework-agnostic base every front end builds on. It declares what a UI needs from its
surroundings (the definitions of a form, the signed-in user's context, a connector per program) as contracts, and
holds the rules for a form's values that must be the same in every head. It carries no UI-framework types and no
transport: a UI built on it works against a Polhem backend through `Polhem.UI.ApiBridge`, or against the
application's own data through its own implementation of `IFormApiConnector`.

## Key Types

### Service Locator for Controls Created from Markup

- `UIServices` -- the process-wide `IServiceProvider` (`Provider`) that controls created by a XAML loader resolve their
  services from, with `Get<T>()` returning `null` when no provider is set or the service is not registered. A
  single-user head sets it once at startup; with nothing registered the controls run without a backend. A host that
  serves several users from one process must not use it.

### Front-End Service Contracts

- `IUserContext` -- the signed-in user as a front end sees it: `User`, `Capabilities` (per permission model),
  `Company` and the deployment's `DefaultLanguage`.
- `Definitions/IFormDefinitionSource` -- supplies the form schema, the layout and the currency and unit masters a form
  view is built from. The implementation decides the language, and every instance it returns belongs to the caller.
- `Definitions/StorageDefinitionSource` -- an `IFormDefinitionSource` over an `IDefineStorage` (for example
  `FileDefineStorage` over a local folder), for a front end without a Polhem backend. It returns the definitions as
  stored, with no language resources and no tenant customization.
- The connector contract itself, `IFormApiConnector` / `IFormApiConnectorFactory`, is in `Polhem.Api.Contracts`
  (namespace `Polhem.Api.Contracts.Form`), so the API client can implement it without referencing the UI.

### Results for a Connector of Your Own

- `Connectors/FormListResult`, `FormDataResult`, `FormDeleteResult` -- simple records implementing the response
  interfaces of `Polhem.Api.Contracts.Form`, for an application that implements `IFormApiConnector` over its own data
  instead of a Polhem backend.

### Form Value Rules Shared by Every Head

- `FormValueBinding` -- converts between the values in a form's `DataSet` and the strings UI bindings exchange, and
  builds the empty `DataSet` a form starts from.
- `FormDataGuard` -- the preconditions a form's CRUD calls check before they reach the connector, with one wording of
  each failure for every head.
- `FormLiveComputationEngine` -- the live recomputation of computed fields and new-row default expressions that
  every head drives. It takes the expression evaluator (`Polhem.Core.Expressions.IExpressionEvaluator`) and the
  user's time zone from its caller, so this package carries neither an expression engine nor process-wide state.

### Field Editor Rules Shared by Every Head

`Fields/` holds the rules the field editors of `Polhem.UI.Avalonia` and `Polhem.Web.Blazor.Server` both use, so the
two heads behave the same:

- `FieldEditRules` -- whether a field accepts input, from the form mode, the layout read-only flag and
  `AllowEditModes`.
- `FieldValueFormatter` -- the display of a value in the user's culture, for form fields and grid cells.
- `NumericFieldFormat` -- number format resolution (currency and unit decimals included), display, input parsing and
  the value written back.
- `DateTimeFieldFormat` -- the display, input and binding-string rules of a date-and-time field.
- `GridCellFormatter` -- the text a grid cell shows: list item text, instants, Booleans, per-row currency and unit
  decimals, and the composed display fields of a lookup column.
- `FieldCaptionRules` -- the caption convention: a read-only grid header is parenthesised, a required caption is
  highlighted, and read-only wins.
- `LookupFieldRules`, `LookupDisplay` -- whether a field looks up, which program it opens, which fields it shows and
  how they are joined, and how a selection is written back or cleared.

### Permission Capabilities

- `Permissions/IElementCapabilityResolver`, `ElementCapabilityResolver`, `FieldCapability` -- turn the user's
  per-model capabilities into element-level decisions (whether a field is visible or read-only).
- `Permissions/LayoutCapabilityApplier` -- applies those decisions to a form layout or a grid in place, hiding or
  marking read-only the sensitive fields; both the Avalonia views and the Blazor `FormPage` use it.

> Client-side capability resolution is **UX degradation only**. The backend remains the
> authoritative security boundary.

## Design Conventions

- **No transport** -- the package references `Polhem.Api.Contracts` only. `POLHEM9001` (`src/Directory.Build.targets`)
  fails the build on a reference to `Polhem.Api.Client`, `Polhem.Api.Core`, `Polhem.Api.Server` or `Polhem.UI.ApiBridge`.
- **Framework-agnostic** -- no UI-framework types, so the same contracts and rules serve every front end.
- **Resolution order in the views** -- a control uses its own property first, then its overridable `Resolve*` hook,
  then `UIServices.Provider`; with none of them it runs on what the host set.
- **Process-wide state stays out of multi-user hosts** -- `UIServices` is static. The Blazor head references this
  package for the value rules only; `UIServicesUsageGateTests` (`tests/Polhem.Web.Blazor.Server.UnitTests`) fails when
  it references `UIServices` or `Polhem.UI.ApiBridge`.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- project root -- `UIServices`, `IUserContext`, `FormValueBinding`, `FormDataGuard`, `FormLiveComputationEngine`
- `Definitions/` -- `IFormDefinitionSource`, `StorageDefinitionSource`
- `Fields/` -- `FieldEditRules`, `FieldValueFormatter`, `NumericFieldFormat`, `DateTimeFieldFormat`,
  `LookupFieldRules`, `LookupDisplay`, `GridCellFormatter`, `FieldCaptionRules`
- `Connectors/` -- `FormListResult`, `FormDataResult`, `FormDeleteResult`, `ListSelectFields`
- `Permissions/` -- `IElementCapabilityResolver`, `ElementCapabilityResolver`, `FieldCapability`, `LayoutCapabilityApplier`
