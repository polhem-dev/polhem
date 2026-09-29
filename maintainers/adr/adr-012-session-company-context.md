# ADR-012: Session company context model (two-phase session lifecycle)

## Status

Accepted (2026-05-15)

## Context

Polhem uses three kinds of logical database ([ADR-010](adr-010-logical-database-category.md)). For the `company`
category, the actual database is determined by "which company the user is currently working in". To support this
multi-company capability, the session has to carry "current company" information. But "verifying the user's
credentials" and "binding the company being worked in" are two independent concerns:

- A user usually enters credentials and is authenticated first, and **only then learns which companies they may
  enter**
- The same user may have access to several companies and needs to switch in the middle of a session
- Some cross-company operations (Login, Logout, Ping, writing the audit log and so on) happen "before entering a
  company" or "after leaving a company"

Putting both into a single `Login(account, password, companyId)` runs into structural problems:

1. **The user experience runs backwards**: you have to tell the system "which company I want to enter" before
   authentication can even start, but "which companies I can enter" is only known after authentication
2. **Switching cost**: switching company = a full logout + a new login, reissuing the token and rebuilding the
   encryption key
3. **Cross-company flows are stuck**: an operation such as "list the companies I can enter" has no suitable phase to
   run in
4. **Confused state semantics**: the in-between state "logged in but not in a company" has nowhere to be expressed

An explicit two-phase session lifecycle is needed: complete authentication first, then bind the company context.

## Decision

Introduce a **two-phase session model**, in which four symmetric methods form the complete lifecycle:

```
Login(account, password)   ←→  Logout()
EnterCompany(companyId)    ←→  LeaveCompany()
```

`SessionInfo` gains a `CompanyId` field (`string?`, nullable) to express the in-between state: `null` = logged in
but not in a company; non-null = currently bound to that company.

### Four key points

1. **`Login` only authenticates** and does not accept a `companyId`

   - On success it creates the `SessionInfo` (including `AccessToken`, `UserId`, `UserName` and so on), with
     `SessionInfo.CompanyId == null`
   - It returns the `AccessToken`, which the client uses to call every subsequent method
   - Afterwards, cross-company methods such as "list the companies I can enter" can be called (they share the
     `common` DB)

2. **`EnterCompany` binds the company context**, and overwriting it directly is how you switch

   - It verifies that the `CompanyId` exists in `ICompanyInfoService` (user-company permission checks will be added
     later)
   - It writes `SessionInfo.CompanyId`, overwriting the old value (first entry and switching use the same method;
     there is no two-step `LeaveCompany` + `EnterCompany`)
   - Switching is atomic: either the switch succeeds and points to the new company, or an exception is thrown and the
     original `CompanyId` is kept
   - It returns the complete `CompanyInfo` object to the client (for display)

3. **`LeaveCompany` is an explicit "leave the company but keep the session"**

   - It clears `SessionInfo.CompanyId` and keeps the rest of the session state
   - Idempotent: calling it on a session that has not entered a company does not raise an error, so the front end
     does not have to check the state first
   - Switching company does **not** go through the two steps `LeaveCompany` + `EnterCompany` (to avoid the
     intermediate state of a non-atomic operation); the main use of `LeaveCompany` is an explicit UX action such as
     "go back to the company selection page"

4. **`Logout` implies the `LeaveCompany` cleanup, then destroys the whole session**

   - Internal flow: clear `SessionInfo.CompanyId` (if not empty) → remove the whole session entry
   - Idempotent: it does not raise an error for a token that does not exist (so an attacker cannot use it to probe
     whether a token exists)
   - The caller does not need to call `LeaveCompany` before `Logout`; one method covers it

## Rationale

### Why `SessionInfo.CompanyId` is nullable (rather than defaulting to an empty string)

`null` and "a real empty-string CompanyId" mean different things. `null` states clearly "no company is bound", while
an empty string could be mistaken for a legitimate value. This project has nullable reference types enabled
everywhere, so adding the `?` annotation costs no extra maintenance.

### Why `Logout` implies `LeaveCompany`

Two API designs:

| Option | Assessment |
|------|------|
| **`Logout` cleans up internally ✅** | One action does it all, and the caller does not have to worry about order; the server can guarantee "when a session is destroyed, it is always cleaned up" |
| `Logout` requires the caller to call `LeaveCompany` first | Callers easily forget; if they do, the audit log shows the odd state "the session has a CompanyId but has already expired" |

The former was adopted, in line with the idempotent design principle: the caller does not have to remember several
steps.

### Why `CompanyAccessDenied` merges "no permission" and "does not exist"

