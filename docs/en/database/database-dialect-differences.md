# Database Dialect Differences (DDL)

[繁體中文](../../zh-TW/database/database-dialect-differences.md) · [← Docs Index](../README.md)

Polhem generates DDL (CREATE TABLE / ALTER TABLE) from a single `TableSchema` definition and hides the per-database differences behind dialect adapters under `src/Polhem.Db/Providers/<Dialect>/`. Application developers usually never see these differences — the framework's own CRUD, seeding, and schema-upgrade paths handle them uniformly.

This document consolidates the DDL rules and **exceptions** that *do* leak through when you write schema definitions or hand-written SQL by hand (for example, an `INSERT` in a test helper or a migration script). It covers every supported engine: **SQL Server, PostgreSQL, MySQL, Oracle, SQLite**.

> Related, more focused documents:
> - [database-naming-conventions.md](database-naming-conventions.md) §5 — identifier case sensitivity and quoting.
> - [database-schema-upgrade.md](database-schema-upgrade.md) §4 — ALTER-vs-rebuild decision and per-dialect ALTER capabilities.
> - [src/Polhem.Db/README.md](../../../src/Polhem.Db/README.md) — SQLite limitations and the Oracle identifier strategy.

---

## 1. Why text and numeric columns are `NOT NULL` by default

This is a deliberate framework design decision, so it is stated first because it drives most of the exceptions below.

`DbField.AllowNull` **defaults to `false`** ([DbField.cs](../../../src/Polhem.Definition/Database/DbField.cs)). The rule is:

| Column category | Nullability | Default value |
|-----------------|-------------|---------------|
| **Text** (`String`, `Text`, `Time`) | `NOT NULL` | empty string `''` |
| **Numeric** (`Short`, `Integer`, `Long`, `Decimal`, `Currency`, `Boolean`) | `NOT NULL` | `0` |
| **Date / DateTime** | `NOT NULL` unless marked otherwise | current UTC date / timestamp |
| **Guid** | `NOT NULL` unless marked otherwise | a new GUID |
| **Binary** | `NOT NULL` unless marked otherwise | none: every `INSERT` must supply the value |
| Columns that genuinely need null (e.g. an "invalid/expiry time", optional binary) | set `AllowNull="true"` **explicitly** | none |

### Rationale

Allowing `NULL` in text and numeric columns forces every consumer — application code *and* hand-written SQL — to defend against null:

- `WHERE col = ''` silently fails to match `NULL` rows; you would need `WHERE col IS NULL OR col = ''` everywhere.
- Aggregations, joins, and string operations need `COALESCE` / null guards.
- C# consumers risk `NullReferenceException` unless every read is null-checked.

By guaranteeing "text is never null, numeric is never null", the framework lets you treat an empty string and zero as the canonical "empty" values and skip null handling entirely. **Do not reflexively add `AllowNull="true"`** — only set it for columns that have a real, distinct "unknown / not-set" state that empty-string or `0` cannot represent (typically `DateTime` such as a session expiry time, or optional binary payloads).

### How the guarantee is upheld even where a database fights it

