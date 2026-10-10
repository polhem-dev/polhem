# ADR-032: DateTime uses UTC as the single time zone source, and the Connector is the only conversion point

## Status

Accepted (2026-07-25)

> P0–P3 are fully implemented (2026-07-26). How consumers use it is described in
> [datetime-timezone.md](../../docs/en/database/datetime-timezone.md).
>
> The only verification not carried out: time zone availability on **real** mobile / WASM devices. Each head pins
> `InvariantGlobalization=false` and `InvariantTimezone=false`, but that is a configuration guardrail, not
> verification: a failure caused by missing tz data is a runtime exception on the device, which neither the desktop
> build nor the tests can catch.
>
> **Revisions**: on 2026-09-04 D9 withdrew "deliberately not converted to UTC" and now shares its source with the
> write side; on 2026-09-12 D6 added "the request-direction guard runs before the time zone conversion" and relabeled
> the DTO property rule from an invariant to a writing discipline; on 2026-09-12 D12 added "the basis of 'now' is
> decided by the side the `DataSet` is on", correcting the original description of the residual risk; on 2026-09-12
> D4 added "for cells in the DST fall-back overlap, the Connector remembers the original UTC value".
>
> **Another, larger revision on 2026-09-12**: the request direction no longer converts the `DataSet`, and the
> server-side `Save` does not adopt the `DateTime` values sent by the client (option 5, D14). The openings of D3 and
> D4 now distinguish by direction and carrier, and the overlap memory added earlier the same day was withdrawn with
> it; the related text in D6, D12, D13 and "Consequences" was corrected at the same time.

## Context

The framework has to support cross-time-zone deployments (database times stored as UTC, converted to the user's
time zone for viewing), while keeping the **complexity** that a single-time-zone deployment has to bear to a minimum
(see D10 for what "zero cost" means).

### The current state is not "local time end to end"

An inventory found three time bases in the framework at the same time:

| Basis | Location |
|-------|----------|
| **UTC** | `SessionRepository`, `AccessTokenValidator`, `AuditEntry.LogTimeUtc`, `LoginAttemptTracker`, `PingResult.ServerTime` |
| **DB server clock** | cache-notify's `sys_update_time` (`getdate()` / `LOCALTIMESTAMP` are server local, but SQLite `CURRENT_TIMESTAMP` is UTC). This row is the inventory at the time of the decision; it has since been unified to UTC, see D9 |
| **Local** | Business data default values, trace, the `CreateTime` of definition files |

Two inferences: migrating existing data **must be judged column by column** (`st_session` is already UTC, and
converting everything would convert it wrongly); and the pattern "store UTC in a naive column" **is already proven in
production on five databases** (`st_session` is exactly that), so it does not need to be argued again.

### Measured serialization behavior is the core constraint of this decision

The payload is produced on a `+08:00` side and read on a side with `TZ=America/New_York`:

| Value on the wire | Read back via JSON | Read back via MessagePack |
|------------|----------|-----------------|
| `2026-01-01T09:00:00` (Unspecified) | `09:00` Unspecified ✅ | `09:00` Unspecified ✅ |
| `2026-01-01T09:00:00Z` (Utc) | `09:00Z` Utc ✅ | `09:00` **Unspecified** (Kind erased) |
| `2026-01-01T09:00:00+08:00` (**Local**) | **`2025-12-31T20:00:00-05:00`** ❌ crosses the day | `09:00` Unspecified |

The table above goes through the `DataTable` path. Additional measurements of two more carriers, `DataSet` cells and
strongly typed DTOs, showed that the behavior is not consistent:

| Carrier | What happens to a `Local` value | Explanation |
|------|-----------------|------|
| `DataSet` cell | Not shifted | `DataColumn` first normalizes `Kind` away according to `DateTimeMode`, so the formatter never sees `Local` |
| Strongly typed DTO property (MessagePack) | **Shifted on the writing side** (`09:00`+08 → `01:00Z`) | The msgpack timestamp extension stores an absolute instant, so `Local` is converted to UTC |
| Strongly typed DTO property (JSON) | **Shifted on the reading side** (can cross the day) | The offset is written to the wire, and the reader recomputes it in its own time zone |

In addition, **XML is a third serialization path** (the audit `WriteXml(DiffGram)` goes through it), and it is the only
format that decides whether to write a time zone offset based on `DataColumn.DateTimeMode`. The .NET default,
`UnspecifiedLocal`, is exactly the value that "writes an offset", and once an offset is in the XML, reading it back in
another zone shifts the value or even crosses the day.

Three conclusions run through all the decisions below:

1. **MessagePack does not keep the `Kind` information** (the `DataTable` path erases it to `Unspecified`, and the DTO
   path always returns `Utc`), so "the value carries its own time zone information (the ISO 8601 `Z`)" does not hold
   in this framework.
2. **`Kind=Local` shifts the value in both formats**: JSON on the reading side (can cross the day), MessagePack on the
   writing side. `Local` has no escape route at all.
3. **The same UTC value read back through the two formats has a different `Kind`** (JSON `Utc` / MessagePack
   `Unspecified`), and `PayloadFormat` can be switched at deployment time, so **any logic that branches on `Kind`
   behaves differently depending on the deployment settings**.

## Options considered

### 1. Send ISO 8601 with a time zone offset on the wire, letting the value express it (rejected)

MessagePack does not keep `Kind` (measured conclusion 1), so the offset information cannot survive. Inconsistent
across formats.

### 2. Asymmetric design: the client sends the user's time zone, and the server converts back by `SessionInfo.TimeZone` (rejected)

