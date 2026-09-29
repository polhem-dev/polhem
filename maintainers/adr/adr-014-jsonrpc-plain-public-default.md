# ADR-014: Opening JSON-RPC `Plain` — `Public` as the default protection level, HTTPS as the trust boundary

## Status

Accepted (2026-05-26)

**Supplemented by [ADR-044](adr-044-payload-codec-negotiation.md) (2026-09-03)** — the conclusions of this ADR
(Plain is the path for JS front ends; the seven methods stay `Public`) **still hold and are not superseded**.
ADR-044 additionally provides a path for "when a JS front end needs application-layer encryption", and responds point
by point to this ADR's assessment in "Why not build a 'JS encryption pipeline' for JS front ends".

⚠️ **Corrected on 2026-09-07** — throughout this ADR, "default" means **the developer's default choice**, not a
framework fallback. `ApiProtectionLevel` is a required parameter of `[ApiAccessControl]`; the framework has no
default value, **and there is no "not set" state**. Point 3 of "Three key points" originally contained a sentence
saying the opposite; it was corrected on the same day, see there for details.

## Context

By the v4.5 stage Polhem had completed the connection abstraction for three kinds of front-end host
([ADR-013](adr-013-frontend-api-connection-strategy.md)): the desktop uses the `Polhem.UI.Core` static singleton,
and Blazor Server / WASM use `Polhem.Web.*` with DI. All three share the `Polhem.Api.Client` communication layer, but
all assume the client is a .NET runtime — the full payload pipeline of RSA key exchange + AES-CBC-HMAC + MessagePack
serialization + gzip compression.

At the v4.6 stage a new kind of front end appeared: **pure JavaScript (React / Vue / Angular / vanilla)**, with no
.NET runtime to carry the encryption pipeline. Technically, the server's `PayloadFormat.Plain` already supported
sending `fetch + JSON` directly (`System.Text.Json` already implemented serialization of `DataSet` / `DataTable`),
but the default `ProtectionLevel` values of BO methods blocked this path:

| Method | Original `ProtectionLevel` | Impact on JS |
|------|--------------------|--------|
| `FormBO.GetNewData` / `GetData` / `Save` / `Delete` | `Encrypted, Authenticated` | JS cannot perform any CRUD |
| `SystemBO.EnterCompany` / `LeaveCompany` / `Logout` | `Encrypted, Authenticated` | JS cannot go through the full session lifecycle |
| `SystemBO.Login` / `Ping` / `GetCommonConfiguration` | `Public, Anonymous` | JS can call it |
| `<ProgId>.GetList` | `Public, Authenticated` | JS can call it |
| `System.GetDefine` / `SaveDefine` | `Public, Authenticated` | JS can call it (the sensitive scope is guarded by `IsLocalCall`) |

The `Encrypted` level **requires an encrypted payload**: the client must complete the RSA key exchange to obtain the
`ApiEncryptionKey`, and encrypt the request body with AES-CBC-HMAC. For a JS front end to take this path, it would
have to implement RSA + AES-CBC-HMAC + gzip + MessagePack inside the browser — **at that point switching to Blazor
WASM is more practical**, and the whole technical path loses its point of being open to JS.

### The reverse question: is it right to default every BO method to `Encrypted`?

Looking back at the history of the `Encrypted` default: early Polhem was deployed on intranets or in HTTP-only
environments and needed application-layer encryption to protect the payload. But at the v4.x stage:

- Production deployments generally use HTTPS (TLS 1.2+) — transport-layer encryption is already the industry baseline
- The double authentication of the `AccessToken` GUID + `X-Api-Key` is already enough to block unauthorized calls
- The real value of application-layer encryption is "**access by intermediaries after TLS termination**" (log
  aggregators, APM proxies, CDN edges). This matters for **specific** highly sensitive methods (changing a password,
  exchanging encryption keys) and is **not needed** as a default for every method

In other words: making `Encrypted` the default is **a reverse precaution that applies "the highest security level" to
every method**, while in practice the payload of most BO methods (DataSet contents, switching companyId and so on) is
adequately protected by falling inside TLS encryption.

## Decision

**`Public` is the default protection level for BO methods, and HTTPS is the trust boundary; only specific methods
that the developer explicitly judges to need application-layer encryption are marked `Encrypted`.**

