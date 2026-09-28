# ADR-035: Business logic plugins (hooking into the existing flow rather than replacing the whole BO)

[繁體中文](adr-035-business-logic-plugin.zh-TW.md)

## Status

**Accepted (2026-08-06); Decision 3 revised on 2026-09-05**. The decision has been carried out. The `PluginSettings`
definition types, the `FormBusinessPlugin` base with its four hook points, the additive semantics of stacking two
layers, and the first write path of the customization layer are all in place.

This ADR records six long-lived decisions: **hook in rather than replace**, **the closed set of hook points and the
criteria for adding one**, **declaration granularity and the per-operation lifetime**, **two layers that add up, with
no removal semantics**, **two asymmetries in failure handling**, and **the boundary with the rule engine**.

Decision 3 was originally "the settings file lists only types, and the class decides its stage by what it overrides";
on 2026-09-05 it was changed to **"the settings file states the stage explicitly, one stage per plugin"**. What was
revised, what was deliberately given up, and the direct reason for changing the decision are all written in that
section.

## Context

The customization overlay of [ADR-016](adr-016-multitenant-customization-overlay.md) provides four mechanisms:
language resources, FormLayout, custom BOs and custom Repositories. Of these, there is only one way to "change business
behavior": **inherit the whole BO class** and replace the binding in `ProgramSettings`
([ADR-034](adr-034-progid-type-registry.md)).

That granularity is too heavy for common needs:

- "Send a notification after saving" means taking over the business object of the whole document just for that.
- When several customization needs stack up, the only option is one grab-bag subclass, with no isolation at all
  between them.
- Once a custom BO exists, whether steps the packaged BO adds later take effect depends on whether the subclass
  remembered to call `base`.

`FormBusinessObject`'s Save / Delete have long been split into three overridable sub-methods each:

```
Save:   DoBeforeSave → [capture change set] → DoSave → [write change audit] → DoAfterSave
Delete: DoBeforeDelete → DoDelete → [write delete audit] → DoAfterDelete
```

What was missing was not an extension point, but **a way of hooking in that is lighter than inheritance and can be
declared per tenant**.

## Decision 1: Hook in rather than replace, with a separate `PluginSettings`

A new `PluginSettings.xml` carries the plugin chain, and `ProgramSettings` keeps its role as the "progId → type
binding" registry unchanged.

Plugins run **after the final implementation** of each `Do*` sub-method; that final implementation may be the base of
the packaged BO or an override in a custom BO. **The two extension techniques, inheritance and plugins, can therefore be
stacked**: inherit when you need to take over the whole flow, hook in a plugin when you only want to add a step.

### Why not put it in `ProgramSettings`

The meaning of `ProgramSettings` is "which type this progId **is**": one progId, one BO, one Repository, an
**either-or** relationship. A plugin is "what else this progId **must also do**": one progId, many plugins, an
**additive** relationship. Mixing the two meanings in the same file would turn the override rules into a list of
per-attribute exceptions.

## Decision 2: Four hook points, and the set is closed