This makes "the time zone used for display" (decided by the client) and "the time zone used to interpret written-back
values" (decided by the server session) two independent sources. As soon as they disagree (the user is traveling, the
device time zone differs from the company setting, the session time zone is not filled in), the failure mode is
**the user sees 09:00, enters 09:00, a different instant is stored, and the screen jumps after reloading**: silent,
and the data is corrupted.

### 3. UTC in both directions (adopted; superseded by option 5 on 2026-09-12)

There is only a single time zone source, the two directions are necessarily inverse functions, and a round trip is
always the identity. Even if the time zone is set wrongly, the error only degrades to "display offset" rather than
corrupted data. A side benefit: for a JS client, sending UTC is the native behavior of `date.toISOString()`.

> **Why it was retired (2026-09-12)**: the request direction has to convert values in the user's time zone back to
> UTC, and that conversion needed holes patched one by one in three places. In the DST fall-back overlap one wall-clock
> time corresponds to two UTC values, so reading in and saving back ends up one hour late; the only fix was for the
> Connector to remember the original value keyed by the row instance, and that memory is lost when the caller copies
> the `DataSet` itself. Values filled in by a client expression with `UtcNow()` are converted a second time. The clock
> reading frozen in `DataColumn.DefaultValue` is sent back as if it were a value in the user's time zone. The common
> root of all three is that the server adopted a value whose basis it had no way of knowing; "inverse functions,
> round trip is the identity" only holds once all these paths are patched.

### 4. A column-level `DateTimeSemantics` marker providing a third semantics, `Local` (rejected)

The original idea was to add a `DbField.DateTimeSemantics` property or a new `FieldDbType` enum value for columns
"bound to the local time of a particular place, independent of the viewer" (such as a meeting scheduled for
"09:00 local time"). Three reasons for rejecting it:

1. **Wrong layer.** `FieldDbType` describes "what type of data the column stores"; "whether to use UTC or the user's
   time zone" is a convention of transport and presentation, and this ADR has already fixed that convention (the wire
   is always UTC, and the only conversion point is the Connector). Putting time zone policy into the type description
   would give the same thing two deciders.
2. **Per-column cannot solve the real need.** The real cases of "presenting in the time zone of a particular place",
   such as HRM attendance that must be seen in the time zone of the employee's workplace, are **per-row**: employees
   are stationed in different places, and each record has a different time zone. A marker on the column can only
   express "the whole column is bound to the same place", which does not solve it at all. And no non-contrived example
   can be found of a situation where a per-column `Local` really holds; shift schedules such as "morning shift 08:00"
   or business hours are really timetables and should not be `DateTime` columns in the first place.
3. **Disproportionate cost.** It would require touching a core persisted enum or adding a property to every `DbField`,
   in exchange for a semantics that cannot solve the real need.

### 5. Convert in the response direction, and the server does not adopt the `DateTime` in a request (adopted, 2026-09-12)

`DateTime` accepts only values written by the server. The Connector only converts responses into the user's time zone;
the `DataSet` sent for saving is not converted, and at the entry of `Save` the server overwrites the values with a
server-side reading or the original database value (D14). The only conversion kept in the request direction is for
filter conditions, because they are used only for querying and are never persisted.

The difference from option 2 is that the server **does not interpret** the client's `DateTime`. Option 2 had the server
convert the client's value back to UTC by `SessionInfo.TimeZone` and adopt it, making the display time zone and the
interpretation time zone two sources; this option does not adopt that value at all, so there is no second source.
Filter conditions are still converted by the Connector rather than handed to the server precisely to avoid the split
of option 2: the client caches the session time zone at login, while the server rereads the user settings when its
cache is rebuilt, and the two may disagree.

Two other ways of having the server not adopt the value were not adopted: excluding `DateTime` columns in the write
layer would let BOs, rules and plugins read values in the user's time zone; having the Connector clear them before
sending would still require the server to read back the original values, and the audit would lose the original value.

The cost is that users cannot edit `DateTime` columns directly; a BO that really needs to overrides the normalization
method and converts on its own (D14). At the time of the decision the framework had no user-entered `DateTime` columns;
the business time columns were all `Date`.

## Decision

### D1: The DB always stores UTC, in naive columns for every provider

SQL Server `datetime2`, PostgreSQL `timestamp` (without tz), Oracle `TIMESTAMP`, MySQL `DATETIME`, SQLite `TEXT`.
Time zone conversion is not left to the database.

PostgreSQL `timestamptz` is not used: it converts implicitly according to the server tz, becoming an uncontrollable
variable and causing behavior to diverge across providers.

### D2: Neither serialization format touches time zones

MessagePack and JSON only carry values. The responsibility for conversion lies entirely with the server and the
client.

### D3: The server data path is UTC; a `DateTime` in a request depends on the carrier (revised 2026-09-12)

The server sends UTC. **The server does no time zone conversion at all on the data path**; it reads and writes UTC
directly.

