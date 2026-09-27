# Load test measurement rules

How to use `tools/Polhem.LoadTests` and the discipline for taking numbers. **A maintainer document, not a public
document**: its readers are polhem maintainers, not framework users.

The complete list of settings and what each one means is **not in this file**: the authority is
`tools/Polhem.LoadTests/loadtest.sample.json` (every item has a comment) and the XML docs of each settings type. This
file covers only "how to run it" and "how to read the numbers".

---

## Prerequisites

1. **The database containers** are running (the set used by `./test.sh`; the container names are in the header of
   `test.sh`).
2. **Connection strings** are provided as `POLHEM_TEST_CONNSTR_{DBTYPE}`, the same variables as `./test.sh`.

   **Exception: the connection string must contain `{@DbName}`**, which is the only place the `loadtest_` prefix can
   take effect. A connection string without it (Oracle's points to a service, not a database) makes every category
   resolve to wherever that string already points, which is the unit tests' own schema, and the run would create
   tables and write data there. Such a string is rejected outright. The remedy is to set
   `POLHEM_LOADTEST_CONNSTR_{DBTYPE}` separately, pointing to a schema reserved for load testing. When that variable
   exists it takes precedence and this check is skipped (it amounts to the operator stating "this one is for load
   tests to write to").
3. **`prepare` has run once**: it creates the load-test databases, creates the tables, and seeds the accounts and
   data.

### Oracle: create a dedicated schema first

The other four isolate by swapping the database name through `{@DbName}`; Oracle cannot, because its connection
string points to a service. Isolation can only come from **another user/schema**, so create one by hand first (this
needs the `CREATE USER` privilege):

```bash
docker exec -i oracle23ai sqlplus -S 'sys/<ORACLE_PWD>@localhost:1521/FREEPDB1 as sysdba' <<'SQL'
create user loadtest identified by <password>;
grant connect, resource to loadtest;
alter user loadtest quota unlimited on users;
exit
SQL
```

`<ORACLE_PWD>` is the container's `ORACLE_PWD` environment variable (readable with `docker inspect`); this file does
not copy it. Once it is created, point `POLHEM_LOADTEST_CONNSTR_ORACLE` at it, and `prepare` builds all its tables and
the seed data there, without touching `testuser`:

```bash
export POLHEM_LOADTEST_CONNSTR_ORACLE='Data Source=localhost:1521/FREEPDB1;User Id=loadtest;Password=<password>;'
```

After the run it is worth checking that the isolation really held (`ft_customer` of `testuser` should stay at the
unit tests' seed row count).
**This step is not a formality**: it was exactly because this path was not verified that a load test once wrote a
hundred thousand rows into `testuser`.

```bash
export POLHEM_TEST_CONNSTR_SQLSERVER='...'
dotnet run --project tools/Polhem.LoadTests -c Release -- prepare
dotnet run --project tools/Polhem.LoadTests -c Release -- run
```

Remote mode also needs the server started first (in another terminal):

```bash
dotnet run --project tools/Polhem.LoadTests -c Release -- serve
dotnet run --project tools/Polhem.LoadTests -c Release -- run --mode Remote --endpoint http://localhost:5199/api
```

`--vu` / `--duration` / `--warmup` / `--mode` / `--endpoint` / `--protection` can override the settings file; the
full list of flags is in `--help`.

## Two hard exclusions

### No SQLite

It is positioned as a file-based, single-machine / embedded database, not a server option, and its global write lock
would show a bottleneck on concurrent writes that does not exist in practice.
Settings validation rejects it outright, with a message that explains why.

> **Do not use "SQLite takes a different code path" as the reason.** Its unique differences are concentrated in the
> DDL layer, while the load test hits the DML hot path, and on that path `SqliteProviderFactory` deliberately brings
> it in line with the other providers.

### Not in CI

Load test numbers on a CI runner are too noisy. As a gate they would only produce flakiness, and a flaky gate ends up
ignored or turned off.
Trigger it by hand and record the results in documents.

## Measurement discipline

- **Warm-up cannot be skipped.** Caches load on first use, so percentiles without warm-up describe a cold start.
- **Official numbers are taken on a real server provider**, SQL Server by default.
- **Read the error count together with the latency.** A report in which half the calls fail fast looks great if you
  only read latency. In practice there was once a whole round with 0 successes and a latency column of all 0.
- **The scenario the report names is not necessarily what is broken.** When the error count is so large that it has
  nothing to do with the workload (millions, tens of millions), it is almost always **something failing instantly
  and spinning at full speed under the closed model**, not that scenario really being called that many times.
  Read the messages in `ErrorSamples` first; do not start from the scenario name. The login case has been fixed (a
  failure now aborts before the run, see below), but other causes can still show up in this shape.
- **A login failure now stops before measurement.** The message names the VU and the account, the exit code is
  non-zero, and **no report is produced**. When you see it, a prerequisite is not in place: check first that
  `prepare` has run and that `auth.*` matches the seeded accounts. Do not look at the scenarios.
- **Know which model you are measuring.** The default is the closed model (each VU waits for the previous call to
  return before sending the next), so the send rate drops as the system slows down, and it therefore **does not**
  show the tail latency collapse that an open model finds. It is close to how people use a business application, but
  to find the saturation point you need to switch models.

## Reports

Three layers of output: console, Markdown and JSON, written by default to `artifacts/loadtest/` (gitignored).

**Copy the ones worth keeping into this directory by hand**; the rest need not be kept, since most runs are
exploratory.

### Writing numbers so they do not drift

Always record results as "**what was measured at the time**". The metadata section of the report already carries the
version (including the commit), provider, mode, machine and load shape. When you copy a report here, **copy the
metadata with it**; do not extract only the latency numbers.

**Do not write "this framework's throughput is X"**: that is a copy, it is bound to drift, and no mechanism will
notice when it is out of date (see `.claude/rules/single-source.md`). When performance characteristics must be
stated externally, promote them to an ADR or a public document.

## Known limitations

Things you need to know when reading a report:

| Limitation | Effect |
|------|------|
| **Oracle needs a dedicated schema** | Its connection string has no `{@DbName}`, so the `loadtest_` prefix has nowhere to take effect and it is rejected by default. To run Oracle, first prepare a dedicated schema and point `POLHEM_LOADTEST_CONNSTR_ORACLE` at it; see "Oracle: create a dedicated schema first" above. |
| **A Remote run cannot measure the cache** | The counting provider is in the driver's process, while the cache being exercised is on the server. The report marks it `Not observed`, and the JSON carries `CacheObserved: false`. Use Local mode to measure cache behavior. |
| **The BO binding of `Order` is removed** | That set of definitions binds `Order` to the demo server assembly, which the driver does not reference (referencing it would fold the application's business logic into numbers meant to "measure the framework"). That program therefore falls back to the framework's own implementation, and the report lists it under `Dropped bindings`. |
| **Seeded relation fields are not real foreign keys** | Each table is seeded independently, and relation fields get generated values. That is enough for read scenarios, which measure the query itself; a scenario that needs master and detail to match must seed its own data. |

## Related

- [plan-load-testing.md](https://github.com/jeff377/bee-library/blob/7d6cc9d9/docs/plans/archive/plan-load-testing.md) (bee-library): design decisions and how they were derived (an archived working document that records what was intended at the time, not current behavior)
- `tools/Polhem.LoadTests/loadtest.sample.json`: the authoritative source for the settings
- `.claude/rules/testing.md`: the unit test rules (unrelated to this file; do not mix them up)
