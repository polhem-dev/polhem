# Pitfall log: Avalonia controls

Pitfalls proven in `tools/DefineEditor`, `src/Polhem.UI.Avalonia` and `apps/Polhem.Northwind` (Avalonia 12 +
Semi.Avalonia). The matching hard rules are in `.claude/rules/avalonia.md`. **Scan this before changing a control.**

## Layout and hit-testing

1. **`HorizontalAlignment=Stretch` + `MaxWidth` centers the element.** When the available space exceeds MaxWidth, the
   element is centered in the remaining slot, not aligned left. "Fill but cap the width" while aligned left cannot be
   done at the Avalonia element level; DocumentStyles solved it by removing MaxWidth and filling the full width, and
   moved the width-cap requirement to a fixed Width on `.row.short`.
2. **`Background = null` does not take part in hit-testing.** When a `ContentControl` / panel has no background, only
   the literal content is clickable, and clicks on the blank area next to it pass straight through. To make the whole
   area clickable, set `Brushes.Transparent`.
3. **Semi's ComboBox / DatePicker do not stretch by default** (TextBox stretches by default). The field width then
   follows the content, and the read-only underline cannot extend to the full width. As with TextBox, set
   `HorizontalAlignment=Stretch` in the ctor so the container decides the width.

## Inheriting built-in controls

4. **`StyleKeyOverride` is mandatory.** By default a subclass looks up its ControlTheme by its own type, and
   Semi/Fluent only provide themes for the built-in types → without the override the **whole control is invisible**.
5. **If code-behind accesses an `x:Name` control → you must use the source-generated `InitializeComponent`.**
   The ctor only calls `InitializeComponent()`; **do not write your own**
   `private void InitializeComponent(){ AvaloniaXamlLoader.Load(this); }`. The hand-written version only loads the
   XAML and does not assign the named control fields → the fields are null → NRE when accessed in `OnLoaded`/events
   (exit 134 SIGABRT).
   **A View that does not access named controls passes with either version and never blows up**, so it is easy to
   carry the hand-written version along when copying a template.
   Follow the pattern in `samples/Avalonia.DemoCenter` / `tools/DefineEditor`.

## Events and data flow

6. **A control's "semantic events" do not fire for values set by code.** `TextBox.TextChanged`,
   `DatePicker.SelectedDateChanged` and `CalendarDatePicker.SelectedDateChanged` are only guaranteed to fire on user
   interaction; values set by code are often silent. To listen reliably, always hook `PropertyChanged` and compare
   against `TextProperty` / `SelectedDateProperty` (that is also the only way headless unit tests can test it).
7. **DataGrid does not observe changes to a `DataView`.** After rows are added to or removed from a `DataTable`, the
   `DataView` is updated, but the realized DataGrid rows do not move (it does not listen to `ListChanged`). After any
   change to the row collection, reset `ItemsSource` to re-realize.
8. **ADO.NET `DataRow.BeginEdit` does not suppress `ColumnChanged`** (and an `EndEdit` with no changes still raises
   `RowChanged`). "Buffered edit, cancel with zero events" requires tracking the row being edited yourself and
   silencing it at the event layer; the row edit protocol of `FormDataObject` is this pattern.

## DataGrid and template recycling

9. **The DataGrid edit pipeline structurally conflicts with popup editors.** With a `ComboBox` / `DatePicker` in
   `CellEditingTemplate`, focus leaves the cell as soon as the popup opens → the edit template is torn down. The fix is
   **click-to-swap** (the CellTemplate manages the swap itself, and the column is marked `IsReadOnly` so the pipeline
   stays out; see ADR-021). A bonus pitfall on the same stage: setting `IsDropDownOpen=true` on the swapped-in
   ComboBox immediately gets closed by the later events of the same click, so defer it with `Dispatcher.Post`;
   `DatePicker` has no public API to open it, so use `FindDescendantOfType<Button>` to trigger the Click of the
   template's flyout button.
10. **`FuncDataTemplate` with content computed once + `supportsRecycling:true` → the display drifts away from the
    underlying row** (see ADR-022).
    If a cell template computes `Text` once at creation (`Text = FormatCell(row,...)`, not a binding), it **cannot**
    enable recycling: when the DataGrid recycles a presenter across rows it does not rerun the creation delegate, it
    only swaps the DataContext, and the precomputed Text stays on the old row.
    **Symptom**: in the lookup picker it shows up as "you see one row, click it, and get a different row back". **The
    selection and the write-back are actually both correct; only the display layer is wrong**, so it is easy to
    investigate in the wrong direction. **Highly latent**: with little data and no scrolling, each cell is built once
    at first and the display is correct; it only blows up after opening the window several times or scrolling.
    **Fix**: a cell with precomputed content always uses `supportsRecycling:false` (the other cell templates of
    `GridControl` were already false; only the plain-text cell slipped through). Switching to a real binding is not
    possible: Avalonia binding does not support the string indexer of `DataRowView` (ADR-020).
11. **ComboBox + a recycled template leaves the selection box blank.** When `ItemTemplate` uses
    `FuncDataTemplate(supportsRecycling: true)`, the same control instance is handed to both the drop-down list item
    and the selection box (a control cannot have two parents), so after you pick a value and the list closes, nothing
    is shown. Fix: use `DisplayMemberBinding` instead (each container generates its own content).

## Read-only appearance (FormMode switches off the border and keeps the underline)