The request direction depends on the carrier (D4's carrier table): filter condition values, strongly typed DTO
properties and `Parameters` are UTC; a `DataSet` sent for saving keeps the values shown on the client's screen, **is
not guaranteed to be UTC**, and the server does not adopt the `DateTime` values in it either (D14).

> The original text was "a `DateTime` on the wire is UTC in both directions: the server sends UTC and so does the
> client". It was revised along with option 5.

### D4: The Connector is the only conversion point

Time zone conversion on the client is concentrated in the `Connector` (the API interfacing layer), not handled by each
UI layer on its own.

#### Principle of conversion direction (revised 2026-09-12)

> **By default the Connector converts only the response direction; the request direction is not converted by
> default.**
>
> - **Response direction**: the `DateTime` columns of a `DataSet` / `DataTable` are converted from UTC to the user's
>   time zone.
> - **Request direction**: the `DateTime` values of a `DataSet` are not converted, and the server does not adopt the
>   client's values (D14).
> - **Exception (converted in the request direction)**: the `DateTime` of filter conditions is converted from the
>   user's time zone to UTC. It is used only for querying and is never stored in the database.
> - **Outside the scope of conversion**: strongly typed DTO properties and `Parameters` are UTC in both directions,
>   and the caller is responsible.

If a new need for request-direction conversion appears later, add it to the exception list case by case, following the
filter condition example; do not go back to converting both directions by default.

**Do not read this as "`DataSet` one way, everything else both ways".** No carrier is currently converted in both
directions: filter conditions appear only in requests, and strongly typed DTOs are not converted in either direction.
Accepting user-entered `DateTime` values in the future also means the BO converts them on the server (D14), not the
Connector converting both ways.

| Carrier | Response (server → client) | Request (client → server) |
|------|------|------|
| `DateTime` columns of a `DataSet` / `DataTable` | UTC → user's time zone | **Not converted** (the server does not adopt them) |
| `FilterCondition.Value` / `SecondValue` | Does not appear in responses | User's time zone → UTC |
| Strongly typed DTO properties (`ExpiredAt`, `FromUtc` / `ToUtc`, `ServerTime`, etc.) | Not converted, always UTC | Not converted, always UTC (the caller is responsible) |
| `Parameters` | Not converted | Not converted |

`DateOnly` and `TimeOnly` are not converted in any carrier or in any direction.

> The original text was "on receiving a response, UTC → user's time zone; before sending a request, user's time
> zone → UTC", that is, the two-way conversion of option 3.

#### Details

- **The deciding factor is the `FieldDbType` marker that travels with the payload** (ADR-031): `Date` is never
  converted, and `DateTime` is always treated as an instant and converted. **`FormSchema` is not needed at all**, so
  schema-less scenarios such as reports and AnyCode are covered too.
- **The `DateTime` properties of strongly typed DTOs always stay UTC and are not converted** (`PingResult.ServerTime`,
  `SessionInfo.ExpiredAt`, `AuditEntry.LogTimeUtc` and so on are system timestamps in the first place).
- **`FilterCondition.Value` / `SecondValue` are converted from the user's time zone to UTC** (the only conversion in
  the request direction), and the semantics is self-described by the value's CLR type: `DateOnly` is never converted,
  and `DateTime` is treated as an instant. The symptom of missing this is that "query today's documents" silently
  returns less data across zones without any error.