`BeforeSave` / `AfterSave` / `BeforeDelete` / `AfterDelete`. The names use **lifecycle stages** rather than BO method
names (the `Do*` prefix belongs to the BO's overridable steps, which is a different layer of concept).

### Why not six

The draft originally had one for each `Do*` sub-method. The two post-points of `DoSave` / `DoDelete` were cut **not
because "no use could be thought of"**, but because they behave exactly like `After`: both are outside the transaction,
both can change `RefreshedDataSet`, and the audit reads the diffgram captured before `DoSave`, so neither affects it.
Keeping them would only force users into a multiple-choice question with no right answer.

Read methods (`GetList` / `GetData` and so on) have no three-part structure, and **the ruling is not to split them**.
The hookable scope is therefore closed within the two pipelines, Save and Delete.

### Three tests for adding a hook point

1. **Distinguishable from its neighbors**: its behavior differs from existing points, not just its position.
2. **Has a concrete use**: driven by a real need, not opened for symmetry.
3. **Does not lead people to a dangerous position**: this test eliminates candidates that "have a use". For example, a
   pre-point of `DoSave` falls **after** the audit snapshot; changing data there would be written to the database but
   not enter the audit trail.

### The cost asymmetry decides the default

Hook points are a public contract: **adding is non-breaking, removing is breaking**. So the default leans towards
fewer, extended as real needs push for it, rather than opening a whole row in advance and waiting for someone to use
them.

## Decision 3: The settings file states the stage explicitly, one stage per plugin

> **This section was revised on 2026-09-05.** The original decision was "the settings file lists only types, and the
> class decides its stage by what it overrides", and it rejected the option of "stating the stage explicitly in the
> settings file". Below, **what was given up** and **why it changed** are recorded together.

```xml
<PluginSettings>
  <Items>
    <ProgramPluginItem ProgId="Order">
      <Plugins>
        <PluginItem Type="Acme.Plugins.CreditLimitCheck, Acme.Plugins" Stage="BeforeSave" />
        <PluginItem Type="Acme.Plugins.OrderSync, Acme.Plugins"        Stage="AfterSave" />
      </Plugins>
    </ProgramPluginItem>
  </Items>
</PluginSettings>
```

One binding declares one stage, and the class must override **exactly that one** stage. The key of `PluginItem` is
still the **type name**: a class hooks into only one stage, so the same type appears at most once within a program and
no composite key is needed; declaring the same type twice is rejected at load time.

### Why "one class, one stage" rather than "one class, several stages"

**Single responsibility.** The two stages do fundamentally different things: `BeforeSave` is checking / adjusting before
saving, and `AfterSave` is a side effect after saving. Putting two things of different nature into the same class
sacrifices the class's single responsibility for the sake of sharing an instance field.

This is **a design ruling, not something enforced by the type signatures**. The signatures of the four stages are
`BeforeSave(SaveContext)` / `AfterSave(SaveContext)` / `BeforeDelete(DeleteContext)` / `AfterDelete(DeleteContext)`:
the parameter types differ only between the Save pipeline and the Delete pipeline, and **are the same within a
pipeline**. So "the same class doing both `BeforeSave` and `AfterSave`" is entirely possible as far as types go; not
doing it is a choice.

### What was given up: per-operation state sharing across stages

The original decision treated the per-operation lifetime as the condition that made "list only types" work, and called
sharing the same instance across stages the **only real advantage** of that option over "stage × type".
**This revision deliberately gives it up.**

"A check (`BeforeSave`) + a follow-up action (`AfterSave`)" must now be written as two classes, with **no place to
share state** between them; the data `AfterSave` needs has to be reread or recomputed. The original decision saw this as
"one need forced to split into two classes" (a loss); this revision sees it as "two different things should have been
two classes all along" (a correction). The difference is not in the facts, but in whether that instance field is worth
trading single responsibility for: originally judged worth it, now judged not.

Instances are still **per-operation**: each Save / Delete constructs its own, not shared across calls, so locking does
not need to be considered. But they **no longer carry any cross-stage guarantee**, and they are now **constructed on
demand**: a plugin is only constructed the first time its own stage runs. The old design's reason for "constructing the
whole chain at once" (later stages have to find the same object) no longer exists, so a single Save does not construct
plugins hooked only into delete stages.

### The direct reason for changing the decision: the compensation for the readability cost never materialized

The original decision acknowledged that "you cannot tell from the XML which plugin runs at which stage", and answered:
the fix is not to change the structure of the settings file, but to compute the execution list of each stage by
reflection, **for a maintenance tool to display the execution order**.

That maintenance tool never existed. `FormPluginChain.TypesForStage` has zero production callers, and no consumer of
`PluginSettings` can be found anywhere in the tree. The readability cost was therefore a net loss all along, and the
compensation stayed on paper.

### Reflection did not retire; it became a validator

The stage information now lives in two places: what the class overrides, and what the XML declares. **The two must be
equal; a mismatch always refuses to load**, and the message says which stage the class actually overrides. When they
are equal, "run as declared" and "run by reflection" are the same thing, with zero change in execution semantics; when
they do not match it fails loudly, and "overridden but not declared → silently not run" cannot happen.

Two gates complement each other: at save time, the maintenance API of `SystemBusinessObject` blocks it (the editor
knows on the spot); at resolution time, `PluginSettingsResolver` blocks it (**hand-written files** have no maintenance
API: the packaged-layer `{DefinePath}/PluginSettings.xml` and external users never go through it). The chain is cached
by `(customizeId, progId)`, and the reflection was computed only once anyway, so the reconciliation costs nothing
extra.

### The honest cost: changing a plugin class means changing the XML too

Previously, switching the stage a plugin overrides took effect automatically on redeploying the assembly. From now on
the same change **throws** at the next resolution until the XML catches up. This is a new coupling, and what it buys is
"the settings file says what runs".

## Decision 4: Two layers add up, with no removal semantics

The packaged chain runs first and the custom chain afterwards, each in the declaration order of its file. **No priority
numbers are introduced** (numbers always end up as a 10/20/30 mess).

This is **the only item with "additive" granularity** in the customization overlay; the other four are all either-or
(language resources per key, FormLayout per whole file, BO / Repository per progId). The reason goes back to the
difference in meaning in Decision 1: a binding names "this program is this type", so a customization that wants to
change it must replace it; a plugin is just one more step, and plugins of the two layers do not conflict.

A customization only writes the plugins it adds itself, and plugins the packaged product adds later **take effect
automatically**.

**The cost: a customization cannot disable a plugin declared by the packaged product.** There is no tombstone syntax.
To really remove one, inherit and override that sub-method, which leads straight back to the division of labor "use
inheritance when you need to take over the flow".

The API surface of both layers is open (`DefineType` / `IDefineAccess` / cache / path / reader, the whole set); the
framework just **does not ship a packaged file** for now, keeping open the possibility of assembling optional modules
from plugins later.

## Decision 5: Two asymmetries in failure handling

### Runtime exceptions: always propagate

When a plugin at any stage throws → the exception propagates, and `Save` / `Delete` report failure. A message meant for
the user is thrown as `UserMessageException` (the framework's existing signal for aborting a business flow, already
used by the rule engine), so plugins need no new mechanism.

**Rejected**: swallowing the exception + logging (when an important follow-up action of a customization fails, nobody
knows); wrapping it in the same transaction and rolling back (transactions are not lifted up to the BO layer).

But "throwing aborts" is right for validation plugins, while for **synchronization with external systems** it becomes
"the other system is under maintenance, so the user cannot save the document". In practice it would inevitably be
bypassed with a blanket `try-catch`, and a rule the framework sets that everyone bypasses is meaningless. So **no
mechanism is added; instead, the documentation states where the responsibility lies**:

> Plugins that synchronize with external systems should handle their own failures (log without rethrowing, or register
> a retry), rather than letting the availability of an external system decide whether users can complete their work.
> The framework's default is "throwing aborts", because validation plugins need it; which failures should abort the
> operation is the plugin author's judgement.

It must also be understood that plugins run **outside the transaction**: if the process dies before the plugin runs,
the data is committed, the synchronization did not happen, and no trace is left. A synchronization that must not be
missed should register an outbox entry inside the transaction; plugins are only suitable for best-effort scenarios or
ones with reconciliation as a safety net.

### Resolution failures: always throw, the opposite of the BO axis

When a BO type cannot be loaded, it **degrades** to `FormBusinessObject` (the service is not interrupted, but an error
is logged); when a plugin type cannot be loaded, it **throws directly**.

The asymmetry is deliberate: a binding names "this program is this type", and falling back still gives **a program that
runs**; a plugin was added deliberately by its author, and skipping it means **the customization did not take effect**.
Silently skipping a credit limit check is worse than refusing to save.

Likewise, the write API of `PluginSettings` **validates each binding before saving**: the type can be loaded, it
inherits `FormBusinessPlugin`, and it **overrides exactly the one stage declared by that binding**; if any single
binding fails, the whole file is refused. Putting validation on the write side means the editor knows about a typo **on
the spot**, rather than weeks later when some document cannot be saved. A plugin that overrides no stage does nothing
even when hooked in, and one that overrides two stages violates "one stage per plugin"; both are configuration errors.

## Decision 6: The boundary with the rule engine (ADR-028)

| | Rules (`IFormRuleProcessor`) | Plugins |
|---|---|---|
| Stored in | The FormSchema (**not customizable**, permanently excluded by ADR-016) | `PluginSettings.xml` (**customizable**) |
| Form | Declarative expressions | Compiled types |
| Suited to | Field-level default values, computation, validation | Cross-table / cross-system side effects |
| Deployment | Change the definition file | Deliver an assembly |

**"The rule engine is not connected to the customization layer" is not a gap waiting to be filled but a deliberate
division of labor**: rules are not customized; customization goes through plugins. Rules live in the FormSchema, and
the FormSchema drives both the database structure and validation, so diverging per tenant would split the physical
schema ([ADR-016](adr-016-multitenant-customization-overlay.md)).

### Accompanying decision: no plugins for Repositories

Interception in the data access layer would make SQL behavior untraceable. To change it, "replace the whole
Repository".

## Consequences

### The customization layer becomes writable for the first time

`PluginSettings` is **the only customization definition with a maintenance API** (`GetCustomizePluginSettings` /
`SaveCustomizePluginSettings`, both `LocalOnly`). Before this the customization layer was entirely read-only and
therefore had no cache invalidation mechanism; both were added together. In file mode, writing immediately evicts that
tenant's cache slot (the maintenance tool reads back what it just saved right away, and the "eventually arrives" of a
file watcher is not the contract that belongs here).