Two engines cannot honour "text `NOT NULL` with an empty-string default" directly. The framework still upholds the *contract* — see the two hard exceptions in [§3](#3-the-two-hard-nullability-exceptions).

---

## 2. Built-in default value expressions

For a `NOT NULL` column with no explicit `DefaultValue`, each dialect emits its own default expression. (When `AllowNull="true"`, **no** default is emitted on any dialect.)

| `FieldDbType` | SQL Server | PostgreSQL | MySQL | Oracle | SQLite |
|---------------|-----------|------------|-------|--------|--------|
| `String` / `Time` | `N''` | `''` | `''` | *(nullable — see §3.1)* | `''` |
| `Text` | `N''` | `''` | *(none — see §3.2)* | *(nullable — see §3.1)* | `''` |
| `Short`/`Integer`/`Long`/`Decimal`/`Currency`/`Boolean` | `0` | `0` (`FALSE` for `Boolean`) | `0` | `0` | `0` |
| `Date` | `getutcdate()` | `(NOW() AT TIME ZONE 'UTC')` | `(UTC_DATE())` | `SYS_EXTRACT_UTC(SYSTIMESTAMP)` | `CURRENT_TIMESTAMP` |
| `DateTime` | `getutcdate()` | `(NOW() AT TIME ZONE 'UTC')` | `(UTC_TIMESTAMP(6))` | `SYS_EXTRACT_UTC(SYSTIMESTAMP)` | `CURRENT_TIMESTAMP` |
| `Guid` | `newid()` | `gen_random_uuid()` | `(UUID())` | `SYS_GUID()` | `(hex(randomblob(16)))` |
| `Binary`, `AutoIncrement` | *(none)* | *(none)* | *(none)* | *(none)* | *(none)* |

SQL Server wraps whatever expression it emits in one more pair of parentheses (`DEFAULT (N'')`).

Notes:

- **Temporal defaults are all UTC-returning.** Framework time columns are stored in UTC (see
  [Time Zones](datetime-timezone.md)), and a `DEFAULT` is the path that actually writes when the
  SQL does not name the column: a hand-written INSERT that omits it, and `ALTER TABLE ADD COLUMN`
  backfilling existing rows. See D9b in [ADR-032](../../../maintainers/adr/adr-032-datetime-timezone.md).
- **MySQL** wraps function-call defaults in parentheses (`(UUID())`, `(UTC_DATE())`, `(UTC_TIMESTAMP(6))`) because MySQL 8.0.13+ accepts a bare function as a default only for `CURRENT_TIMESTAMP`; every other non-literal default must use the parenthesised *expression* form.
- **SQLite** has no native UUID generator; `hex(randomblob(16))` is a unique-but-not-strictly-v4 surrogate, sufficient for framework-managed defaults.
- **Boolean literals**: the framework's canonical form is `"1"` / `"0"`. PostgreSQL rejects those for a `BOOLEAN` column, so the PG dialect translates them to `TRUE` / `FALSE` at the SQL-emission boundary. All other dialects accept `1` / `0`.

### Explicit `DefaultValue`

A `DbField.DefaultValue` replaces the built-in default, and how it reaches the DDL depends on the column type ([DefaultValueLiteral.cs](../../../src/Polhem.Db/Providers/DefaultValueLiteral.cs)):

- **`String`, `Text`, `Time`**: quoted and escaped by each dialect (`N'...'` on SQL Server; MySQL also escapes backslashes). MySQL and Oracle still emit no default for a `Text` column (§3).
- **`Short`, `Integer`, `Long`, `Decimal`, `Currency`, `Boolean`**: written into the DDL unquoted, so the value must be a plain literal of the column's type: a signed integer, a decimal with `.` as the separator, or `0` / `1` for `Boolean`. Anything else makes DDL generation throw `InvalidOperationException` ("... is not a valid ... literal").
- **`Date`, `DateTime`, `Guid`, `Binary`**: these types have no unquoted literal form, so an explicit value is rejected the same way (Oracle drops it for `Binary` instead). Leave `DefaultValue` empty to get the built-in default.

---

## 3. The two hard nullability exceptions

These are the rules most likely to cause a "works everywhere else, fails on one engine" surprise.

### 3.1 Oracle: `''` is `NULL`

Oracle has no concept of a non-null empty string — `''` **is** `NULL`. So `VARCHAR2(n) DEFAULT '' NOT NULL` is self-contradictory (`DEFAULT ''` means `DEFAULT NULL`, which conflicts with `NOT NULL`).

**How the framework handles it** ([OracleSchemaSyntax.cs](../../../src/Polhem.Db/Providers/Oracle/OracleSchemaSyntax.cs)):

- `String`, `Time` and `Text` columns are emitted **nullable** (no `NOT NULL`, no `DEFAULT ''`) on Oracle only.
- The "text is never null" contract is upheld at the C# layer: `ValueUtilities.CStr(null)` returns `""`, so callers still only ever see an empty string.
- `OracleTableSchemaProvider` reads such columns back as `AllowNull = false` to keep the schema diff stable against the definition.
- An explicit *non-empty* default is still a valid non-null literal on a nullable Oracle column, so it is preserved.
- `CLOB` / `BLOB` also reject an inline literal `DEFAULT` in the framework's `CREATE TABLE` shape.

### 3.2 MySQL: `TEXT` / `BLOB` cannot have a `DEFAULT`

MySQL forbids a `DEFAULT` clause on `TEXT` / `BLOB` columns. So a `Text` column with `AllowNull=false` is emitted as `TEXT NOT NULL` **with no default** ([MySqlSchemaSyntax.cs](../../../src/Polhem.Db/Providers/MySql/MySqlSchemaSyntax.cs)) — the column stays `NOT NULL`, but there is no DB-side fallback value.

**Consequence for hand-written SQL:** any `INSERT` that **omits** a `NOT NULL` `Text` column fails **only on MySQL** in strict mode with:

```
Field 'x' doesn't have a default value
```

On the other four engines the same partial `INSERT` succeeds, because their `NOT NULL` text columns carry an implicit `DEFAULT ''`. The framework's own CRUD and seeding always list every column, so they are unaffected — the trap is **hand-written raw SQL** (test helpers, seed scripts, migration snippets).

> **Rule when adding a `NOT NULL` `Text` column:** keep it `NOT NULL` (do **not** switch it to nullable to work around MySQL), and make sure every hand-written `INSERT` supplies the value explicitly (an empty string is fine). This matches the framework principle in §1; the fix belongs in the INSERT, not in the column's nullability.

---

## 4. Identifier quoting and case

| | Quote form | Case behaviour |
|---|-----------|----------------|
| SQL Server | `[name]` (`]` → `]]`) | quoted lowercase |
| PostgreSQL | `"name"` (`"` → `""`) | quoted lowercase |
| MySQL | `` `name` `` | quoted lowercase; case-insensitive comparison via table-level `COLLATE utf8mb4_0900_ai_ci` |
| Oracle | `"NAME"` (`"` → `""`) | **quoted UPPERCASE** — Oracle has a wide reserved-word set (`COMMENT`, `SIZE`, `LEVEL`, `SESSION`, …) so every identifier is quoted; the adapter folds to uppercase to match Oracle's native unquoted behaviour, and normalises back to lowercase on read-back |
| SQLite | `"name"` (`"` → `""`) | quoted lowercase; `String`, `Text` and `Guid` columns get `COLLATE NOCASE` for case-insensitive comparison |

See [database-naming-conventions.md](database-naming-conventions.md) §5 for the full case-sensitivity matrix (identifier folding vs. data comparison).

---

## 5. AutoIncrement (identity) syntax

`AutoIncrement` maps to a different construct per engine, and some require inlining it with the primary key on the same column line:

| | Syntax | Must inline with PK? |
|---|--------|----------------------|
| SQL Server | `[int] IDENTITY(1,1)` | no |
| PostgreSQL | `GENERATED BY DEFAULT AS IDENTITY` | no |
| MySQL | `BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY` | **yes** |
| Oracle | `NUMBER(19) GENERATED BY DEFAULT AS IDENTITY` | no (PK added separately) |
| SQLite | `INTEGER PRIMARY KEY AUTOINCREMENT` | **yes** (cannot attach `AUTOINCREMENT` via an external PK constraint) |

---

## 6. ALTER vs. table rebuild

When a schema upgrade changes a column, some changes can be done with `ALTER`, others require rebuilding the table (create-new + copy + swap). The decision and the per-dialect capabilities are documented in [database-schema-upgrade.md](database-schema-upgrade.md) §4. Highlights:

- **Every dialect** rebuilds on a change across type families (for example text to number) and on turning AutoIncrement on or off; adds, renames and index changes are applied with `ALTER`.
- **SQLite** cannot change a column in place at all (its `ALTER TABLE` only adds, renames and drops columns), so every column change is a rebuild.
- **Oracle** also rebuilds when a text column crosses the LOB boundary (`VARCHAR2` ↔ `CLOB`), which `ALTER ... MODIFY` rejects.
- **Oracle** `ALTER TABLE ... MODIFY` must be **diff-based** for nullability: re-issuing `NOT NULL` on an already-`NOT NULL` column raises `ORA-01442`, so the adapter only emits the `NULL` / `NOT NULL` hint when it actually changes.
- **MySQL** `ALTER ADD` of a `Guid` column with a non-deterministic default (`UUID()`) is split into two statements to stay replication-safe under statement-based binlog.
- The rebuild's table rename differs too: `sp_rename` (SQL Server) vs `ALTER TABLE ... RENAME TO` (the others).

---

## 7. Checklist: adding a column across all dialects

1. **Choose nullability by principle, not reflex.** Text / numeric → leave `AllowNull=false` (NOT NULL, default `''` / `0`). Only set `AllowNull="true"` for a genuine "unknown / not-set" state (e.g. a `DateTime` expiry, optional binary).
2. **Update every hand-written `INSERT`** that targets the table so it lists the new column — mandatory for a `NOT NULL` `Text` column because MySQL gives it no DB-side default (§3.2). The framework's own CRUD/seed already lists all columns.
3. **If the column is normally empty and you need Oracle**, be aware it will be physically nullable there (§3.1); the C# layer still reads it as an empty string, so no application change is needed.
4. **If you set a `DefaultValue` on a non-text column**, make it a plain literal of the column's type (§2, Explicit `DefaultValue`).

---

## Reference

- Dialect implementations: `src/Polhem.Db/Providers/<Dialect>/<Dialect>SchemaSyntax.cs`, `…TableSchemaProvider.cs`.
- Dialect-neutral ALTER-vs-rebuild and narrowing rules, shared by every provider: `src/Polhem.Db/Schema/AlterCompatibilityRules.cs` (SQLite replaces `GetKindForTypeChange` in `src/Polhem.Db/Providers/Sqlite/SqliteAlterCompatibilityRules.cs`; Oracle adds the LOB rule in `OracleTableAlterCommandBuilder.GetExecutionKind`).
- Column model: [DbField.cs](../../../src/Polhem.Definition/Database/DbField.cs).
- Related docs: [database-naming-conventions.md](database-naming-conventions.md), [database-schema-upgrade.md](database-schema-upgrade.md), [src/Polhem.Db/README.md](../../../src/Polhem.Db/README.md).