- **The conversion hooks into the Connector's entry and exit points, not the serialization entry**, and **a `DataSet`
  in a request is always replaced by a deep copy**. In-process (`LocalApiProvider` + `PayloadFormat.Plain`) there is no
  serialization boundary and objects are passed by reference, so hooking the serialization entry would bypass it
  entirely. The copy is still needed now that the request direction no longer converts the `DataSet`: the server-side
  `Save` rewrites the received `DataSet` in place (D14's normalization, and the `AcceptChanges` after writing), and
  without the copy it would change the caller's own instance. This is enforced by
  `ApiConnectorRequestIsolationTests` and `PayloadZoneCoverageGuardTests`.
- **`ApiMessageBase.Parameters` is not converted** (the untyped parameter bag carried by every request / response).
  The values in the bag are `object`, and **there is no type marker at all to tell "instant / calendar day / system
  timestamp" apart**; converting everything would be guessing, and it would also break UTC values the caller put in
  deliberately. If a custom AnyCode method needs to pass an instant, agree on a basis yourself (UTC throughout is
  recommended) or use the `DataTable` carrier, which carries the `FieldDbType` marker.
- **`Kind` is always ignored**, and the value is treated as UTC per D3 (measured conclusion 3).
- **When a filter condition value falls on a local time that does not exist (the spring-forward gap), it is moved
  forward by one DST delta.** A date picker has no way of knowing that a particular wall-clock time on a particular day
  does not exist, and a user picking 02:30 is a normal action; `ConvertTimeToUtc` throws `ArgumentException` for it,
  and the exception passes straight through JSON-RPC. So before converting to UTC, a value that falls inside the gap is
  moved forward by the delta of that transition (02:30 → 03:30), consistent with mainstream pickers such as iOS,
  Android and Google Calendar.
- **The fall-back overlap needs no handling by the Connector** (revised 2026-09-12). During the overlapping hour, two
  UTC values correspond to the same wall-clock time (on 2026-11-01 in US Eastern, both 05:30Z and 06:30Z are 01:30),
  and that information is lost the moment the response is converted into the user's time zone. Now that the request
  direction does not convert the `DataSet`, the time columns of modified rows are overwritten by the server with the
  original database value (D14), so reading in and saving back is not one hour late, and it does not depend on whether
  the caller copied the `DataSet`. When a filter condition value falls in the overlap, `ConvertTimeToUtc` resolves it
  as standard time; that is an ambiguity of the wall-clock time itself, consistent with mainstream calendars.

  This is enforced by `DateTimeZoneDstSaveRoundTripTests`: read in, convert into the user's time zone, change another
  column, save back as is, then read the database value back with SQL. SQLite reads time columns back as strings, which
  the response direction does not convert, so this rule cannot be verified on that database.

  > The original item (added earlier the same day) had the Connector remember the original UTC value of overlap cells,
  > keyed by **the row instance handed to the caller**, and send the remembered value in the request direction when
  > the cell still held the wall-clock time converted at the time; rows the caller copied or rebuilt had no memory.
  > It was withdrawn along with option 5.
- **The time zone source is `SessionInfo.TimeZone`; the device OS time zone is not used.** The authoritative source is
  the server-side user settings, so changing devices or traveling does not affect the meaning of the data. "Follow the
  device time zone" can be offered as a user-selectable setting, but it is not the default.

### D5: The framework provides only two time semantics

Namely the two that `FieldDbType` already distinguishes: `Date` (calendar day, never converted) and `DateTime`
(instant, converted).

**No per-column time zone override is provided** (see above for the reasons it was rejected). When there is a need to
"present in the time zone of a particular place", **model it explicitly as "a time column (UTC) + a time zone
column"** and let the application layer decide the presentation time zone. That is a data model decision, not
something the framework does on the application's behalf.

> This item is stated deliberately; otherwise someone will one day "helpfully add" `DateTimeSemantics`.

### D6: Time representation discipline and the wire guard

Split into three rules by carrier; **they guard different things**, and their enforcement mechanisms are not the same:

| Carrier | Rule | Enforcement |
|------|------|---------|
| `DataSet` / `DataTable` | The **`DataColumn.DateTimeMode` of every `DateTime` column must be `Unspecified`** | `DateTimeWireGuard` |
| `FilterCondition.Value` / `SecondValue` | The **`Kind` of a `DateTime` must not be `Local`** | `DateTimeWireGuard` |
| Strongly typed DTO properties | A `DateTime` should be UTC, and its **`Kind` should not be `Local`** | **None**: a writing discipline; the guard does not check DTO properties |

The `DataSet` rule does not check `Kind`: a cell's `Kind` is determined by `DateTimeMode`, so checking the value always
yields `Unspecified` and checking it is the same as not checking; what really decides "whether the XML output carries
an offset" is `DateTimeMode`. `AddColumn` already sets `Unspecified`; the gaps are paths such as
`DbDataAdapter.Fill` / `DataSet.ReadXml` that fall back to the .NET default `UnspecifiedLocal`. Now that the request
direction no longer converts the `DataSet`, this rule still checks the `DataSet` in a request: what it guards is
whether serialization writes a time zone offset, which has nothing to do with whether a conversion happens
(2026-09-12).

The rules for filter condition values and DTO properties both target `Kind`: without the normalizing buffer of a
`DataColumn`, `Local` **shifts the value on both wires** (MessagePack on the writing side, JSON on the reading side).
`Local` slips in very easily: `DateTime.Now`, `DateTime.Today`, values produced by UI controls and the result of
`ToLocalTime()` all have `Kind` `Local`.

DTO properties are not checked by the guard: the guard matches carriers one by one per message type and does not walk
the object graph, so a newly added message carrying a `DateTime` is not covered automatically. The current
request-side DTO properties that carry a `DateTime` all state their basis through the property name (`FromUtc` /
`ToUtc`) or the parameter documentation ("The UTC expiry"); **correctness relies on callers complying, and there is no
runtime check**.

- **The guard is fail fast: it throws in both debug and release**, and does not "fix it and let it through". Both ways
  of fixing silently produce wrong data: `SpecifyKind(Unspecified)` keeps the wall-clock time and discards the time
  zone information (a `Local` 09:00 mistakenly sent from Taipei would be stored by the server as UTC 09:00, off by
  8 hours); `ToUniversalTime()` converts by the **device OS time zone**, which D4 has already rejected as an
  authoritative source. `Kind=Local` entering the wire is **a programming error in the framework itself**, not a data
  condition of external input.
- **The guard hooks into the Connector's entry and exit points**, for the same reason as D4 (in-process has no
  serialization boundary).
- **The request-direction guard must run before D4's filter condition conversion**, validating the original values
  handed in by the caller. The conversion first applies `SpecifyKind(Unspecified)` to the filter condition value and
  then converts it by the user's time zone, which is exactly the "fix it and let it through" rejected in the previous
  item. Placed after the conversion, a `Local` value always passes as long as there is a user time zone (that is, on
  every call after login). `ApiConnectorDateTimeGuardTests` verifies this ordering.
- **The guard is always on and is not affected by any deployment setting.**
- Instant values read from the DB are uniformly `SpecifyKind(Utc)`; calendar day columns stay `Unspecified`.
  > Checked after implementation, this item has almost nowhere to land in this repository: the `Kind` of `DataSet`
  > cells is erased to `Unspecified` by `DataColumn` (marking it `Utc` is a no-op), the POCO mapping of `Query<T>` has
  > zero callers, and the only two expiry checks both compare against `DateTime.UtcNow`; `DateTime` comparison looks
  > at ticks, not `Kind`, so they were already correct. In practice it only marks the expiry time in
  > `SessionRepository`, and its value lies in turning "this column stores UTC" from an implicit dependency into a
  > declaration.

### D7 / D8: Persisted objects and system timestamps are always UTC

Time properties of persisted objects are always UTC (`SessionUser.EndTime`, `SessionInfo.ExpiredAt`,
`AuditEntry.LogTimeUtc`), and serialization does not touch time zones. Audit and trace always use `UtcNow`.

The `CreateTime` of definition files (`FormSchema` / `TableSchema` / each `*Settings`) is marked
`[XmlIgnore, JsonIgnore, IgnoreMember]` and has never been persisted, but it is changed to `UtcNow` as well, purely so
that "time properties are always UTC" has zero exceptions; kept as a Local exception, nobody would dare touch the
semantics of these properties later.

Cache expiry times (`CacheItemPolicy.AbsoluteExpiration`) likewise use `UtcNow`.