`EnterCompany` can fail in two situations:

- The `CompanyId` does not exist
- The user has no access to the `CompanyId`

If the two used different error codes, an attacker could determine by repeated attempts which `CompanyId`s exist in
the system (a user enumeration attack). With both merged into a single `CompanyAccessDenied` (-32003, HTTP 403), a
user without permission cannot tell "really does not exist" from "exists but I cannot enter".

### Why `EnterCompany` overwrites directly instead of requiring `LeaveCompany` first

If switching company were split into two steps (`LeaveCompany` + `EnterCompany(newId)`), there would be three risks:

1. **Not atomic**: if an error or a network disconnection happens between the two steps, the session is stuck in the
   "not in a company" state
2. **A window of failed permissions**: between the two steps the UI may briefly try to call some company-bound BO
   method and be rejected
3. **Confused semantics**: "I want to switch to company B" is by nature an atomic intent and should not be expressed
   as two independent actions

With direct overwriting, switching is a single RPC: it either switches successfully or fails and rolls back, with no
intermediate state.

## Alternatives considered (evaluated and rejected)

1. **Single-phase `Login(account, password, companyId)`**
   - Reason for rejection: users usually need to authenticate before they can look up the list of companies they may
     enter; forcing them to choose a company first goes against how things work in practice

2. **Automatically bind the first accessible company after `Login`**
   - Reason for rejection: users need to choose explicitly (to avoid entering the wrong one; with several companies,
     "the first one" has no clear meaning)

3. **Omit `LeaveCompany` and switch purely by overwriting with `EnterCompany`**
   - Reason for rejection: it loses the ability to "leave explicitly"; there is no API for the UI to go back to the
     company selection page
   - Compromise adopted: keep `LeaveCompany`, but do **not** make it a required step before switching

4. **Omit `Logout` and rely on the token expiring naturally**
   - Reason for rejection: passive logout has a long delay (a token usually has a 1-hour TTL) and cannot support the
     UX of "the user explicitly logs out"; the audit log also needs an explicit logout event

5. **Use an enum instead of a string for `CompanyId`**
   - Reason for rejection: a companyId is a string value produced by deployment settings (it may contain a tenant
     code, a year and so on) and cannot be turned into an enum at the code level

## Consequences

### Session state transitions

```text
              Login()
   (none) ───────────────→ Logged-in
                          (CompanyId = null)
                              │
                              │ EnterCompany(A)
                              ↓
                          In Company A
                          (CompanyId = "A")
                              │
                       ┌──────┼──────┬──────────┐
                       │      │      │          │
              EnterCompany(B) │      │ Logout() │ LeaveCompany()
                       │      │      │          │
                       ↓      │      ↓          ↓
                  In Company B│   (none)    Logged-in
                              │
                              │ Logout()
                              ↓
                            (none)
```

### Valid and invalid call paths

| Path | Result |
|------|------|
| `Login → EnterCompany(A) → [business] → LeaveCompany → EnterCompany(B) → Logout` | ✅ |
| `Login → EnterCompany(A) → Logout` (implies LeaveCompany) | ✅ |
| `Login → Logout` (log out without entering a company) | ✅ |
| `Login → LeaveCompany` (Leave without entering a company) | ✅ idempotent |
| No `Login` → any method (except Anonymous ones such as `Login` / `Ping`) | ❌ `Unauthorized` (-32001) |
| `Login` done but no `EnterCompany` → a company-category BO method | ❌ `CompanyNotEntered` (-32002) |
| `EnterCompany(nonexistent or no permission)` | ❌ `CompanyAccessDenied` (-32003) |

### New error codes

| Error code | Value | HTTP mapping | Purpose |
|--------|------|----------|------|
| `Unauthorized` (existing) | -32001 | 401 | The session is invalid or expired |
| `CompanyNotEntered` (new) | -32002 | 409 | Logged in but not in a company, and a method that needs a company context was called |
| `CompanyAccessDenied` (new) | -32003 | 403 | `EnterCompany` failed (the company does not exist / no permission, deliberately merged) |

### Public API changes

