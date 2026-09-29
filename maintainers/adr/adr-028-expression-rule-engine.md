# ADR-028: Custom expressions and a rule engine (less hand-written BO code)

## Status

Accepted (2026-07-09), partially superseded: the assembly layout by
[ADR-038](adr-038-definition-dependency-boundary.md) (see the note below).

> **The assembly layout was revised by [ADR-038](adr-038-definition-dependency-boundary.md) (2026-08-11)**:
> the three abstract types `IExpressionEvaluator` / `ExpressionPolicy` / `ExpressionEvaluationException`
> moved to `Polhem.Base.Expressions`, and `Polhem.Expressions` keeps only `DynamicExpressoEvaluator`.
> This ADR's evaluation semantics, `FormExpressionCalculator` staying in the definition layer, and the conclusion that
> client and server share a single implementation **are all unchanged**; the only change is that the abstraction and
> the implementation live in two assemblies.

## Context

Much of the business logic, "field calculations" and "checks before save/delete", used to have to be hand-written in
C# in a custom BO: even a one-line formula like "amount = unit price × quantity" meant creating a BO that overrides
`Save`. There were two pain points: a lot of boilerplate, and **customers could not customize it** (changing one
validation condition meant changing code, recompiling and redeploying).

The goal is to have this kind of logic live as **declarative expressions** in the `FormSchema` definition file, so
customers can customize it at design time:

- **Field calculations**: computed fields (`Amount = UnitPrice * Quantity`), recomputed and filled in before save.
- **Validation before save / checks before delete**: when a condition fails, show a message and abort the action.
- **Field default value expressions**: when adding data, produce the default value with an expression.

This capability spans the definition layer (`Polhem.Definition`), a new evaluation engine (`Polhem.Expressions`) and
the business logic layer (`Polhem.Business`), and is a structural contract of the framework's external API surface,
hence this ADR. The user guide is `docs/en/definitions/expression-rules.md`.

## Options considered

1. **A home-made mini parser as the evaluation engine**: fully under control, zero external dependencies.
   **Rejected**: reinventing the wheel, high maintenance cost. **DynamicExpresso** (`DynamicExpresso.Core`, MIT) is
   used instead: an interpreter for a subset of C# syntax that exposes no types by default (an unregistered identifier
   is an error at parse time, a natural sandbox), and can parse once and compile to a delegate for caching. Lighter
   than Roslyn Scripting, and closer to C# syntax than NCalc.

2. **The frontend is authoritative, or frontend and backend each implement the calculations**: field calculations are
   done live in the UI and sent straight to save. **Rejected**: data integrity cannot be entrusted to the frontend (it
   can be tampered with / miscalculate). **The backend is the only authority**: before saving, the backend always
   recomputes from the schema and overwrites the computed field values sent by the frontend; the frontend's live
   calculation is purely a UX preview.

3. **Build rounding into the expression engine (by `DbField.Scale`)**: handle it where it happens. **Rejected**:
   `DbField.Scale` is the DDL precision for creating tables, and business rounding is a separate system (see ADR-026).
   Numeric results of computed fields are **delegated to the existing `NumberFormatResolver.RoundByKind`** (by
   `NumberKind`), inheriting for free the company/currency/unit adjustable decimals and round-then-sum; the engine
   itself only computes at full precision and knows nothing of NumberKind, which keeps it portable.

4. **Subclasses override the whole `Save` (the current state)**: keep the existing way of extending. **Rejected**:
   "overriding the whole `Save`" makes future framework features and subclass overrides fight each other, and every
   customization point has to copy the authorization/audit boilerplate again. Instead `Save`/`Delete` are refactored
   into **template methods** (see below).

5. **`FormRule` applicability through an inline implication (`!When || Condition`)**: one field fewer. **Rejected**:
   an implication is easy for customers / consultants configuring it to get silently backwards. A structured two-part
   form is used instead, an optional `When` (the applicability condition) + `Condition` (the validation condition),
   named `When` (aligned with .NET FluentValidation's `.When()`, avoiding the semantic mismatch with Design by
   Contract, where "a precondition that does not hold = an error").

## Decision

- **Definition layer**: `FormField` gains `ValueExpression` (computed field) and `DefaultValueExpression` (default
  value expression); `FormSchema` gains a `FormRule` collection (`When` / `Condition` / `Message` / `Trigger` =
  `BeforeSave` | `BeforeDelete` / `TargetTable` / `Enabled` / `Order`). `FormSchema` uses **XML as the only transport
  serialization path** (backend `XmlCodec.Serialize` → frontend `XmlCodec.Deserialize`).