> The types of trace's `TraceEvent.Time` / `TraceContext.Start` and of `CacheItemPolicy.AbsoluteExpiration` are all
> `DateTimeOffset`, which **already carries an offset and is comparable across zones**. Changing them to `UtcNow` is
> not to fix comparability, but to keep serialization and log presentation independent of the deployment time zone,
> and to remove the trap of "the offset being discarded when it is later converted to `DateTime` or lands in a naive
> column". That is the value of a rule with zero exceptions: no need to judge case by case "will this
> `DateTimeOffset` be downgraded".
>
> This applies to the cache in particular: **it is currently an in-process cache, but if it is later replaced by a
> cross-machine distributed cache** (Redis and the like), expiry times will travel across processes and land through
> third-party serialization, and **the offset being discarded during serialization is exactly the existing behavior
> this ADR has measured** (see the Context section: MessagePack does not keep `Kind`). At that point "the value itself
> is UTC" is the only basis that does not depend on whether the serializer keeps the offset.

### D12: "Today" is based on the user's time zone, and "now" follows the side the `DataSet` is on

**"Today" = today in `SessionInfo.TimeZone`**, not today on the device OS, and not today on the server machine.

The reason is business semantics: the leave date of a leave request defaults to "the current day", and that day is
necessarily the current day in the user's time zone. The authoritative source is `SessionInfo.TimeZone` rather than
the device time zone; otherwise a user on a business trip in New York filling in a leave request for the Taipei
company would get the previous day as the default date. This is consistent with D4's authoritative time zone source.

**The server and the client must use the same definition**: the Connector never converts `Date` columns (D4), so if
the two sides computed different "todays", the same document would have different dates on the two sides. Evaluation
on the server likewise uses the session time zone, not the machine time zone.

In the implementation, the scattered `DateTime.Now` / `DateTime.Today` calls are consolidated into a single seam
(`FormRowDefaults`, `FieldDbTypeExtensions`, and `Today()` / `Now()` of `DynamicExpressoEvaluator`), and that seam
derives the values from the user's time zone.

**"Today" on both paths is connected to the user's time zone**, because `Today()` and the column type default values
share the same seam (`FrameworkClock`), and the time zone is passed as an argument along the call chain (D13(b)): on
the server the BO takes the session time zone and passes it in; on the client it takes `ClientInfo.UserInfo.TimeZone`.

#### The basis of "now" is decided by the side the `DataSet` is on (added 2026-09-12)

"Today" is a calendar day, and on both sides it belongs to the user's time zone. "Now" is an instant; when it is
written into a `DataSet` or compared with a cell, it must have the same basis as the existing time values in the same
`DataSet`, and the two sides have different bases:

| Side | Basis of `DateTime` in the `DataSet` | Reason |
|----|------|------|
| Client | User's time zone | The Connector already converted it on receiving the response (D4) |
| Server | UTC | The data path does no conversion (D3) |

So the seam takes two arguments: the time zone decides "today", and `DateTimeBasis` decides which basis "now" is
expressed in. `FrameworkClock`, `FormRowDefaults` and `IExpressionEvaluator` all carry this argument, defaulting to
`UserZone`:

| Caller | Basis |
|--------|------|
| `DataFormRepository.GetNewData` (server-side default values for a new row) | `Utc` |
| `FormExpressionCalculator.ApplyFieldExpressions` / `ValidateRules` (the server-side pass before saving) | `Utc`, fixed inside the method |
| `FormExpressionCalculator.ApplyComputedRow` / `ApplyDefaultRow` (client-side live preview) | `UserZone`, fixed inside the method |
| `FormRowDefaults.Apply` when the client adds a detail row | `UserZone` (the default) |

This is enforced by `DataFormRepositoryTests.GetNewData_TimeDefaults_DateTimeIsUtcAndDateIsUserDay`,
`FormRowDefaultsCoverageTests.Apply_OnAddColumnTable_SeedsDateOnUserDayAndDateTimeOnBasis`, and the server-side and
client-side `Now()` tests in `FormExpressionCalculatorTests`.

> **The original decision had a defect here, and it was hidden by another defect.** The original text said "both paths
> are ultimately connected to the user's time zone", connecting "today" and "now" together, so `Now()` in the
> server-side save pass produced a wall-clock time in the user's time zone and put it into a `DataSet` expressed in
> UTC: the written value was off by the time difference, and so were comparisons with cells in rules. **This has
> nothing to do with the server host's time zone**: the value is converted from `DateTime.UtcNow` by the session time
> zone, so it happens even when the host runs on UTC.
>
> The `DateTime` default values of `FormRowDefaults` had the same problem, but it never surfaced in `GetNewData`:
> `AddColumn` wrote the UTC reading at column creation into `DataColumn.DefaultValue`, `NewRow()` carried the value as
> soon as it was created, and `FormRowDefaults` skips columns that already have a value. That `DefaultValue` itself
> caused three further errors:
>
> 1. The `Date` default value of the server-side `GetNewData` was today in UTC, not today in the session time zone.
> 2. `DefaultValue` travels to the client with the table: serialization carries it as is per D2, and the Connector only
>    converts cells. When the client added a detail row to a new document, it got the UTC reading frozen at the moment
>    the server built the skeleton, which was treated as a value in the user's time zone when sent, and stored in the
>    database as the user's time zone.
> 3. The empty table built by the client's `FormValueBinding.BuildEmptyDataSet` had the same problem, except that the
>    reading was frozen at the moment the client built the table.
>
> Therefore `AddColumn` no longer sets default values for `Date` / `DateTime`, and the time default values of new rows
> are produced only by `FormRowDefaults`.

