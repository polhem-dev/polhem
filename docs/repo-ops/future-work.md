# Future work ideas

Directions that have not started and have not been written up as a plan yet. **This file records only "why do it,
what to wait for, and what the first step is when it starts"**. When one really starts, first write a plan in
`local/plans/` for the user to review, and carry it out only after that (see "Plan before you build" in
`.claude/CLAUDE.md`).

## `sys_date` system field: naming the role of "document date"

**Idea (raised by the user on 2026-08-13 while proofreading Day 3 of the iThome Ironman series)**: add a `sys_date`
system field for the document date. `FormBusinessObject.GetNewData` recognizes it and fills in today in the user's
time zone by default.

⚠️ **Know this before you start: "fill in today" already happens now, and over a wider scope.**
`FormRowDefaults.DefaultForDbType` returns `FrameworkClock.Today(timeZoneId)` for **every** `FieldDbType.Date` field
(`DataFormRepository.GetNewData` → `FormRowDefaults.Apply`).
So "fill in today when there is a `sys_date`" adds no new behavior in effect.

**The real value is not the default value but naming a role**, in the same family as `sys_id` (business code) and
`sys_name` (display field). Once the framework knows which field is the document date, the default value is not the
only thing that can use it: the default range of period queries, report ranges, period-close checks, and which day an
audit record belongs to.

**It also exposes a real problem**: the XML doc of `FormRowDefaults` states that its purpose is NOT NULL filling
("never reaches the database with a NULL"), while "the document date defaults to today" is a **semantic** default.
The two currently share the same rule, and the symptom is visible in `apps/Polhem.Northwind`: filling `order_date`
with today is right, but **filling `hire_date` with today only keeps it from being NULL**.

**Four questions to answer first**:

1. `sys_*` are framework-reserved names (`docs/en/framework-reserved-names.md`). Adding one extends the reserved
   word list, and existing applications may already use that field name.
2. **Truly separating the semantics means that `Date` fields not marked `sys_date` stop being filled with today
   automatically, and that is a breaking behavior change.** It has to go through the version number and the
   CHANGELOG. Without that change, `sys_date` is only an alias and solves nothing.
3. Does a document have only one date? An expected ship date or a due date cannot be carried by it, so you are back
   to declaring field by field.
4. `FormField.DefaultValueExpression` has shipped and its allowlist includes `Today()`. **The same thing can be
   expressed by declaration**, and a declaration can express what `sys_date` cannot (which field wants today, which
   wants the end of the month).

