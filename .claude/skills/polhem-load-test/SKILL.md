---
name: polhem-load-test
description: Run load tests against the polhem framework with tools/Polhem.LoadTests and interpret the results — pre-flight checks (containers, connection strings, prepare), the Local / Remote modes, and the discipline of "when the numbers cannot be trusted" (parameters too small, error counts ignored, the closed model's blind spot for tail latency, Remote cannot measure the cache). Use when the user wants to "run a load test", "stress test", "load test", "measure performance", "performance test", "will this change make it slower", "measure throughput / latency", and similar requests. **Only runs and interprets; does not tune.** It also does not change parameters or source code to make the numbers look good.
---

# polhem load test execution

**The authoritative source for operation and policy is `docs/repo-ops/load-testing.md`**, the list of settings is
`tools/Polhem.LoadTests/loadtest.sample.json` (every item is commented), and the flags are `--help`.
This file does not duplicate those; it holds only **the judgement and discipline needed while running** — that is,
the part an agent most easily gets wrong.

---

## 1. Pre-flight checks (in order, do not skip)

### Docker daemon

```bash
docker ps
```

On failure, **tell the user to start Docker Desktop; do not run `open -a Docker` yourself**.
If the container exists but is stopped, do not `docker run` a new one yourself either — image version / port / volume
all have constraints.

### Connection string

The console app **does not read `.runsettings`**; you must use environment variables:

```bash
export POLHEM_TEST_CONNSTR_SQLSERVER='...'
```

The value can be taken from `.runsettings`. When it is missing, the tool's error message names the variable and how to
fix it; just follow it.

### prepare

Run it the first time, or after changing `seed.rowCount` / `auth.userPoolSize`:

```bash
dotnet run --project tools/Polhem.LoadTests -c Release -- prepare
```

It is idempotent; re-running is safe.

## 2. Running

Local (measures BO + Repository + DB):

```bash
dotnet run --project tools/Polhem.LoadTests -c Release -- run --vu 20 --duration 120
```

Remote needs two terminals: first `serve`, then `run --mode Remote --endpoint ...`;
the full commands are in `docs/repo-ops/load-testing.md`.

## 3. Interpretation discipline

**This section is the reason this skill exists.** For the previous two sections, just follow the docs; this section is
about what comes after you have the numbers.

### Look at the error column first, then latency

A report in which half the calls fail fast will have very pretty latency. **When reporting results, error counts and
latency must be shown side by side**; never summarise latency alone. This has happened: in the run where every Login
failed, the latency columns were all 0.

When the error type name is not enough to diagnose, the report's `ErrorSamples` keeps one message per type; read that
first.

### Numbers from parameters that are too small support no conclusion

`--vu 4 --duration 5` is for checking "it runs", not for drawing conclusions.
To answer "will this change make it slower", VU count and duration must both be large enough for the numbers to
stabilise, and **run the control group with the same parameters**.

**The absolute numbers from a single run mean almost nothing** — what means something is a before/after comparison on
the same machine with the same parameters.

### The closed model cannot see tail-latency collapse

By default each VU waits for its previous call to return before sending the next, so the send rate drops as the system
slows. Therefore **it will not show the saturation point that an open model can find**. To find the saturation point
you must switch models; do not use the closed model's p99 to claim "the system is fine at this load".

### Remote cannot measure the cache

The counting provider lives in the driver's process, while the cache being exercised is on the server. The report marks
it `Not observed`; **that means "not measured", not "0% hit rate"**. To measure cache behaviour, use Local mode.

### Save reports together with their metadata

Reports are written to the gitignored `artifacts/loadtest/`. Copy the ones worth keeping to `docs/repo-ops/`,
**together with the metadata section** — if only the latency numbers are excerpted, nobody will later know under what
conditions they were measured.

Always write it as "what was measured at the time"; **never write "the framework's throughput is X"** (a copy always
drifts; see `.claude/rules/single-source.md`).

## 4. Common failures and how to handle them

| Symptom | Cause and handling |
|---------|--------------------|
| `Environment variable 'POLHEM_TEST_CONNSTR_*' is not set` | Not exported; set it as the message says |
| Many `HttpRequestException` 401 | Remote mode is missing `X-Api-Key`; the configured `target.apiKey` was not sent |
| Every scenario fails and the message points at DI resolution | The backend cannot start; run `verify` first to isolate the problem |
| `Unknown scenario 'X'` | Scenario name misspelled in the settings file. **This is deliberately not skipped** — silently skipping would produce a report that looks complete |
| Container not up | See the pre-flight checks; **do not change tests or source code to make it "pass"** |
| `has no {@DbName} placeholder` | That provider's connection string cannot be isolated by database name, so the load test would write into the unit tests' schema; it is therefore rejected outright. **Do not work around it** — as the message says, set `POLHEM_LOADTEST_CONNSTR_*` to point at a dedicated schema |

The `verify` command starts the backend, resolves services, reads one FormSchema and tears it down, to separate
"the backend has a problem" from "the scenario has a problem".

## 5. What this skill does not do

- **Does not tune parameters or change source code to make the numbers look good.** Ugly numbers are a signal, not
  something to eliminate.
- **Does not put load tests in CI.** Runner noise is too high; as a gate it would only create flakiness.
- **Does not run SQLite.** Settings validation rejects it outright; the reason is in the docs. Do not work around it just
  to "make it run".
- **Does not tune.** Once a bottleneck is measured, whether and how to tune it is a later decision; report back to the
  user to decide.

## Related

- `docs/repo-ops/load-testing.md` — the authoritative source for operation and policy, including known limitations
- `tools/Polhem.LoadTests/loadtest.sample.json` — the authoritative source for settings
- `.claude/rules/testing.md` — unit test policy (unrelated to load testing; do not mix them up)
