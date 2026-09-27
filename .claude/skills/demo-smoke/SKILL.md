---
name: demo-smoke
description: Run one round of end-to-end smoke testing against a demo project (`samples/<Name>/` or `apps/<Name>/`) (clicking UI elements through computer-use and verifying key screens) to declare that the demo runs locally. Reads the `.smoke.yaml` configuration file at the project root. Use it when the user asks to "run the demo", "verify the sample", "does the demo run", "smoke test sample" and similar.
---

# Sample demo end-to-end smoke test

Run a reproducible UI flow check against a demo project (`samples/<Name>/` or `apps/<Name>/`). The whole flow
**observes from the outside**: start the dependencies (backend server etc.), open the app, simulate clicks, check
screenshots, clean up. What each demo's "success screen" looks like is written in `.smoke.yaml`; this skill is the runner.

## When to use

- After changing Polhem.UI.Avalonia / Polhem.Web.Blazor.Server, to confirm the samples still run
- A final sanity check after adding a new sample
- Smoke-testing all samples at once before a release

## When not to use

- Unit tests (use `dotnet test`)
- Full UI E2E testing (use a real Playwright / Selenium / Appium toolchain; this skill is "quick look" level)
- Samples without a GUI (for Console, grepping stdout is enough; no computer-use needed)

## Prerequisites

- The main macOS development machine (computer-use drives its desktop apps and browser)
- The `claude` desktop app has computer-use MCP permission
- The sample has already been built (this skill does not build)

## Configuration file: `<project-root>/.smoke.yaml`

Every demo with a GUI carries its own `.smoke.yaml` (find them with `find samples apps -name .smoke.yaml`). Example,
abridged from `apps/Polhem.Northwind/.smoke.yaml` (the real file continues into the Orders list and an order record):

```yaml
# apps/Polhem.Northwind/.smoke.yaml
name: Polhem.Northwind
display_name: Polhem.Northwind

# Dependency processes to run before starting the demo
prerequisites:
  - id: server
    cwd: apps/Polhem.Northwind/Polhem.Northwind.Server
    cmd: dotnet run --configuration Debug --no-build
    ready_when: "Now listening on"   # ready only once stdout shows this string
    timeout_seconds: 30

# How the demo app itself is launched
launch:
  app_name: Polhem.Northwind
  # The Avalonia desktop head produces an ordinary .NET executable (no .app bundle on macOS, no MSIX on Windows),
  # so the built host DLL is launched directly with dotnet; the smoke run does not depend on the dotnet run file-watch loop.
  launch_cmd: dotnet apps/Polhem.Northwind/Polhem.Northwind.Desktop/bin/Debug/net10.0/Polhem.Northwind.Desktop.dll
  # If the project produces an .app bundle, use instead:
  # bundle_path: <project>/bin/.../<App>.app   # launched with macOS `open` by default

# Click and verification flow
flow:
  - step: take initial screenshot
    action: screenshot
    expect_text:                          # strings that must at least be visible (via OCR / screenshot comparison)
      - "Polhem.Northwind"
      - "Endpoint"
      - "Connect"

  - step: click Connect
    action: click
    # target describes the button; the runner can use screenshot coordinates, the accessibility tree, or a label fallback
    target: { label: "Connect" }
    wait_after_seconds: 4                 # wait for the reachability check + ping
    expect_text:
      - "Sign in"
      - "User ID"

  - step: click Sign in
    action: click
    target: { label: "Sign in" }
    wait_after_seconds: 6                 # wait for the RSA handshake + Login
    expect_text:
      - "Master Data"                     # menu group
      - "Transactions"
      - "Beverages"                       # first seeded category

  - step: final screenshot
    action: screenshot
    save_as: northwind-category-list.png  # for manual confirmation

# Cleanup
teardown:
  kill_app: true                          # kill the app process at the end
  kill_prerequisites: true                # kill the server at the end
```

## Execution flow

### Step 1: read `.smoke.yaml`

```bash
test -f <project-root>/.smoke.yaml || {
  echo "This demo has no .smoke.yaml; write one for it first"
  exit 1
}
```