- **Evaluation engine (`Polhem.Expressions`, portable and shared)**: depends only on `Polhem.Base` + DynamicExpresso,
  with no server-only dependencies, so that backend BOs and a future frontend share the same engine and the same
  `ExpressionPolicy` (type mapping, `DBNull`→the type's default 0/empty), ensuring the frontend preview value = the
  backend saved value. The sandbox exposes only the field variables + whitelisted functions
  (`Today`/`Now`/`IsNullOrEmpty`) + `Guid`.

- **BO lifecycle (template methods)**: `FormBusinessObject.Save`/`Delete` are refactored into an orchestration layer.
  Authorization (`AuthorizeSave`), record scope (`EnforceWriteScope`) and auditing are **fixed and cannot be
  overridden**; in between there are three `protected virtual` override points, `DoBeforeSave`/`DoSave`/`DoAfterSave`
  (and their Delete counterparts). The base `DoBeforeSave` automatically applies, from the schema, default values →
  computed fields (rounding delegated to `RoundByKind`) → `BeforeSave` validation; ordinary CRUD forms need **zero BO
  code**. A subclass overriding `Do*` calls `base.Do*` first and then adds its own logic.

- **The backend is authoritative**: live calculation on the frontend (Phase 2, Avalonia first) is a UX bonus; saving
  follows the backend's recomputation. If the frontend cannot calculate (the AOT boundary), the worst case falls back
  to no preview, and correctness is unaffected.

## Consequences

- **Positive**: customers can do field calculations and validation before save/delete purely through definitions,
  without writing a BO; new framework features land in the orchestration layer or in a specific `Do*`, without
  fighting subclass overrides; rounding reuses the single numeric subsystem (round-then-sum stays consistent).

- **Demonstration**: `OrderBO` in `apps/Polhem.Northwind` migrated from "override `Save`" to "override
  `DoBeforeSave`": the detail amount became a `ValueExpression`, and the required customer/product/quantity checks
  became `FormRule`s; only "at least one detail row", "the header total (a cross-row SUM)", "status transitions (need
  to look up the stored status)" and "document number generation (needs a DB sequence)" stay in `DoBeforeSave`,
  clearly marking the current boundary of the declarative approach.

- **Boundaries (separate work, not covered by this ADR)**: cross-row/detail aggregation (`SUM(detail)`), virtual
  display computed fields, finer-grained `BeforeInsert`/`BeforeUpdate` triggers, more rounding modes (banker's /
  round down / round up, an extension of the numeric subsystem), evaluation timeouts, and exposing session variables.

- **Dependency**: adds the third-party package `DynamicExpresso.Core` (MIT). The engine goes through
  `Expression.Compile()`, so live calculation on mobile/WASM AOT targets needs separate measurement (the same
  trim/AOT context as ADR-025); but because the backend is authoritative, this risk only affects the frontend preview,
  not data correctness.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: the sandbox functions.** The function list in "Decision" is the one at the time of the decision; the
  sandbox has gained more since (for example `UtcNow` and `IsNullOrWhiteSpace`). The authoritative list is
  `s_helperFunctions` in `src/Polhem.Expressions/DynamicExpressoEvaluator.cs`.
- **2026-09-27: AOT and trimming on mobile heads.** The AOT question of "Dependency" is answered: `Expression.Compile()`
  falls back to DynamicExpresso's interpreter when dynamic code is unsupported, so live calculation needs nothing
  disabled. The CI Mobile AOT gate in `.github/workflows/build-ci.yml` runs `tests/Polhem.Expressions.UnitTests` with
  `-p:DynamicCodeSupport=false`. Trimming is a separate matter: DynamicExpresso reaches the members an expression names
  by reflection, so `Polhem.Expressions` ships `src/Polhem.Expressions/ILLink.Descriptors.xml`, which roots the types
  the sandbox exposes; `TrimmerDescriptorGateTests` (`tests/Polhem.Expressions.UnitTests`) fails when the descriptor
  and the exposed types disagree.
- **2026-09-27: rule messages are localized.** A failing `FormRule` carries its `Message` as the English text and, when
  the rule and the schema are named, the language key `{ProgId}.Rule.{RuleId}.Message`, which the server resolves in
  the session's culture (`src/Polhem.Definition/Forms/FormExpressionCalculator.cs`).