`CustomizeOnlyStorage` stays entirely read-only, and writing instead goes through `ICustomizeDefineWriter`, which writes
the file directly via `CustomizeOnlyPathOptions`: the two share the same path source, so that class's read-only promise
does not have to be broken for a single exception.

### Knock-on fix: the load condition of `DeleteContext.Snapshot`

The original condition did not load the snapshot when auditing was off and there were no `BeforeDelete` rules, yet an
`AfterDelete` doing external synchronization certainly needs to know what was deleted. The condition now includes
"whether this progId has a plugin at a delete stage"; otherwise whether there is a snapshot would depend on **an audit
switch unrelated to plugins**, and the same plugin would work in one deployment and get `null` in another, which is the
hardest kind of difference to track down.

### Main types

- `src/Polhem.Definition/Settings/PluginSettings/`: `PluginSettings` / `ProgramPluginItem` / `PluginItem` /
  `PluginStage` / `PluginBinding` and the two collection types
- `src/Polhem.Business/Form/FormBusinessPlugin.cs`: the base and its four empty virtual implementations
- `src/Polhem.Business/Form/FormPluginChain.cs` / `FormPluginRunner.cs` / `PluginSettingsResolver.cs`: resolution, the
  chain and execution
- `src/Polhem.Definition/Customization/CustomizeOverlay.cs`: `GetPluginBindings` (the only additive overlay)
- `src/Polhem.Definition/Storage/ICustomizeDefineWriter.cs`: the write path of the customization layer