The expression function set is `Today()` (today in the passed-in time zone, returning `DateOnly`), `Now()` (the
current moment on the same basis as the containing `DataSet`, with `Kind` always `Unspecified`), and `UtcNow()` (the
raw UTC reading of the current moment, not affected by the basis). Sharing the seam is deliberate: calendar day columns
are never converted (D4), so sharing introduces no double conversion problem, while letting the same name mean two
different things in two places is the easiest trap to fall into later.

The only one not connected to a time zone is `FieldDbTypeExtensions.GetDefaultValue`: it has no user context to pass
in; see the exception clause in D13.

> **Residual risk (accepted deliberately)**: `UtcNow()` is not affected by the basis. When the client-side live preview
> fills a `DateTime` cell with `UtcNow()`, the value on screen is off by the time difference. On saving, the server
> does not adopt the client's `DateTime` values (D14), and columns with expressions are re-evaluated by the server or
> keep the database value, so no wrong data is written; what is wrong is the value on screen before saving. To write
> into a `DateTime` cell or compare with one, use `Now()`.
>
> This paragraph originally said "when sent, it is treated by the Connector as a value in the user's time zone and
> converted again", which described the time when the request direction still converted the `DataSet`; it was revised
> along with option 5. An even earlier version also said "filling with `Now()` is converted again too", and the
> `Now()` half of that was wrong: the client's `Now()` was already a value in the user's time zone; what was really
> misaligned was the server's `Now()`, which has been fixed by the basis above.

### D13: Dates are always `DateOnly`, with `DataSet` as the only exception; time zones are always passed as arguments

**Two rules that together make up the shape of date handling.**

#### (a) The carrier of dates

Dates are always expressed as `DateOnly`. **The only exception is `DataSet`**: `DataColumn` forces type conversion
through `IConvertible`, and `DateOnly` does not implement it (measured: `row["d"] = new DateOnly(...)` throws
`ArgumentException` for a `DateTime` column), so calendar day columns stay `typeof(DateTime)`, and the distinction
between "date-time vs date" is carried by the `FieldDbType` marker (ADR-031 established this mechanism).

The conversion happens **at the moment of writing into the `DataSet`**, rather than making the whole framework speak
`DateTime` for the sake of one consumer.

#### (b) Passing the time zone

**For date-time functions shared by the front end and back end, the time zone is always passed as an argument, never
resolved from ambient state.**

The reason is not just "cleanliness": this kind of code **runs on both sides**. A helper that reads the time zone from
somewhere invisible behaves differently on the server and on the client, and that is exactly the hardest kind of split
to notice. Specifically in this framework:

- The server has **no** ambient "current user": `ISessionInfoService` is keyed by access token, and when it serves
  several users concurrently there is no single session to look up.
- `IExpressionEvaluator` is registered as a **singleton**, so no design that "fixes the time zone at construction" can
  express a per-user time zone.
- Passing an id rather than an `IUserInfo` lets `FrameworkClock` stay in `Polhem.Base` (below the identity model);
  callers holding an `IUserInfo` just pass `.TimeZone`, and the interface works all the same.

**Exception**: `FieldDbTypeExtensions.GetDefaultValue` has no user context to pass in, so it produces UTC. It is a
**data integrity fallback** that fills values for NOT NULL parameters, not a value users read. The new-row default
values users can see go through `FormRowDefaults`, which takes the time zone argument and `DateTimeBasis`.

`AddColumn` does **not** use it for `Date` / `DateTime` (fixed 2026-09-12, see D12): `DataColumn.DefaultValue` is a
single value fixed when the column is created; putting a clock reading in it gives every later row a stale value, and
it also hides `FormRowDefaults`.

> This fallback takes effect only when the command is not bound to a data row. Form saving goes through
> `DbDataAdapter.Update`, and the adapter overwrites parameter values with the row values of the `SourceColumn`, so a
> `DBNull` in the row is still sent to the database as NULL and is stopped by the NOT NULL constraint. `DateTime`
> columns are the exception: before a form is saved, D14's normalization has already filled new rows with the server
> reading, so a NOT NULL `DateTime` column without a default value expression is not sent as NULL (2026-09-12).

### D14: `DateTime` accepts only values written by the server (2026-09-12)

`FormBusinessObject.Save` calls the `protected virtual` `NormalizeDateTimes` after the authorization and write-scope
checks and before `DoBeforeSave`, handling the `DateTime` columns of each table according to the `FormSchema`:

| Row state | Handling |
|------|------|
| Added | `sys_insert_time`, `sys_update_time` and columns without a `DefaultValueExpression` are filled with the UTC reading at the moment of saving (one reading for one save); columns with an expression are cleared and left to `ApplyFieldExpressions` to evaluate |
| Modified, deleted | Read back from the database by `sys_rowid`, and both row versions are changed to the database values; the `sys_update_time` of a modified row is then filled with the UTC reading |

- **It sits before the rules**, so the rules, plugins, audit and writes after it all see UTC, and D3's "the server data
  path is UTC" still holds.
- **It does not distinguish the caller**: when one server-side BO calls another's `Save`, the `DateTime` values passed
  in are not adopted either. Work that needs to write `DateTime` values overrides the normalization method or goes
  through the repository directly.
- **Accepting user-entered `DateTime` values**: override `NormalizeDateTimes`, read out the values passed in first, call
  the base implementation, then convert them to UTC by the user's time zone and write them back. Pure `FormSchema` forms
  do not support this; at the time of the decision there was also no editor that could keep hours and minutes.
- **If a row cannot be found when reading back** (it was deleted concurrently), a `UserMessageException` is thrown,
  aborting before any write.
- **To rewrite the Original, first capture both versions of the whole row, then `RejectChanges`**; otherwise changes
  to non-time columns are lost. This is the same trap as when D4 converted modified rows in the response direction.
