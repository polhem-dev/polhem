# ADR-063: One company scope per request

## Status

**Accepted (2026-11-10)**

A breaking change within 1.x under [ADR-052](adr-052-breaking-changes-in-1-x-minors.md), released with
[ADR-062](adr-062-row-level-tenancy.md).

## Context

A session's company lives in `SessionInfo.CompanyScope`: the company, the user's roles in it, the customization and the
record-scope identity. `EnterCompany`, `LeaveCompany` and `Logout` replace the whole scope at once, so a reader that
takes it once sees one company's values together (`SessionCompanyScope`).

One request did not take it once. The authorization check (`CompanyAuthorizationService.Can`), the record scope
(`ScopeResolver.ResolveFilter`), the database router (`RepositoryDatabaseRouter.Resolve`), the company a repository
stamps rows with (`RepositoryBase`), the business object's `CompanyInfo`, its audit rules, the department tree and the
customization that picks the business object and repository types each read the session through the access token.

`EnterCompany` is open to any signed-in caller, and every request with the same token shares one cached `SessionInfo`.
A user with access to companies A and B could switch the token between them while sending requests, and one request
could then be authorized with A's roles and reach B's data. `RepositoryBase` read the company before and after routing
and refused a change, which caught one switch but not a switch away and back: routing read B, both checks read A, and
company A's id was written into company B's database. ADR-062 made that combination reach real rows, because companies
can now share a database.

## Decision

### 1. A request reads the company scope once

`BusinessObjectFactory` reads the session's `SessionCompanyScope` once per call and hands it to the business object
through `IBusinessObjectContext.CompanyScope`. It also picks the business object's type from the snapshot's
customization. The business object exposes it as `BusinessObject.CompanyScope`; one constructed without a snapshot reads
the session the first time the property is used.

### 2. The snapshot travels down, not the token

Every company-dependent service gains an overload that takes the snapshot, and the framework's business objects call it:

- `ICompanyAuthorizationService.Can(SessionCompanyScope, …)` and `IScopeResolver.ResolveFilter(SessionCompanyScope, …)`.
- `IRepositoryDatabaseRouter.Resolve(DbScope, SessionCompanyScope)`.
- `IRepositoryFactory.CreateFormRepository<T>(…, SessionCompanyScope)`, which puts the snapshot on the context it builds
  the repository with (`IRepositoryContext.CompanyScope`) and resolves the repository type from its customization
  (`IRepositoryTypeResolver.Resolve(string customizeId, string progId)`).
- `IBusinessObjectFactory.CreateBusinessObject(…, SessionCompanyScope)`, and `CreateFormBO` / `CreateSystemBO` overloads,
  so a business object that calls another hands on its snapshot.

The access-token overloads stay as public entry points for code that has no snapshot. Each reads the session once and
calls the snapshot overload.

### 3. A repository takes its database and its company from one read

A company-bound `RepositoryBase` takes both from the context's snapshot. Without one, it reads the session once and uses
that read for both. It no longer compares two reads, so it no longer throws when the company changes during
construction.

### 4. The write side is unchanged

`EnterCompany`, `LeaveCompany` and `Logout` still replace the scope in place. A request already running keeps acting for
the company it started with; the next request sees the new one.

### 5. A gate keeps it that way

`SessionCompanyScopeReadGateTests` (`tests/Polhem.Business.UnitTests`) reads the IL of `Polhem.Business`,
`Polhem.Repository` and `Polhem.ObjectCaching`. It fails when a method reads a company member of `SessionInfo`, or calls
an access-token overload of the services above, outside a short list of snapshot points, write-side methods and public
entry points, each listed with its reason. `FormBusinessObjectCompanyScopeSnapshotTests` switches the session's company
right after authorization and checks that the record scope and the repository still get the authorized company.

## Consequences

- `ICompanyAuthorizationService`, `IScopeResolver`, `IRepositoryDatabaseRouter`, `IBusinessObjectFactory` and
  `IRepositoryTypeResolver` gain members. They are not in the table of [ADR-046](adr-046-api-evolution-policies-for-1-0.md)
  decision 2, so a host that implements one of them adds the member. `IRepositoryFactory` is in that table.
  `IRepositoryContext.CompanyScope` has a default implementation (`null`, read the session), which is a real default.
- A host that overrides `RepositoryFactory.CreateFormRepositoryCore(Guid, string)` overrides the new
  `CreateFormRepositoryCore(Guid, string, SessionCompanyScope)` as well, because the framework's business objects create
  their repositories through it.
- Host business objects that read `SessionInfoService.Get(AccessToken)?.CompanyId` keep working but are not protected;
  they should read `CompanyScope`. The gate covers only the framework's own assemblies.
- A request costs one session read for its company instead of several.

## Alternatives considered

### Remove the double reads only

Read the session once inside each component: the router, `RepositoryBase`, `FormBusinessObject.ResolveCompanyInfo`. That
removes "one company's database, another company's id", but not "authorized for A, reading B", because authorization
and the data would still read separately. Rejected as insufficient for the release that introduces shared databases.

### Issue a new access token when the company changes

`EnterCompany` would return a new token and leave requests on the old one in their company. That isolates requests
completely, but it changes the protocol for every client: `PolhemApiClient`, the native heads, the Blazor heads and
polhem-connector-js would all have to swap tokens. Kept as a possible later step; not needed to make one request
consistent.

### Replace the access-token overloads instead of adding to them

Removing `Can(Guid, …)` and the others would force every caller onto a snapshot. It would also break every host that
calls them from code with no business object, such as a background job, and the gate already keeps the framework's own
code off them.