### Related documents

- [Tenant Customization](../en/customization.md): the decision table and how-to for the five mechanisms
- [End-to-End Development Cookbook](../en/development-cookbook.md): the "Business Plugins" section
- [ADR-016](adr-016-multitenant-customization-overlay.md): the customization overlay
- [ADR-028](adr-028-expression-rule-engine.md): expressions and the rule engine
- [ADR-034](adr-034-progid-type-registry.md): the ProgId type registry

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-08-16: the BO axis now throws too, so "the opposite of the BO axis" no longer holds.** Decision 5 contrasts
  plugins with business objects, which at the time fell back to `FormBusinessObject` when their type could not be
  loaded. [ADR-034](adr-034-progid-type-registry.md) (Implementation evolution, 2026-08-16) changed that: a declared
  `BusinessObject` type that cannot be loaded, or does not derive from the expected base, throws
  `InvalidOperationException` (`src/Polhem.Business/ProgramSettingsBoTypeResolver.cs`). Plugin resolution throws on
  every failure as described above (`src/Polhem.Business/Form/PluginSettingsResolver.cs`), and the reason given for it
  still holds: skipping a plugin means the customization silently did not take effect. Only the asymmetry with the BO
  axis, listed in the Status as one of the two asymmetries, is gone; the asymmetry in runtime exceptions is unchanged.