- **System timestamp columns are always marked `ReadOnly` in the `FormSchema`**; otherwise users could change on screen
  a value that cannot be saved. Missing the mark does not write wrong data, so no separate gate is set up.

Residual limitation: `Unchanged` rows are not normalized. They are not written and do not enter the audit, but
`ValidateRules` walks all non-deleted rows, and plugins can see them too; for `Unchanged` rows sent up together with a
save, the `DateTime` columns hold values in the user's time zone. Rules or plugins that compare the time columns of
these rows get the wrong basis.

This is enforced by `FormBusinessObjectDateTimeNormalizationTests`: insert, update and delete run for real on each
database (including the audit DiffGram), plus the row-not-found-on-read-back case and the override seam.

### D9: The time basis of cache-notify shares its source with the write side, always UTC (revised 2026-09-04)

The high-water mark of `sys_update_time` is only compared with itself, but "itself" has two sources: the value of
each row is stamped by the write side, while the starting cursor for an empty table is obtained by the read side by
asking the database for "now". **The two must be on the same basis.**

So both sides take the value from the same place, `IDialectFactory.GetDefaultValueExpression(FieldDbType.DateTime)`,
which is the table in D9b, all UTC:

| Side | Location |
|----|------|
| Write | The column `DEFAULT`, the UPSERT of `CacheNotifyService` |
| Read | The empty-table baseline of `CacheNotifyReader` |

This is enforced by `CacheNotifyBaselineBasisTests`: it verifies that the baseline expression is exactly the same as
the write side's, and runs the statement for real on SQL Server / PostgreSQL / MySQL / Oracle, verifying that the
returned value is close to UTC.

> **The original decision, "deliberately not converted to UTC", has been withdrawn.** The original text held that the
> high-water mark is only compared with itself, so converting it to UTC had no real benefit, and warned that unifying
> it later would run into the differing bases of each provider's time functions.
>
> Why it was withdrawn: the write side read the dialect expression of the column `DEFAULT` from the start, so when D9b
> changed it to UTC, the write side became UTC along with it; the read side's baseline, however, carried its own
> dialect table returning the server's local time (`getdate()` / `LOCALTIMESTAMP` / `CURRENT_TIMESTAMP(6)`), which was
> not changed. On a server whose time zone is ahead of UTC, the first cursor of a fresh deployment (empty table) lands
> in the future, and every later incremental query finds no rows: **cache invalidation silently stalls until the wall
> clock catches up**, eight hours for UTC+8. Oracle's `LOCALTIMESTAMP` takes the client session's time zone, so the
> basis even varies with the machine running the poll. The local containers and the CI runners all run on UTC, where
> the two expressions happen to be equal, which is why it was never discovered.
>
> The lesson is the opposite of the original warning: the danger is not "unifying", it is **two dialect tables that
> must stay consistent**. The read side therefore no longer keeps a copy of its own.

### D9b: Column `DEFAULT`s on the database side must also be UTC

D1 makes "`FieldDbType.DateTime` columns store UTC" a **mandatory condition**, and SQL statements do not always
specify the value of such a column; `DEFAULT` is exactly the path through which data is actually written in those
cases. Therefore every dialect's default value expression uses a UTC form:

| Provider | `DEFAULT` for `DateTime` |
|----------|----------------------|
| SQL Server | `getutcdate()` |
| PostgreSQL | `(NOW() AT TIME ZONE 'UTC')` |
| MySQL | `UTC_TIMESTAMP(6)` |
| Oracle | `SYS_EXTRACT_UTC(SYSTIMESTAMP)` |
| SQLite | `CURRENT_TIMESTAMP` (already UTC) |

**This has nothing to do with D12; do not confuse the two.** D12 is about "default values users can see must use their
time zone", which is indeed something the database cannot do: `DEFAULT` is evaluated inside the database, with no
session and no knowledge of who the user is. But D1 is about the **storage basis**, and UTC is an absolute instant
independent of the user, which the database is fully able to, and must, follow. The new-row default values users can
see are produced separately by `FormRowDefaults` according to the session time zone.

It takes effect on two paths: a caller's hand-written INSERT that omits the column, and the backfill of existing rows by
`ALTER TABLE ADD COLUMN`.

> **PostgreSQL's round-trip trap**: PG does not keep function-style default values verbatim; it rewrites them as
> `(now() AT TIME ZONE 'UTC'::text)`. The framework's schema comparison is a text comparison, so without handling it
> would always judge that there is a difference and **reissue the same ALTER on every check**.
> `PgTableSchemaProvider.ParseDBDefaultValue` therefore has a dedicated normalization for it; the generic "truncate at
> the first `::`" logic does not apply here, because that `::` sits **inside** the parentheses.

### D10: The conversion always runs, and is the identity conversion within the same time zone

**"Zero cost" means complexity cost, not execution cost.** The conversion pipeline always runs and is not bypassed by
deployment settings; when the user's time zone == the system time zone it degrades to the **identity conversion** (the
value does not change), rather than being skipped.

> The original plan, "a no-op within the same time zone, behavior bit-for-bit identical to today", conflicts directly
> with D1: in a single-time-zone Taipei deployment, if the conversion really were a no-op, users would see the raw DB
> value; for users to see Taipei time, the DB would have to store Taipei time, which overturns D1. Conversely, if the
> DB really stores UTC, Taipei users must be converted, and the short circuit never triggers.
>
> Letting single-time-zone deployments not store UTC would break the "always" of D1, and a later upgrade to a
> cross-zone deployment would require data migration, while D11 has already decided not to build a migration tool.
> So the conversion is made to always run: D1 has zero exceptions, and the cost of the identity conversion is
> negligible (one check per column, not per cell). The price is that "a single-time-zone deployment behaves bit-for-bit
> like today" no longer holds, and DB contents change from local wall-clock time to UTC; since there are no external
> consumers (D11), this only involves rebuilding local / CI / demo data.

