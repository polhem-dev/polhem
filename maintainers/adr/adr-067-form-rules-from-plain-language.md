# ADR-067: A form rule can be written from plain language, and the engine, not the model, vouches for it

## Status

**Accepted (2026-11-19)**

Builds on [ADR-028](adr-028-expression-rule-engine.md) (the expression rule engine),
[ADR-064](adr-064-document-form-drafting.md) (drafting behind `IChatClient`) and
[ADR-066](adr-066-detail-aggregates-in-expressions.md) (detail aggregates). Tracked in
[#568](https://github.com/polhem-dev/polhem/issues/568).

## Context

A `FormRule` stops a save with a message when its `Condition` is false. The people who know the rules of a business,
such as "an approved order needs an approver", are rarely the people who can write `!IsBlank(approver_id)`, so a rule
waited for a developer. Two things stood between them and the rule: a function vocabulary close enough to how the rule
is said, and a way to turn their words into an expression they can trust without reading it.

A language model can write the expression. What it cannot do is be trusted: it may misread a field, invert a
comparison, or write an expression that reaches far past the form. An expression is also code that runs on the server
at every save.

## Decision

### 1. Functions compute from the form's own values only

The business functions (`IsBlank`, `Between`, `In`, `DaysBetween`, `MonthEnd`, `RegexMatch`) compute from their
arguments alone. None reads the database, the session or the value a field had before the edit.

- The same expression runs in the clients' live computation and in the server's save. The clients have neither the
  database nor the stored row, so a function that read either would give a different answer on each side, or none.
- A function of its arguments can be evaluated over sample values, which is what makes decision 3 possible.
- A rule that needs stored state, such as an allowed status transition, stays in the business object's
  `DoBeforeSave`, as ADR-028 left it.

Change detection ("the amount may not change once approved") was considered and left out for these reasons. It would
need the original row, which only the server has, and which an added row does not have at all.

### 2. The function catalog is the single source

Each helper function is registered once, in `DynamicExpressoEvaluator`, with its summary and an example
(`ExpressionFunction`). DefineEditor's expression editor, the function list given to the model and the expression rules
document all list from it. The document's descriptions are written by hand; `HelperFunctionDocsTests` fails when the
document and the catalog name different functions.

### 3. The model proposes; the engine decides

`RuleDrafting` (in `Polhem.Definition.Drafting`) gives the model a single tool, which submits a rule. The rule's message
comes with translations, and the submission carries sample rows, each with the outcome the model expects (passes,
blocked, or does not apply), at least one passing and one blocked. A submission is accepted only when two checks hold:

- its expressions pass `FormExpressionCalculator.ValidateUntrustedExpression`;
- the expression engine computes, for every sample, the outcome the model claimed.

Otherwise the reasons go back to the model, which tries again within a bounded number of rounds.

The samples a person sees are therefore the engine's results. Someone who cannot read the expression checks the rule
by rows such as "shipped, no shipper: blocked". Explaining an existing rule works the same way: the explanation is the
model's, the samples are computed.

As in ADR-064, the feature knows only `IChatClient`, and the tool is declared, not invoked by the client. Only
DefineEditor uses it for now. It is a design-time tool, and its credential is the developer's own.

### 4. The allowlist is enforced on untrusted text and only warns on definition files

Parsing an expression is not a sandbox: members are resolved by reflection. `ValidateUntrustedExpression` checks the
text before parsing (length, nesting, literal length), because deep nesting overflows the parser's stack and ends the
process. It then accepts only:

- the form's fields;
- the catalog's functions;
- the detail aggregates;
- operators and literals;
- a fixed list of members.

`RegexMatch` is refused there. The cost of building its matcher is decided by the pattern, and two hand-written
scanners meant to bound it were both bypassed in review.

The check is enforced for text from an untrusted author: a model's output today, and rules a customer configures in the
future. A packaged definition file only gets a warning, in `ValidateExpressions` and DefineEditor. Its author is the
developer, remote callers cannot write it (`SaveDefine` is `LocalOnly`), and turning the warning into an error would
reject definitions that work today. Changing that later follows the versioning policy of
[ADR-046](adr-046-api-evolution-policies-for-1-0.md).

### 5. Message translations go into the languages the form already has

The model writes the message in English, the base text of the FormSchema. It also translates the message into every
language the form already has a packaged Language file for. Applying a draft in DefineEditor writes the translations
under `Rule.{RuleId}.Message`. A new language file is never created, and a customization folder is never written: which
languages a solution supports, and what a customer overrides, are not the model's decisions.

## Consequences

- A person who knows the rule can write it in DefineEditor, and check an existing rule by its explanation and computed
  samples.
- An expression a model writes is held to a narrower language than a developer's. A rule it cannot express within the
  allowlist, or one that needs the database, still needs a developer.
- Rules a customer configures in the application, kept apart from the packaged FormSchema and checked alongside its
  rules, are expected to reuse `RuleDrafting` and the allowlist; they are not built yet. The evaluator caches every
  parsed expression, so admitting expressions from many authors will need a bound on that cache.
- Rules can read values a save does not write: a row the caller marks unchanged, a `ref_*` field, and, on a form
  without a permission model, detail rows of another record. This was accepted rather than fixed
  ([#577](https://github.com/polhem-dev/polhem/issues/577)): forging it takes a signed-in user writing their own
  payload, and a rule is meant to catch mistakes made through the clients. A check that must hold against such a caller
  belongs in the business object.
