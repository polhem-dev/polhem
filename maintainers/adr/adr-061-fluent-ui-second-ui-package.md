# ADR-061: Fluent UI is the second Blazor UI package

## Status

**Accepted (2026-11-03)**

## Context

[ADR-060](adr-060-mudblazor-first-ui-package.md) made MudBlazor the first UI package and said a second framework would be
one new package over the same bases. `Polhem.Web.Blazor.Fluent` tests that claim on Fluent UI Blazor, for applications that
want Microsoft's design system. The bases and the behavior tests did carry over unchanged in what they require. The markup
did not take the same shape, because Fluent UI v5 is built on web components.

## Decision

### 1. Fluent UI v5, pinned to an exact version

v4 is on an archive branch, so a new package does not start there. v5 has its first stable release in September 2026 and is
supported on a best-effort basis through GitHub, so the package pins one exact version and an upgrade is verified at run
time (`.claude/rules/blazor.md`).

### 2. Native inputs where text must reach the server at a key press

A Fluent input keeps its `<input>` in a shadow root and reports its text when it is left. Three places use a native input:

- **Sign-in**, because a password manager has to see the inputs and their `autocomplete`.
- **The column filter and the lookup search**, because Enter has to submit what was typed. A form does not submit on Enter
  from a Fluent input, the component's own key hooks register a round trip later, and text typed just after the focus reached
  a server-focused input could be overwritten. In the filter that applied an empty filter while the panel closed as usual:
  once in CI, and once in 60 runs under CPU load. After the change there was none in 150 runs under the same load.

The field editors stay Fluent inputs. Typing at once after a click, a focus or a Tab lost nothing in 250 rounds of each
under the same load, on fields already rendered. A feature that focuses a just-rendered field has to measure this again.

### 3. The shell is plain elements

It needs a header, a menu and a page, and its open state and breakpoint are those of `ShellLayoutBase`. Plain elements leave
one less component to verify at each upgrade.

### 4. Each workaround stays visible in the package

Fluent UI 5.0.1 needs these, each marked `WARNING:` where it is:

| Where | Why |
|---|---|
| `PolhemDataGrid` | With resizable or reorderable columns, the grid calls a disposed script module when removed during `OnAfterRenderAsync`, which ends the circuit. An error boundary that stays on the page does catch it, measured on 2026-11-03, but then shows its error content for a grid that was only leaving. Upstream: [fluentui-blazor#5580](https://github.com/microsoft/fluentui-blazor/issues/5580). |
| `PolhemDialog` | An inline dialog raises no `OnStateChange`, stays inside the surrounding `FormScope` (a provider's dialog leaves it), and `Modal` renders a type that a click outside does not close. |
| `GridColumnFilter` | The popover opens only when `Opened` turns true on an element already rendered. |
| `GridControl` | A Fluent input writes its value back just after it takes the focus, so held keys replay 50 ms later. This was measured and not traced to its source: 16 ms held and 0 ms did not. |
| `DateEdit`, `DateTimeEdit` | The picker has no callback for text it cannot parse, so leaving recreates it after a 250 ms wait for the focus to land. |

## Consequences

- An application chooses between the two UI packages when it starts. Definition-driven pages move by changing the package
  and the `@using` lines. Pages written directly against a framework do not, as ADR-060 says.
- Every behavior change is written twice in markup, and both frameworks are verified at each upgrade.
- The first load of 200 rows by 15 columns sends about 1.98 MB against MudBlazor's 1.82 MB, measured once on a spike.
  `FluentGridPerformanceTests` fails above 2.1 MB, against 1.9 MB for MudBlazor.
- Buttons in the grid toolbar and the filter use text symbols, to avoid the separate icons package.
- The fixed timings above are the weak points: they hold on what was measured, and nothing tells when they stop being needed.
- Northwind's Fluent head runs on Blazor Server. WebAssembly was only exercised on a spike with text and numeric cells.
  (Update, 2026-11-03: `Polhem.Northwind.BlazorFluentWasm` now runs the package on Blazor WebAssembly, and the demo
  site serves it at `/fluent-wasm/`.)
