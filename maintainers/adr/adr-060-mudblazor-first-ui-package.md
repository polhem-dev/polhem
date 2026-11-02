# ADR-060: MudBlazor is the first Blazor UI package, with no generic UI abstraction

## Status

**Accepted (2026-11-01)**

## Context

The Blazor head drew its controls in plain HTML, with a stylesheet of its own. Keeping that up meant building a date
picker, a dialog, a data grid and a menu by hand, and they lagged what the Avalonia head gets from its control library.
[ADR-059](adr-059-blazor-shared-layer.md) split the head into a shared layer, UI packages and host packages so a mature
framework could take over the markup. This record covers which framework, and what the split deliberately does not do.

## Decision

### 1. MudBlazor comes first, pinned to an exact version

`Polhem.Web.Blazor.Mud` builds on MudBlazor and pins one exact version. Its density stylesheet and some of its tests
depend on MudBlazor's internal class names, so an upgrade is a decision with its own verification
(`.claude/rules/blazor.md`), not a side effect of a restore.

### 2. The HTML controls are removed, not kept as a second UI package

`Polhem.Web.Blazor.Server` is now only a host. Keeping the HTML controls would mean maintaining two sets of markup and a
public class-name contract for a look nobody chooses over the other. Applications move to a UI package.

### 3. No generic UI abstraction

There is no `IGrid`, `ITextInput` or similar. The shared layer holds behavior as abstract component bases
(`FormPageBase`, `GridControlBase`, `TextEditorBase` and so on), and each UI package writes its own markup over them.

An abstraction over controls would have to cover what every framework's grid, dialog and picker have in common, which
is little, and would then be what every package fights. The bases share what is actually the same, the rules, and leave
what differs to the package. The behavior tests in `tests/Polhem.Web.Blazor.BehaviorTests` hold the packages to the same
behavior instead.

### 4. Pages an application writes directly with MudBlazor stay MudBlazor pages

Definition-driven pages (`FormPage`, the shell) move to another UI package by changing the package and the `@using`
lines. A page an application writes with MudBlazor components does not carry over. That is the price of not having an
abstraction, and it is stated in the package README so an application chooses knowingly.

### 5. Cells open their editors on focus, and keys wait for them

A grid with an editor in every cell costs too much on a Blazor Server circuit, so a cell shows text until it takes the
focus. Opening an editor takes a round trip, so `polhem-grid-editor.js` queues keys, Tabs and a click elsewhere until it
arrives and replays them in order, which writes the text before the click runs. `GridEditingSafetyTests` runs this in a real browser over a slow link, because bUnit runs no script.

## Consequences

- A second UI framework is one new package over the same bases, and derives from the same behavior tests.
- Upgrading MudBlazor can break the density stylesheet without any build error, which is why it is verified at run time.
- The first load of a large grid sends more than the HTML grid did: 1.76 MB against 1.53 MB for 200 rows of 15 columns,
  measured once on 2026-11-01, while a circuit held less memory (30 to 36 MB against 53 to 59 MB).
  `GridPerformanceTests` fails above 1.9 MB.
- Grid rows are not virtualized, and neither are the phone cards. Measured on 2026-11-02 on 200 rows by 15 columns, the most a
  list page holds: the first load sends 1.82 MB, and moving to the next editor about 6 KB. Over a link of 250 ms each way
  that move takes about a second, nearly all of it waiting for the round trips, which virtualizing would not change. It
  would also put an open editor at risk when its row scrolls away. The cards instead stop at 1000 rows, down from 5000,
  which measured 18.2 MB and 131 MB of managed memory; at 1000 they measured 0.84 MB and 30 MB. Revisit this if a list
  needs far more rows on one page.
- On WebAssembly the circuit's cost goes away, and the same limit of 1000 cards still applies. Going from 400 cards to
  1000 took 3.2 s on localhost, measured once on 2026-11-02, about a second per 200 cards. Raising it is a separate
  decision, with no measurement yet of the browser at 5000.
- MudBlazor renders the table header itself, so `aria-sort` is not set on it. The direction shows in the caption.
- A key pressed before the focus reaches a dialog goes to the page behind, so on a slow link an early Escape has to be
  pressed again.