### Three key points

1. **Downgrade 7 BO methods to `Public + Authenticated`**

   | Method | `ProtectionLevel` change |
   |------|----------------------|
   | `FormBO.GetNewData` / `GetData` / `Save` / `Delete` | `Encrypted` → `Public` |
   | `SystemBO.EnterCompany` / `LeaveCompany` / `Logout` | `Encrypted` → `Public` |

   - `AccessRequirement = Authenticated` stays unchanged: the identity threshold is not relaxed
   - Application-layer business permission checks (who can edit which DataSet / who can enter which company) are not
     within the scope of `ProtectionLevel`; the BO layer guards them itself

2. **`ApiAccessValidator` allows "a higher-level format calling a lower-level method" (backward compatible)**

   An existing `.NET` client calling a downgraded method in `Encrypted` format is still allowed. `Encrypted ≥ Public`
   is legitimate "**over-encryption**", and callers are not forced to follow the downgrade. This guarantees:
   - Existing desktop / Blazor clients can keep calling **without any change**
   - Upgrading to v4.6 does not break existing deployments

3. **`Encrypted` is still a valid option, but it has to be marked explicitly**

   Designers of future BO methods must **actively assess** which methods need `Encrypted` (such as changing a password
   or generating encryption keys), instead of marking everything by default. This turns "needs application-layer
   encryption" from a **global default** into a **case-by-case decision**.

   > ⚠️ **Correction (2026-09-07)**: this item originally contained the sentence "when `ProtectionLevel` is not set,
   > the server takes the established default (`Public`)". **That is the opposite of the implementation, and it never
   > held.** On the day this ADR was adopted ([`aa843f71`](https://github.com/jeff377/bee-library/commit/aa843f71),
   > 2026-05-26), `ApiAccessValidator.ValidateAccess` already threw `UnauthorizedAccessException` for a method with no
   > declaration found (its remarks state that such a method “is denied, not treated as unrestricted”), and the
   > `protectionLevel` of `ApiAccessControlAttribute` was already a required parameter that day. **Current state**: a
   > method not covered by `[ApiAccessControl]` is always denied, and at build time `POLHEM3001` also points out such
   > methods (under `TreatWarningsAsErrors` that is a compile failure). The original sentence was removed rather than
   > kept as a record — it was not a decision but a description of a mechanism sitting inside a decision item, and it
   > pointed in the opposite direction from this item's decision (you must actively assess), in effect offering a way
   > out of assessing at all.

## Rationale

### Why trust HTTPS as the transport-layer baseline

`Encrypted` is the strongest ProtectionLevel, and it addresses the threat model "**TLS is broken by a
man-in-the-middle**". In practice:

- A production HTTPS configuration (TLS 1.2+, HSTS, HPKP / Certificate Transparency) is already a basic deployment
  requirement
- If TLS is broken, the AES-CBC-HMAC keys of the encrypted payload are also exchanged through the same TLS channel via
  the RSA key exchange, so they **leak just the same**
- The scenario where `Encrypted` has real value is blocking intermediary access after TLS decryption (such as a cloud
  APM proxy capturing the unencrypted body); applying that to every method is over-engineering

### Why not build a "JS encryption pipeline" for JS front ends

Letting JS bring its own AES-CBC-HMAC + RSA implementation (the Web Crypto API already supports it) was evaluated, but
rejected because:

- The JS and .NET encryption pipeline implementations would have to match byte for byte (IV randomness, HMAC bit
  alignment, gzip wrapping), which makes testing expensive
- Even if achieved, **the whole motivation for downgrading ProtectionLevel is to spare JS from touching the encryption
  pipeline**; if JS still had to implement RSA + AES-CBC-HMAC, this ADR would be wasted effort — better to switch to
  Blazor WASM (which already has a complete implementation)
- A JS encryption pipeline would be reimplemented separately in several front-end frameworks (React / Vue / Angular),
  and the maintenance cost would grow exponentially

JS takes the double line of protection of `Plain` + HTTPS + Bearer Token, consistent with the strategy of ADR-013
Family B (`Polhem.Web.*` uses `RemoteApiProvider`).

### Why the downgrade does not affect existing deployments

The decision logic of `ApiAccessValidator` is **"actual payload format" ≥ "declared level of the method"**:

| Actual format | Declared level of the method | Result |
|---------|------------|------|
| `Encrypted` | `Public` (after downgrade) | ✅ Allowed (over-encryption is legitimate) |
| `Encoded` | `Public` (after downgrade) | ✅ Allowed |
| `Plain` | `Public` (after downgrade) | ✅ Allowed |
| `Plain` | `Encrypted` (not downgraded) | ❌ Rejected |

After the downgrade, requests from `Encrypted` clients still fall in the ✅ rows and are not rejected.

### Why `Login` was already `Public + Anonymous`

Historically `Login` is the only method that has to be called at the stage where "**there is no AccessToken yet**",
so it must allow anonymous calls. It is also the entry point of the RSA key exchange (`ClientPublicKey` is exchanged
here), so it cannot itself require `Encrypted` (otherwise it would be a chicken-and-egg problem). This existing design
**incidentally** paved the way for opening everything to JS front ends: JS calls `Login` in Plain (with
`ClientPublicKey` as an empty string), the server short-circuits the encryption negotiation and returns the
`AccessToken`, and subsequent calls carry `Authorization: Bearer <token>`. This ADR **extends that existing
single-point mechanism into a general pattern**.

## Alternatives considered (evaluated and rejected)

1. **Keep the `Encrypted` default and have JS front ends implement the encryption pipeline with WebCrypto**
   - Reason for rejection: see "Why not build a 'JS encryption pipeline' for JS front ends"

2. **Add a `JsAuthorized` level (a JS-only authentication mode)**
   - Reason for rejection: it equals `Public + Authenticated`, so the extra name makes no real difference; and it
     would mislead readers by implying "JS clients and .NET clients take different authentication paths"

3. **Keep `Encrypted` as the default and downgrade only these 7 methods**
   - Partly adopted: this change does downgrade only 7 methods. But "**the default for methods added in the future**"
     is a separate decision. This ADR explicitly sets **the default for new methods to `Public`**, so that not every
     new method has to go through a downgrade

4. **Retire `ProtectionLevel = Encrypted` (remove it from the enum entirely)**
   - Reason for rejection: application-layer encryption still has value in specific scenarios (highly sensitive
     methods such as changing a password or generating keys) and should not be removed entirely

5. **Express "callable from JS" with a `[JsAccessible]` attribute and leave `ProtectionLevel` alone**
   - Reason for rejection: in essence it is still "does this method's payload require encryption", so adding another
     attribute to express the same meaning is redundant; and two independent attributes easily drift out of sync over
     time

## Consequences

### The full API surface callable from JS front ends

After the downgrade, within the **Authenticated (AccessToken required)** zone, JS front ends can call through
`PayloadFormat.Plain`:

| Method | `ProtectionLevel` | Purpose |
|------|------------------|------|
| `System.EnterCompany`* | Public | Enter a company |
| `System.LeaveCompany`* | Public | Leave a company |
| `System.Logout`* | Public | Log out |
| `System.GetDefine` | Public | Get definitions such as FormSchema / TableSchema |
| `System.SaveDefine` | Public | Write definitions (guarded by `IsLocalCall`) |
| `System.GetFormSchema`† | Public | Get a FormSchema in a JSON-friendly way |
| `System.GetFormLayout`† | Public | Get a FormLayout in a JSON-friendly way |
| `<ProgId>.GetList` | Public | List query |
| `<ProgId>.GetNewData`* | Public | Get a blank DataSet |
| `<ProgId>.GetData`* | Public | Get a single record |
| `<ProgId>.Save`* | Public | CRUD save |
| `<ProgId>.Delete`* | Public | Delete |

`*` is opened by this ADR's downgrade; `†` is a JSON-native getter added alongside.

**Anonymous (no AccessToken required)** zone: `System.Ping` / `GetCommonConfiguration` / `Login` / `CreateSession`
were all already Public and are outside the scope of this ADR.

### Methods not changed

`CheckPackageUpdate` / `GetPackage` stay `Encoded`, because these two are the package update mechanism on the `.NET`
runtime side, and JS front ends have no matching need.

> **Note (2026-09-04)**: `CheckPackageUpdate` and `GetPackage` **have both been removed**. They were extension points
> whose base threw `NotSupportedException`, and never had an actual consumer — they were not for JS front ends (as
> this section says), and `Polhem.Api.Client` had no matching connector methods either, while the direct consumer of
> the API contract is the connector. This section is kept as a record of the decision at the time.

### Security model

| Attack vector | Line of defense |
|---------|-------|
| Unauthenticated calls | `AccessRequirement.Authenticated` guards the validity of the `AccessToken` |
| Token theft (network sniffing) | HTTPS / TLS 1.2+ |
| Token theft (client side) | The client's own responsibility (browser localStorage against XSS, in-process on the desktop against memory dumps) |
| Cross-origin calls | CORS configuration (already added in QuickStart.Server; production must restrict origins) |
| Intermediary access to the payload (after TLS termination) | Specific highly sensitive methods are still marked `Encrypted` (not in this ADR's downgrade list) |
| Application-layer permission failures | Business checks in the BO layer (such as the company permission check of `EnterCompany`, and data scope filtering in the Repository) |

### Obligations for developers

- **When adding a BO method, default to `Public + Authenticated`**; no longer mark `Encrypted` by default
- If the method's payload is sensitive data that "**must not leak after TLS termination**" (highly sensitive data such
  as passwords, keys, PII), **mark it `Encrypted` proactively** and explain why in the PR
- When deploying a production host, **HTTPS is mandatory** as a precondition (HTTP-only deployments are no longer
  considered a valid configuration)

## Related

- [ADR-013: Front-end API connection strategy](adr-013-frontend-api-connection-strategy.md) — the HTTPS + Bearer Token
  security model of Family B (`Polhem.Web.*`) is consistent with this ADR
- [JSON-RPC front-end integration guide](../../docs/en/api/jsonrpc-frontend-integration.md) — the public document for JS / TS
  developers
- [Polhem.Api.Core README](../../src/Polhem.Api.Core/README.md) — the level-checking logic of `ApiAccessValidator`
- [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) — the TypeScript client for JS front ends

## Out of scope

- **Retiring `ProtectionLevel = Encrypted` from the enum entirely** — application-layer encryption still has value in
  specific scenarios and is kept as an opt-in level above `Public`
- **Implementing a JS encryption pipeline** — see the reason for rejecting "Alternative 1"; if a real need appears in
  the future, it is treated as a separate ADR
- **DTO codegen / automatic TypeScript generation** — a toolchain topic, unrelated to the `ProtectionLevel` decision
- **The CORS default for cross-origin calls** — each host decides, independently of the protection level of BO
  methods
- **Packaging as an NPM package** — the TypeScript client is its own repository,
  [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js)

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

- **2026-09-27: Anonymous and authenticated calls are told apart by `[ApiAccessControl]` alone.** The hand-kept list
  of methods exempt from the Bearer header was removed: a request without an `Authorization` header is an anonymous
  call, and the method's declared `ApiAccessRequirement` decides whether it is admitted. A missing, unknown or expired
  access token on a method that needs one is answered with JSON-RPC `Unauthorized` (-32001) through
  `AuthenticationRequiredException` (`src/Polhem.Base/Exceptions/AuthenticationRequiredException.cs`), not with an
  HTTP 401.
- **2026-09-27: Plain requests carry typed filter and parameter values.** Object-typed filter and parameter values in
  a `Plain` request are bound by their JSON kind (string, integer, decimal, boolean, array) by
  `PlainValueJsonConverter` (`src/Polhem.Api.Core/Json/`), so a `Plain` `GetList` with a valued filter reaches the SQL
  parameter with a usable value.
- **2026-09-27: `GetDefine` has a remote allow-list.** A remote caller may read only the definition types the shipped
  clients need (FormSchema, FormLayout, Language, MenuSettings, CurrencySettings and UnitSettings); table schemas and
  every other type are refused to a remote caller
  (`src/Polhem.Business/System/SystemBusinessObject.Define.cs`). The `System.GetDefine` row in "The full API surface
  callable from JS front ends" describes the state at the time of the decision.
- **2026-09-27: A JavaScript/TypeScript client exists.** The "Packaging as an NPM package" item under "Out of scope"
  has moved on: the client lives in its own repository,
  [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js), which also covers the encrypted path
  that [ADR-044](adr-044-payload-codec-negotiation.md) opened.