**The criterion that splits the paths** (the project's own rule "converge what is standard, leave what differs to the
application"): "the date defaults to today when a document is created" is standard → it should converge; "which field
is that date" differs from company to company → it should be declared.
**Each path is right about half. What really needs deciding is "does the framework need to know the role of the
document date".**

**What to wait for**: **the end of the 2026 iThome Ironman publishing run**. If the framework adds `sys_date` and the
examples change with it (`order_date` → `sys_date`), at least four articles of the series must be verified again: the
system field table in Day 3 §2 (it currently lists five, and the section says "these names cannot change"), Day 3 §5,
the example section of Day 27 (its two `Date` fields are exactly the material), and the reconciliation table of Day 29.
**And after an Ironman article is published, it can only be changed on the same day.**

**First step when it starts**: answer question 2 above first (the scope of the breaking change), then decide whether
to write a plan.

## Skill pack for outside developers (Claude Code plugin)

**Goal**: build a skill pack for **outside developers who use the Polhem framework**, so they can get started quickly.

**The key distinction: this is not "sharing the existing skills"**:

- `.claude/skills/polhem-*` (`polhem-app-scaffold` / `polhem-add-form` / `polhem-add-bo-method` and others) take the
  internal view of **development inside the polhem repo**: they refer to internal paths such as `src/Polhem.*` and
  `apps/Polhem.Northwind`. **They cannot be shipped to outside consumers as they are** (the reason is the internal
  paths and the internal view, not version control: `.claude/skills/` and `.claude/commands/` have been under version
  control since 2026-07-23).
- The developer pack takes the **consumer's view** (someone who installed the `Polhem.*` NuGet packages): it refers
  only to the public API surface, the public documents and the public samples; it must be **version-controlled,
  published and maintained with each version**; and it is packaged as a Claude Code **plugin**.

**Distribution and anchor**: make it one plugin (such as `polhem-dotnet`), **anchored on a public standalone
Northwind repository built on the `Polhem.*` packages** as the living example (not the internal `apps/`).

**The anchor exists**: [`polhem-dev/polhem-northwind`](https://github.com/polhem-dev/polhem-northwind) runs on the
`Polhem.*` 1.0.0 packages (created 2026-09-28; how it is kept in sync is in
[gotchas/northwind-heads.md](gotchas/northwind-heads.md) § Graduation and periodic sync). The pack itself has not
been started.

**Planned contents** (consumer-side rewrites of existing knowledge, plus an onboarding path): `polhem-quickstart`
(install NuGet → minimal app → run it), `polhem-app-scaffold` (PackageReference version), `polhem-add-form`,
`polhem-add-bo-method`, `polhem-formschema-reference` (the complete conventions for lookup / detail / dropdown /
read-only / scope), `polhem-concepts` (FormSchema hub / DataSet DTO / BO / Repository two tracks / common-company
scope), serialization, caching.

**First step when it starts**: write a plan (plugin structure, the consumer-side rewrite of each skill, the
distribution and maintenance mechanism, tying it to releases). The concept consumers get wrong most easily is DB
scope; see `.claude/rules/database.md`.

## A tool for deployment-time operations

> Originally recorded as "API key management tool". After stage 1 of the deployment-level administrator landed on
> 2026-08-01 there was a second consumer, so the scope widened from "key management" to "every `LocalOnly`
> deployment-time operation". That changes the trade-off of where it lands, below.

**Goal**: let the deploying side run deployment-time operations without writing code.

**Why it is needed**: while there is **no deployment-level administrator yet**, deployment-time operations can only be
called on the host, in process (`SetDeploymentAdmin` is always `LocalOnly`). The capabilities have all been
delivered, but there is no "just run it on the host" entry point, so the deploying side has to write the calling code
inside the host process itself.

**Current consumers**:

| Operation | Method | Status |
|------|------|------|
| Issue an API key | `SystemBO.CreateApiKey` | Delivered, no entry point. It also works remotely (for a deployment-level administrator), but **a deployment with no administrator yet still has only the local path**, so bootstrap still needs an entry point |
| List, disable, set expiry | `SystemBO.ListApiKeys` / `SetApiKeyEnabled` / `SetApiKeyExpiry` | Delivered, no entry point. Authorization is the same as issuing: a local call is allowed directly, a remote one requires a deployment-level administrator |
| Assign the deployment-level administrator | `SystemBO.SetDeploymentAdmin` | Delivered, no entry point. `LocalOnly`, **and it is the only write path for that column**: without a tool you have to write code yourself, or `UPDATE st_user` by hand |

The `SetDeploymentAdmin` row is especially awkward: it is **the only way to create the first administrator** (a
bootstrap account in a settings file was rejected as a permanent back door), so it is the first thing a new
deployment runs into after connecting to the framework.

**Two candidate homes**:

| Home | Trade-off |
|------|------|
| `dotnet polhem apikey ...` / `dotnet polhem admin ...` | A CLI is naturally a deployment-time tool and can go into scripts. But `tools/Polhem.Cli` currently declares only `Polhem.Definition` (its transitive closure is `Polhem.Definition` + `Polhem.Base`; since ADR-038 it no longer includes `Polhem.Expressions`). Reaching the DB means pulling in `Polhem.Business` and the repositories as well. **This is the main decision of this item**: it turns the CLI from a "definition file tool" into "an operations tool that must reach a database" |
| Add a tab to DefineEditor | Already a local Avalonia tool with a DI host. But its role is editing definition files, and keys and the administrator flag live in the DB, not in definition files |

**How more consumers change the trade-off**: the cost in the CLI cell (pulling `Polhem.Business` and the repositories
into `tools/Polhem.Cli`) is **one-time**. Once connected, each new deployment-time operation is just one more
subcommand. Paying that dependency cost for a single feature looked heavy; now that issuing, listing, disabling,
setting expiry and assigning the administrator have all been delivered, amortizing it is much more reasonable.
Conversely, the "wrong role" problem in the DefineEditor cell only grows as consumers are added: its tab would
gradually become an operations panel unrelated to definition files.

**What to wait for**: nothing. The operations in the table above have all been delivered; it simply has not been
scheduled.

**First step when it starts**: decide where it lands and the dependency trade-off in the table above, then write the
plan.

## Declarative logic at the tenant level (where to draw the line)

**Goal**: make **the cost of a change proportional to the size of the need**. A simple logic change should only need
a definition file edit; only a complex one should go through assembly deployment.
It is not "the tenant level should never need a new version"; it is that the line is currently forced into an
extreme position.

**Current state**: the line **already exists at the product level**. Declarative expression rules (inside
FormSchema) handle the simple cases and BO code handles the complex ones, each in its place. **The tenant level has
only the "code" half**:

| | Simple logic | Complex logic |
|---|---------|---------|
| **Product level** | Expression rules (edit a definition file) | BO / plugin (assembly) |
| **Tenant level** | ❌ **This half is missing** | ✅ Custom BO / Repository / plugin |

So a tenant that wants to change a discount formula (which should be the simplest kind) and one that wants to
integrate an external ERP (which should be the most complex kind) **pay the same price**: edit a `.cs`, recompile,
deploy to the shared host `bin`.

**Why this hurts especially with multiple tenants**: the cost of assembly deployment is asymmetric. With one tenant it
is just one downtime; with many tenants, a one-line formula change for tenant A makes B, C and D share the deployment
risk: coordinate everyone's maintenance window, run regression tests for every tenant, and roll back the whole batch
if any tenant has a problem. And every tenant's custom assemblies sit in the same `bin`, tied to the same compilation.

**Core design questions (answer these first when it starts)**:

1. **Where the line should be drawn**: which changes belong on the "definition file side" and which on the "assembly
   side". The existing boundary at the product level (expressions in `IFormRuleProcessor` vs BO code) is the starting
   point, but the distribution of needs at the tenant level may be different.
2. **What carries the "definition file side" at the tenant level**: there is a ready-made deadlock. At the product
   level the declarative rules live inside `FormSchema`, and per
   [ADR-016](../adr/adr-016-multitenant-customization-overlay.md) `FormSchema` is **permanently not customizable**
   (it also drives the database structure, and diverging per tenant would split the physical schema). So
   tenant-specific rules need **another home**, perhaps an extension of `PluginSettings`, perhaps a new definition
   type.

**What to wait for**: nothing. `PluginSettings` ([ADR-035](../adr/adr-035-business-logic-plugin.md)) has already
shown that the path "a customized definition is writable + has a maintenance API + a cache invalidation chain" works;
it is the ready infrastructure for this item.

**First step when it starts**: collect real tenant customization cases and sort them by "could a declarative rule
solve it", so that the real distribution decides where the line in question 1 goes, rather than designing a syntax
from imagination.

## Bringing `tools/` into SonarCloud's analysis scope

**Idea (dug up on 2026-09-10 while fixing the scan findings of `Polhem.LoadTests`)**: SonarCloud currently analyzes
only `src/` and `tests/` in practice. Of the 1,760 analyzed files in the whole project (measured on the old
bee-library SonarCloud project, before the move to `polhem-dev_polhem`), `tools/` accounts for only 2,
and neither is C# (one `.py` and one `.sh`, picked up by generic file detection). **Not a single `tools/**/*.cs`.**

**Why it is worth doing**: not to make the numbers look good, but because **a 0 there is misread as clean**. It
happened this time: querying the rules for `tools/Polhem.LoadTests` with `/api/issues/search` returned 0 and the fix
was judged done, when in fact the file was not in the analysis scope at all and the 0 had nothing to do with the
code. Had `components/tree` not been queried to verify something else, that misjudgement would have stayed. The
symptom and how to check are in
[gotchas/test-ci-release.md](gotchas/test-ci-release.md), section
*A 0 from Sonar may mean "not looked at" rather than "clean"*.

**To find out first**: **the mechanism is still unclear**. `tools/Polhem.LoadTests` is built through the
`ProjectReference` of `tests/Polhem.LoadTests.UnitTests`, and SonarScanner for .NET should pick up transitively built
projects, but the result says it does not. The first step when it starts is to find out why: it may be the scanner's
project scope, or the `--no-incremental` of the `Build (for Sonar coverage)` step may not rebuild transitive projects
(the preceding strict build has already built them).
**Do not skip this step and change the settings directly.** If you change them without knowing the cause, you will
not know afterwards whether the change really took effect.

**What to wait for**: no hurry. Most of `tools/` does not ship (`IsPackable=false` in `tools/Directory.Build.props`),
so the risk of the scan missing it is lower than for `src/`. The exception is `tools/Polhem.Cli`, which overrides it
and is published as a dotnet tool; its code is outside the scan too. But **every claim that "some Sonar rule is clean in `tools/`" is false**. Until it is brought
in, such a claim only counts after running the local reproduction in that gotchas file (temporarily add
`SonarAnalyzer.CSharp` and build once).

**Known to surface along with it**: at least S2077 at `SchemaPreparer.cs:169` (PostgreSQL's `CREATE DATABASE`; the
database name is an identifier and cannot be parameterized). Once brought in, it will be an issue that needs a human
to read it and mark it False Positive, not a defect to fix. **It deliberately has no `#pragma` suppression now**:
while the scanner cannot see it, a suppression only makes the code harder to read and buys nothing.

## Exchange rate master and automatic fill: the only two gaps in multi-currency

**Idea (inventoried on 2026-09-01 when the user asked about the multi-currency design)**: add **a company-level
exchange rate master**, and **fill in the exchange rate automatically from the document date when a document is
created**. The conversion itself is not missing; see the next section.

### Premise: the document carries its own exchange rate field (this decides every other judgement)

The ERP convention for multi-currency documents is to **store the exchange rate of the time directly in a field of
the document master, and treat the rate on the document as authoritative**.
This premise decides the shape of this whole section:

- **The role of the exchange rate master is "source of the default value", not "basis of the calculation"**. It is
  filled in when the document is created → written to the document field → every later calculation uses the value
  on the document. If the rate table is edited later, documents already created are not affected.
- **Manual override comes for free**: for a contract rate or a rate locked for a presale, the user edits the document
  field directly.
- **Checking the books later only needs the document itself**; there is no need to look back through the rate table's
  history.

Going one step further decides the master's **correctness requirements and failure behavior**:

- **Failing to find a rate should not block creating a document**: it is a reference value, not voucher data. When
  there is no rate for the day, leave it empty or give 0 for the user to enter, instead of throwing an exception or
  blocking the save.
- **The lookup strategy can be "the most recent entry not later than the document date"**; an exact hit on the day is
  not required, since holidays and unmaintained days are certain to exist in practice.
- **The audit requirement is relatively low**: it is not voucher-level data. What really needs a trail is the value on
  the document, and that already goes with the document's audit trail.

### The conversion capability already exists (**it was once misjudged as "not existing at all"**)

Early in the inventory "the conversion half does not exist at all" was the judgement, and **that overstated the gap**.
Once the document carries its own rate field, local currency conversion works with **pure definitions**, with no new
framework capability:

```
sys_exchange_rate   NumberKind="ExchangeRate"                       ← Preserve, not rounded
amt_doc             NumberKind="Amount"                             ← uses schema.CurrencyField
amt_local           NumberKind="Amount" CurrencyField="sys_local_currency"
                    ValueExpression="amt_doc * sys_exchange_rate"
```

Every link of the chain is implemented:

- `ResolveRefCode` lets **each field specify its own `CurrencyField`**, falling back to `schema.CurrencyField` only
  when there is none (`src/Polhem.Definition/Forms/FormExpressionCalculator.cs`), and the code value is taken from the
  row's variable table. So the document currency field and the local currency field **each** resolve to the decimals
  of a different currency.
- The rate field is `Preserve` (`NumberKindProfile.GetRoundingPolicy`). It is not rounded to 5 places before being
  multiplied; the multiplication uses full precision (in line with D4 of
  [ADR-026](../adr/adr-026-numeric-semantics-rounding.md)).
- The product goes through `RoundByKind`, is rounded to the **local currency's** decimals, and then round-then-sum.

So **ADR-026 D2's "the document currency and the local currency each round-then-sum independently by their own
currency key field" is not a plan; it is current behavior**. (Derived statically, every link checked against the
implementation; no test has been written to run it yet.)

**A related correction: `NumberKind.ExchangeRate` is not an orphan tag.** Early in the inventory it was judged to have
zero consumers and be nearly dead code, by the test "does the framework multiply with it itself". **The test was
wrong.** Its role was always the numeric semantic declaration of "the exchange rate field on the document" (5 places,
not rounded, decimals do not vary by company), and the multiplication is meant to be declared by the app on
FormSchema. That is exactly the consistent FormSchema-driven strategy. The tag is usable now.

### The real gaps

| Level | Content | Status |
|----|------|------|
| System level | Currency codes, minimum unit | ✅ Exists |
| Company level | Local currency, allowed currencies, cash rounding | ✅ Exists |
| **Company-level data** | **Exchange rate master (dated)** | ❌ Missing |
| Document level | Exchange rate field, local currency amount calculated field | ✅ Can be declared |
| **Framework** | **Fill in the rate from the document date when a document is created** | ❌ Missing |

Automatic fill has ready seams: `FormBusinessObject.GetNewData` or `DefaultValueExpression`. This is the same
mechanism as the `sys_date` section of this file, and the two are best considered together (the document date is
exactly what the rate lookup is based on).

**One more point to clarify**: since 2026-09-10 `CompanyInfo.DefaultCurrency` is required (when there is a company
but the local currency is blank, resolving amount decimals throws), but in practice it is still used only as the
source for resolving decimals and has no conversion semantics. **For now it is a default currency, not yet a local
currency.** The XML doc's *default (local/home) currency* implies a concept the framework does not actually have;
clarify it when adding conversion.

### Question one: which level the rates belong in — the data level, not the definition level

This was once judged wrong (the first suggestion was "the same level as `CurrencySettings`, through `IDefineStorage`
and the `GetDefine` channel"), and the user corrected it with the scenario of a rented cloud service. The mistake was
**treating SAP's client level as directly analogous to this framework's system level**: in SAP one client = one
corporate group (the company codes share financial policy, and consolidated reports should use the same set of rates
anyway), while one deployment of this framework = **several unrelated businesses**. They are not the same level.

Even with SAP's absolute quotation model that is not tied to a local currency (TCURR stores currency against
currency, independent of the company), rates still cannot be shared under SaaS, for three reasons: **different rate
sources** (Bank of Taiwan / Bank of Japan / ECB give different numbers for the same pair on the same day),
**different statutory rate rules** (national tax laws specify spot on the transaction date / monthly average /
period end), and **different rate types and financial policies**. Odoo is more direct: `res.currency.rate` stores
"1 unit of the company currency = rate units of the foreign currency", and `company_id` is a structurally required
dimension (Odoo states that the field was originally moved from `res.currency` to `res.currency.rate` because
"currencies are defined worldwide, while rates vary by company and by time").

So exchange rates should follow the existing family in `src/Polhem.ObjectCaching/Database/` (`DepartmentTreeCache`,
`CompanyRolePermissionsCache`, `CompanyAuditRulesCache`): `ICacheDataSourceProvider` + the company database +
cache-notify invalidation, not `DefineType` + `IDefineAccess`.

**`CustomizeId` is not an alternative**: definition files do have a per-tenant overlay (`CustomizeOverlay`), but that
means "which **customized definition version** this company uses". Several companies can share one `CustomizeId`, and
definition files change rarely and follow versions. Exchange rates are business data that change daily; carrying them
there would stuff data into the definition level.

**The criterion (it can be used to check other layering)**: **is this data an "objective fact" or a "business
judgement"?**
Objective fact (ISO 4217 currency codes and decimals, units of measure) → system-level definition file;
business judgement (exchange rates, cash rounding policy, allowed currencies) → company-level data.
The framework's existing layering **already follows this**: `CompanyInfo.CashRounding` and `AllowedCurrencies` are at
the company level, `CurrencySettings` at the system level. **Keeping `CurrencySettings` at the system level is right
and is not affected by this item.**

### Question two: the dimensions of the exchange rate master

| Dimension | Required? | Reason |
|------|-------|------|
| Company | ✅ Required in the first version | See question one |
| **Effective date** | ✅ **Required in the first version** | Creating a document must look up by the **document date** (a late or back-dated document must not get the current rate); period-end valuation needs the period-end rate |
| Currency pair (or currency) | ✅ Required in the first version | See question three |
| Rate type (spot / booking / monthly average) | ⚠️ Can be deferred | Odoo does not have it; but reserve the key, because adding a dimension later is a breaking change |

> **The reason for the date dimension was once written wrong**: the first version said "checking the books later must
> be reproducible". Once the document carries its own rate, reproduction relies on the document itself, not on looking
> back through the rate table. The date dimension is now justified by "look up by document date when creating a
> document" and "period-end valuation".

### Question three: the data model

| Model | Example | Characteristics |
|------|------|------|
| Relative to the local currency | Odoo `res.currency.rate` | Few rows; a third currency relies on triangulation through the local currency |
| Absolute quotation per currency pair | SAP `TCURR` (source currency + target currency + rate type + effective date) | Can quote directly; many rows |

**Leaning towards the latter**: triangulation adds **one more rounding** (in USD→TWD→JPY, whether and to how many
places the intermediate value is rounded), and a direct quotation has no intermediate value. That error is far more
serious than the storage cost of a somewhat larger rate table.

### Known limitations and items to review

- **Consolidated group reports have no shared basis**: once rates are fully per company, two companies in the same
  group maintain their own, and consolidation does not reconcile (that is exactly why SAP puts them at the client
  level). This framework currently has no "group" level.
  This is a **known limitation**; when it is really needed, the answer is **adding a level**, not moving rates back to
  the system level.
- **Whether the fixed 5 places of `NumberKind.ExchangeRate` are enough depends on the quotation direction**: if
  documents always store the direct quotation "1 foreign currency = n local currency" (TWD/USD = 31.50000), 5 places
  are enough for the great majority of pairs; only extreme currencies such as IDR and VND need a factor mechanism like
  SAP TCURF (an item ADR-026 lists itself as not done, at its end). **An indirect quotation (JPY→USD = 0.0067…) is
  immediately not enough.**
- **`CurrencyItem.Name` is not localized**: it is currently a single string, while the display name needs to be
  (US Dollar / 美元 / 米ドル). The framework has the LanguageResource mechanism, but it is not connected here.
- **The display position of `CurrencyItem.Symbol` is not expressed**: prefix (`$1,234.56`) vs suffix (`1 234,56 €`)
  is a **regional convention** that follows the user's locale, not the currency. Odoo has a `position` field, but it
  is also attached to the currency, which is not entirely right either.
- **The system-level "minimum unit" expresses nothing more than "decimals"**: now that cash rounding has been split
  off to `CompanyCashRounding`, the system-level `CurrencyItem.Rounding` can only be `1 / 0.1 / 0.01 / 0.001` under
  ISO 4217; `CurrencySettings.DecimalsFromRounding` is just counting powers of 10 (putting in 0.05 is simply counted as
  2 places). **This does not need to change** (it matches Odoo and eases future integration), but do not mistake it
  for carrying the semantics of cash rounding.

**What to wait for**: a real need for multi-currency conversion (no app uses it yet). The infrastructure is in place:
the company-scope database-dependent cache family is a ready pattern, and the `polhem-add-cache-object` skill already
covers its complete cross-file procedure.

**First step when it starts**: first write a throw-away test that actually runs the pure-definition conversion chain
above (to confirm the static derivation holds), then settle the data model (question three), then create the exchange
rate cache object with `polhem-add-cache-object`.
The rounding policy itself is planned separately.

## BPM / workflow: approval flows and document state transitions

**Idea (raised on 2026-09-11 while discussing the positioning of the public documents)**: the framework's current
audience is form-based information systems such as ERP, CRM and HRM, and BPM / workflow (approval flows, document
state transitions) is a future direction. **It is not being done at this stage**; that is the user's decision.

**Starting point: this layer does not exist today, and it is left empty on purpose.**
The XML doc of `PermissionActions` states that state transitions such as Approve, Post and Confirm are **deliberately
not on the action axis**, but belong to "a separate workflow permission layer"
(`src/Polhem.Definition/Settings/Permission/PermissionActions.cs`).
That layer is only named; it has no interface and no implementation yet. What the framework has now is the form half:
form definitions, data access, and two layers of authorization (the action gate plus record scope; see
[ADR-019](../adr/adr-019-permission-authorization-model.md)).

**Constraint until it lands**: the public documents do not say "supports BPM", because that would be a capability
claim with no mechanism behind it. The repositioning of the public documents already excluded BPM on this basis
([plan-docs-positioning.md](https://github.com/jeff377/bee-library/blob/7d6cc9d9/docs/plans/archive/plan-docs-positioning.md)).
**Once it lands**, add BPM back to the scope stated in the public documents.

**Questions to answer first**:

1. **Build our own flow engine, or provide seams to integrate an external engine?** This question decides the shape
   of the other three.
2. **How do state transition permissions stack on the existing two layers of authorization?** `PermissionActions`
   already reserves "a separate layer", but the order of evaluation and the way it combines with the action gate and
   record scope are not defined yet.
3. **Where is the document state stored?** If it is a new `sys_*` system field, that extends the framework-reserved
   names, and existing applications may already use a field with that name.
   This is the same kind of problem as the `sys_date` section of this file.
4. **How does it relate to the audit trail?** A state transition is itself an audit event; check it against the
   classification axes of [ADR-040](../adr/adr-040-audit-trail-taxonomy.md) to decide which category it falls in.

**What to wait for**: there is no technical prerequisite; it is purely a question of product direction and ordering.

**First step when it starts**: answer question 1 first, then write the plan.