| Scope | Change |
|------|------|
| `SystemBusinessObject` | Adds three public methods: `EnterCompany` / `LeaveCompany` / `Logout` |
| `ISystemBusinessObject` | Adds the interface declarations `Login` / `EnterCompany` / `LeaveCompany` / `Logout` (for cross-BO calls) |
| `SessionInfo` | Adds the `CompanyId` (string?) field |
| `Polhem.Definition.Identity.CompanyInfo` | **New** class (3 fields: `CompanyId` / `CompanyName` / `CompanyDatabaseId`) |
| `ICompanyInfoService` / `CompanyInfoCache` | **New**: modeled on the `ISessionInfoService` / `SessionInfoCache` pattern |
| `SystemActions` | Adds the `EnterCompany` / `LeaveCompany` / `Logout` constants |
| `JsonRpcErrorCode` | Adds `CompanyNotEntered` / `CompanyAccessDenied` |
| `SystemApiConnector` | Adds the matching client wrappers (one async and one sync each) |

## Trade-offs

### Company permission checks are deferred

For now `EnterCompany` only checks "whether the company exists" and **does not check user-company permissions**. The
full permission model (the user-company mapping schema, role / visibility rules) is taken over by a later ADR and
plan. `EnterCompany` already has a TODO comment marking the extension point; in the future a permission check only
needs to be inserted between the "existence check" and "writing SessionInfo". A permission failure uses the same
error code as "the company does not exist" (`CompanyAccessDenied`), so adding permission checks later does not
require changing the error code structure.

### A `CompanyInfo` cache miss does not leak the `CompanyId`

`EnterCompany` has verified that the `CompanyInfo` exists before writing `SessionInfo.CompanyId`, so in theory later
lookups of the `CompanyInfo` cache should not miss. If one does (for example because the cache was invalidated), the
error message does not leak the `CompanyId`, so an attacker cannot combine cache invalidation with differences in
error messages to probe IDs.

### `LeaveCompany` is unnecessary when switching

`LeaveCompany` takes no part in the "switch company" flow (`EnterCompany` overwrites directly), which may make some
developers feel it is "not much use". It is kept by design for:
- The explicit "leave the company" UX action (going back to the company selection page)
- Symmetry with `Logout` (`Logout` runs the `LeaveCompany` logic internally)
- If a degraded behavior such as "session timeout but stay logged in" is needed in the future, an API that clears the
  company context already exists

## Affected areas

| Scope | Impact |
|------|------|
| `src/Polhem.Definition/Identity/` | New `CompanyInfo` class and `ICompanyInfoService` interface; `SessionInfo` gains the `CompanyId` field |
| `src/Polhem.ObjectCaching` | New `CompanyInfoCache` and `CompanyInfoService`; `ICacheContainer` gains `CompanyInfo` |
| `src/Polhem.Business/System` | `SystemBusinessObject` gains 3 methods; the `ISystemBusinessObject` interface is updated |
| `src/Polhem.Api.Core` | New wire DTOs and contract interfaces for `EnterCompany` / `LeaveCompany` / `Logout`; `JsonRpcErrorCode` gains 2 values |
| `src/Polhem.Api.Client` | `SystemApiConnector` gains 3 pairs of async + sync wrappers |
| `src/Polhem.Definition/SystemActions.cs` | Adds 3 constants |
| Tests | 11 P3 EnterCompany tests + 6 P4 LeaveCompany tests + 6 P5 Logout tests + 4 P6 lifecycle integration tests |

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

- **2026-05-15: Company permission checks are implemented.** The deferral described under Trade-offs is resolved:
  `EnterCompany` now admits a user only when the company exists, is enabled and the user is granted it in the
  `st_user_company` table, and all three failures surface as the same `CompanyAccessDenied`. The check lives in
  `SessionCompanyBinder` (`src/Polhem.Business/Session/SessionCompanyBinder.cs`), called from
  `src/Polhem.Business/System/SystemBusinessObject.Session.cs`; session rebuild runs the same binder, so a revoked
  company permission takes effect when the session is rebuilt.
- **2026-07-23: Contract interfaces moved.** The `EnterCompany` / `LeaveCompany` / `Logout` contract interfaces now
  live in `src/Polhem.Api.Contracts/System/`; the wire DTOs stay in `src/Polhem.Api.Core/Messages/System/`.
- **2026-09-27: The company context is one immutable scope.** `SessionInfo.CompanyId` is no longer set on its own:
  the company id and everything snapshotted from the company (customization code, roles, record-scope row ids) form
  one immutable `SessionCompanyScope` (`src/Polhem.Definition/Identity/SessionCompanyScope.cs`), exposed as
  `SessionInfo.CompanyScope`. `EnterCompany` swaps it in a single write, and `LeaveCompany` / `Logout` reset it to
  `SessionCompanyScope.None`, so a concurrent request on the same session reads either the old company or the new
  one as a whole.

## Related

- [ADR-010: Logical database category (DbCategory)](adr-010-logical-database-category.md) — the `company` category
  DB is the main consumer of this ADR