12. **Bind the read-only visuals to `binder.AllowsEdit(formMode)`, not to `TextBox.IsReadOnly`.** The text box of a
    lookup-style `ButtonEdit` is always `IsReadOnly=true` (it can only be written through the dialog), yet in edit
    mode it is actually editable; binding to `IsReadOnly` makes it show the read-only appearance in edit mode by
    mistake.
    Removing the four borders for the read-only TextEdit family: `BorderThickness=(0,0,0,1)`,
    `Background=Transparent`, and `BorderBrush` set to a fixed light gray (`#80808080`); leaving read-only restores
    them with `ClearValue`. **The underline color must be set as a local value** to override the theme's hover/focus
    setters, otherwise the underline is invisible at rest.
13. **DatePicker / ComboBox cannot drop the border in read-only mode through setters; the template must be replaced,
    but a bare replacement blows up.**
    `DatePicker.OnApplyTemplate → SetSelectedDateText()/SetGrid()` has **no null guards** and dereferences
    `PART_DayTextBlock` / `PART_MonthTextBlock` / `PART_YearTextBlock` / `PART_*Spacer` /
    `PART_ButtonContentGrid` (NRE if missing); `ComboBox` uses `NameScope.Get<Popup>("PART_Popup")` (throws if
    missing). A hand-written read-only template must **register** these hidden parts.
    **Key point**: for `Popup` / `Button`, `scope.Register` alone satisfies `Find`/`Get`, and they **must never be
    added to the visual tree**: a Popup as a Panel child **freezes the UI** (proven hang).
14. **Semi/Fluent's `:disabled` fades the text through the Foreground brush of the `ContentPresenter`, not through
    overall opacity.** So "gray CheckBox box, title text stays readable" is achievable: `IsEnabled=false` puts the box
    in the disabled gray (`Border#NormalRectangle` switches to the disabled brush + `Panel#PART_GlyphPanel` opacity
    0.75), and then in `OnApplyTemplate` take `PART_ContentPresenter` and use a **local binding** to pin its
    Foreground back to the control's normal `Foreground` (local has the highest precedence and overrides the theme's
    disabled setter).
    Restoring the ContentPresenter opacity was tried first, then an instance Style + TemplatedParent binding;
    **neither worked**. Only `OnApplyTemplate` + a local binding is stable.

## Test parallelism

15. **The first population of `AvaloniaPropertyRegistry` is not thread-safe.** Under xUnit parallelism, several test
    classes touching a direct property of the same type for the first time at once (such as `DataGrid.ItemsSource`)
    collide on `Dictionary.Add`.
    The test assembly removes it at the root with a single-threaded warm-up in a `[ModuleInitializer]` (construct the
    control + set the direct property once).
16. **`AvaloniaPropertyDictionaryPool` races under parallelism (harder to deal with than the previous one).** When a
    control ctor parents child controls, it goes through `SetInheritanceParent` → `Get/Pop` of the shared pool, which
    is a TOCTOU (a race between the count check and the Pop), and constructing controls in parallel intermittently
    throws `InvalidOperationException: Stack empty`.
    **Different from the previous one**: this is **continuous** pool access, so "warm up once single-threaded" cannot
    resolve it; moving Polhem's own static state into fixtures does not help either (this is inside Avalonia).
    **Symptom**: under xUnit's default parallelism, control tests in the same assembly **fail differently every time**,
    and about 1/3 of the runs blow up.
    **Root fix**: for an assembly that is "almost entirely control tests", add
    `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
    (already added to `Polhem.UI.Avalonia.UnitTests`; running serially takes <1s, a negligible cost).
    It only surfaces when adding many control tests pushes the parallel pressure past the threshold (in the Northwind
    RecordView round, going from 224 to 236 tests made it blow up).

## UI automation

17. **Do not open a freshly compiled app with computer-use's `open_application`.** Launch Services resolves the bundle
    id to the old `publish/*.app` output, not the freshly compiled executable in `bin/{Debug,Release}/net10.0/`.
    Several rounds of useless diagnosis were once done against the old version of the app because of this.
    When you really do drive it with computer-use (as the `demo-smoke` skill does): `dotnet <dll>` or a bare apphost
    process is **not** recognized by `request_access` (it has no bundleID, LaunchServices shows it as "Avalonia
    Application", and every name match fails) → it must be wrapped in a minimal `.app` (`Contents/MacOS/` +
    `Info.plist` with a custom `CFBundleIdentifier`); after `open`, call `request_access` with the **bundle id**; after
    changing code, rebuild and `cp -R` it back into the bundle.
    When another app jumps to the foreground it blocks clicks; use `osascript` System Events to set it frontmost by
    pid and bring it back.

    > User preference: **a change that compiles can be delivered; the user starts it and tests it themselves** (driving
    > the UI with an agent is too slow).

## Single-view overlays (phones, browser)

18. **A child's `MinWidth` beats the card's `MaxWidth`, so a phone clips the overlay on both sides.** The lookup
    panel once carried `MinWidth = 420` for its desktop window; in the overlay card on a 402-wide iPhone the card
    measured to the child's minimum, stayed centered and was cut off left and right. Panels hosted by
    `OverlayDialogHost` carry no `MinWidth` of their own: the preferred width is passed to the host, which clamps it
    to the visible width (`OverlayDialogHost.ComputeLayout`), and the desktop window path sets it on the `Window`.
19. **On iOS the on-screen keyboard covers the overlay; the top level does not shrink for it** (observed on the iPhone
    simulator, 2026-09-28: the row-edit OK / Cancel buttons sat under the keyboard with no way to reach them). The
    overlay host listens to `TopLevel.InputPane` and moves the card's bottom limit above `OccludedRect`, and the
    hosted panels dock their buttons outside a scrolled body, so the height the keyboard takes comes out of the
    scrolled part.