**Exception**: D6's guard is not affected by any setting. Otherwise `Local` slipping in would go completely unnoticed
until the first cross-zone customer, by which time the errors would already be written into historical data.

### D11: No migration of existing data for now

The framework currently has no real external consumers, so there is no existing production data whose meaning needs
preserving at the switch. Local / CI / demo data can all be rebuilt.

> **Preconditions if a migration is really needed later**:
>
> 1. **Switch once, with no compatibility period.** A compatibility period needs per-row markers of old and new
>    semantics and branching on both the read and write paths, which costs more than downtime.
> 2. **Judge column by column; do not apply it to whole tables**: columns that are already UTC, such as those of
>    `st_session`, must not be converted again; calendar day columns are left alone.
> 3. **A fixed offset only holds when "the time zone has no DST changes during the deployment period".**
>    `Asia/Taipei` has no DST, so a fixed +8 is safe and reversible. **If the customer is in a time zone with DST, the
>    migration must instead be a tz-aware, row-by-row conversion.**
>
> A migration need usually comes with time pressure, and there will be no room to re-derive this then, so it is
> recorded here.

### The future home of `FieldDbType.Time`

`FieldDbType` currently has no `Time` (a pure time-of-day value). When it is added:

- **`Time` belongs to "never converted between time zones"**, alongside `Date`: a pure time-of-day value, like a
  calendar day, is wall-clock time, and applying a time zone shift gives a meaningless result. This conclusion is
  recorded in advance here, so it does not need to be re-derived when work on `Time` starts.
- **The new value must be added at the end of the enum**: `FieldDbType` does not specify explicit values and travels
  on the MessagePack wire, so inserting a value in the middle would shift every value after it, breaking compatibility
  with existing payloads and definition files.

## Consequences

**Positive**

- A single time zone source; saving does not adopt the client's `DateTime` values, so time values do not change when
  read in and saved back, regardless of whether the conversion is reversible, and the DST fall-back overlap is no
  exception (D14). A wrongly set time zone only degrades to a display offset.
- The Connector is completely schema-less, so schema-less scenarios such as reports and AnyCode are equally safe.
- A single conversion path: within the same time zone it degrades to the identity conversion, so there is no need to
  maintain two sets of behavior for "cross-zone or not".

**Negative / risks**

- **`Kind=Local` slipping onto the wire** is the most fragile link. With the guard fail fast, the failure mode changes
  from "silently wrong data" to "an exception on the spot", but the risk of the guard itself being removed or bypassed
  remains, and its tests have the highest priority. A real case: when the time zone conversion was hooked into the
  Connector, the guard was wrapped after the conversion, and from then on filter conditions after login no longer
  caught `Local`. The guard's own unit tests were all green: they only verify the guard and cannot see its position on
  the call path (fixed 2026-09-12).
- **Calendar days converted by mistake**: the marker approach cannot guarantee that a column is always marked; calendar
  day columns in a BO's hand-written SQL that are not declared with `SetDateColumns` are still converted as instants
  (ADR-031 records this residual gap and the BO author's responsibility for marking).
- **`DateTime` columns cannot be edited by users directly** (D14). When adding a `DateTime` column that accepts user
  input, the BO must override the normalization method and convert on its own; otherwise the entered value is silently
  replaced by the server's value.
- **The `DateTime` columns of `Unchanged` rows hold values in the user's time zone** (D14's residual limitation),
  which only affects rules and plugins that compare the time columns of these rows on the server.
- **`TimeZoneInfo.FindSystemTimeZoneById` is unverified on WASM / iOS / Android.** It depends on ICU and the tz
  database; under trim + AOT the failure takes the form of `TimeZoneNotFoundException`, and it does not reproduce on
  the desktop at all.
- **The in-process path has no serialization boundary**, so an implementation very easily falls back to the intuitive
  approach of "hooking the serialization entry".

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-08-09: the `CreateTime` attributes.** Definition types no longer carry MessagePack attributes
  ([ADR-036](adr-036-wire-serialization-externalized.md)), so the `CreateTime` properties named in D7 / D8 are marked
  `[XmlIgnore, JsonIgnore]` (for example `src/Polhem.Definition/Settings/SystemSettings/SystemSettings.cs`). They are
  still not persisted and still initialized with `UtcNow`.
- **2026-09-27: the trace types are removed.** The tracing subsystem that owned `TraceEvent.Time` and
  `TraceContext.Start` (D7 / D8) no longer exists; the rule stays as it is for the remaining system timestamps.
- **2026-09-27: where the Connector gets the time zone.** A successful login through `SystemApiConnector` stores the
  user's time zone from the login response in the connector's session (`ApiSessionContext.UserTimeZoneId`,
  `src/Polhem.Api.Client/Connectors/SystemApiConnector.cs`), and the conversion of D4 reads it from there. A host that
  serves several users from one process, such as a Blazor Server app with one session per circuit, therefore converts
  each user's values with that user's time zone.
- **2026-10-10: the zone travels with the credentials.** The zone is now part of the session's
  `ApiSessionCredentials`, replaced together with the token and the key at sign-in, and a call reads it once at its
  start ([ADR-053](adr-053-api-client-composition-root.md)).

## Related

- ADR-031 (calendar day column semantics carried by an explicit marker): the basis of this ADR's D4 decisions