`<project-root>` is `samples/<Name>` or `apps/<Name>`; if unsure, run `ls samples apps` first to match the name.
If it does not exist → stop and ask the user whether to generate one from the template (do not generate it
automatically).

### Step 2: start the prerequisites

For each prerequisite:
- Run `cmd` in the background (use the Bash tool's `run_in_background`)
- Poll the log until the `ready_when` string appears, or fail after `timeout_seconds`
- Record the pid for teardown

If any prerequisite fails → stop, print the log tail, and do **not** launch the app.

### Step 3: call `request_access`, launch the app

```
mcp__computer-use__request_access(
  apps=[launch.app_name],
  reason="Run smoke test for <project-root>"
)
```

Then `open <bundle_path>` (or run `launch.launch_cmd`) and wait 5-8 seconds for the app to bring up its UI.

### Step 4: run each flow step

Run in order:

```python
for step in flow:
  if step.action == "screenshot":
    screenshot()  # take a screenshot; if expect_text is set, do OCR / image comparison
    verify_expected_text(step.expect_text)
    if step.save_as: persist_screenshot_to(step.save_as)

  elif step.action == "click":
    target = locate(step.target)  # via accessibility / label / coordinate
    left_click(target.x, target.y)
    if step.wait_after_seconds: wait(step.wait_after_seconds)
    if step.expect_text: screenshot_and_verify(step.expect_text)

  elif step.action == "type":
    type_text(step.text)
    if step.wait_after_seconds: wait(...)

  elif step.action == "key":
    press_key(step.text)
```

If any step fails (expect_text not visible, target not found, wait timed out) → take a last screenshot, stop the flow,
and go to teardown.

### Step 5: Teardown

```bash
# kill app
pgrep -fl "<Name>" | awk '{print $1}' | xargs -r kill

# kill prerequisites
for pid in "${prerequisite_pids[@]}"; do
  kill "$pid" 2>/dev/null
done

# confirm the port is released (the server's port is in its Properties/launchSettings.json)
lsof -i :<port> -sTCP:LISTEN 2>/dev/null  # should be empty
```

### Step 6: report the conclusion

```
✅ <project-root> smoke passed.
   - prerequisites: 1/1 ready
   - flow steps:    4/4 passed
   - last screenshot: northwind-category-list.png

------ or ------

❌ <project-root> smoke failed at step "click Sign in".
   - prerequisites: 1/1 ready
   - flow steps:    1/3 passed (step #2 timed out waiting for "Master Data")
   - failure screenshot: smoke-failure-2026-05-23.png
```

## Division of labour with other skills

| Skill | Handles |
|-------|---------|
| `run` (global) | Launches the project's app; no scripted verification steps, no teardown |
| **`demo-smoke`** (this skill) | Reads `.smoke.yaml`, starts dependencies + app + clicks + verifies + cleans up |

`demo-smoke` is `run` plus a scripted check and teardown, per demo.

## Known pitfalls

- **pgrep name**: when shipped as an .app bundle, the process name comes from `.app/Contents/MacOS/<AssemblyName>`, not
  the csproj display name; when launched with `dotnet <dll>`, the process name is `dotnet`. Teardown must pick the name
  according to how the app was actually launched.
- **Sandboxed app on first run**: macOS asks the user to approve "network access" and "keyboard monitoring"
  (computer-use clicks). If the dialog appears it blocks the flow; consider adding a `first_run_setup: |` step with a
  prompt in `.smoke.yaml`.
- **Is expect_text OCR or accessibility?**: the MVP uses screenshot comparison + OCR; high-precision needs would use the
  macOS Accessibility API instead (outside the scope of this skill).
- **prerequisites with several dependencies**: if the sample needs something like an SQLite container, write the command
  into prerequisites; this skill does not recognise container tooling.

## Out of scope for this skill

- Writing `.smoke.yaml` itself (there is no generator; copy and paste the template)
- Automatically diffing failure screenshots (pure screenshot comparison is reviewed by a human)
- Batch mode running all samples (if needed, wrap it with `/loop` or `/schedule`)
- Pushing results to a dashboard / SonarCloud / any external reporting
