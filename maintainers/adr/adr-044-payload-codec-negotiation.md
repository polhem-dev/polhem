# ADR-044: The body codec is declared by each request; JSON and MessagePack coexist

## Status

**Accepted (2026-09-03)**

## Context

The framework's wire body has only ever been MessagePack ([ADR-004](adr-004-messagepack-payload.md),
[ADR-036](adr-036-wire-serialization-externalized.md)), which is the right choice for both .NET and mobile. But to let
a front end in the browser call the JSON-RPC backend directly **while keeping encryption**, the whole client-side
payload pipeline would have to be rebuilt in that language.

The survey came out against intuition. **The encryption layer is very browser-friendly**: the RSA handshake is
2048-bit, SPKI public key, OAEP-SHA256, which maps to Web Crypto's `RSA-OAEP`; the symmetric layer is AES-256-CBC +
PKCS7 plus HMAC-SHA256, which maps to `AES-CBC` and `HMAC`; compression is gzip, which maps to `DecompressionStream`.
All of it is native API, and the private key does not even have to be exported.

**The real obstacle is the body.** Per [ADR-037](adr-037-wire-explicit-registration.md), wire types always register a
hand-written formatter explicitly; those formatters are binary contracts mapped key by key, known only to this
framework. Mirroring them in another language would create a second authoritative source for the same contract, and
`WireContractDriftTests` only guards the .NET end: for the cross-language half, **no mechanism would notice the two
sides drifting apart**.

By the `single-source` criterion, this is a structural problem, not a discipline problem.

## Relationship to ADR-014

[ADR-014](adr-014-jsonrpc-plain-public-default.md) already paved a path for pure JavaScript front ends: HTTPS as the
trust boundary, `PayloadFormat.Plain`, and for that purpose seven BO methods were downgraded from `Encrypted` to
`Public`.

**That decision still stands, and this decision does not replace it.** Plain is still the default path for JS front
ends, and those seven methods are still `Public`.

What this decision addresses is the half ADR-014 explicitly placed out of scope: **what a JS front end should do when
a deployment really does need application-layer encryption.** ADR-014 evaluated and rejected this in its section "Why
not build a 'JS encryption pipeline' for JS front ends". Its three reasons are worth comparing one by one, because the
premises of two of them have changed:

| ADR-014's reason | The situation now |
|---|---|
| "The JS and .NET encryption pipelines have to match byte for byte, and testing that is expensive" | This is exactly the problem `wire-fixtures/` solves: golden samples are produced and verified by .NET and compared across languages in both directions, so matching no longer relies on manual comparison |
| "A JS encryption pipeline would be implemented again in each of several front-end frameworks" | This is exactly why it is built as **one npm package** instead of letting each application write its own; each framework implementing it separately is a consequence of "there is no package", not of "there is encryption" |
| "The motivation for downgrading ProtectionLevel is that JS does not have to touch encryption; if JS still has to implement it, the effort is wasted" | **This one is a real difference of position**, and it is not a technical question |

The disagreement in the third is over "whether HTTPS is enough". ADR-014's answer is that it is, and for most
deployments it really is. But that answer shuts deployments that "need application-layer encryption" (intermediaries
with access after TLS termination, or compliance requirements to encrypt payloads) out of JS; their only options are
to switch to Blazor WASM or give up on a JS front end.

**This decision puts the choice back in the deployer's hands**: without a need for application-layer encryption, keep
using Plain, and ADR-014's path is completely unchanged; with that need, declare `codec: json` and use Encrypted. Both
share the same API and the same backend; the only difference is one field in the envelope.

It is worth noting that the other option ADR-014 mentions, "better to switch to Blazor WASM", still holds after this
decision, and each has its trade-offs: Blazor WASM removes everything cross-language, at the cost of the execution
environment and the front-end ecosystem.

## Decision

### 1. The body codec is declared by each request in the envelope

`ApiPayload` gains `Codec`, which travels in the plaintext envelope together with `format` and `type`. The server
decodes according to the declaration and **responds with the same codec**: a client that negotiated one codec cannot
decode another.

**Undeclared means MessagePack.** This is a necessity of compatibility, not a choice of default: every existing client
declares nothing and sends MessagePack. So the envelope of a request that declares no codec is **bit for bit the same
as before**.

The declaration goes in the envelope rather than in an HTTP header because the in-process local call path has no
headers at all; only in the envelope can both transports go through the same interpretation.

### 2. The codec is not a security property, so it can be negotiated

This should be read against [ADR-042](adr-042-api-replay-protection.md). The wire frame there is **deliberately not
negotiable**: letting a request declare "I carry no frame" would open the door to a downgrade attack, so whether a
frame is required is decided by a deployment switch that both ends read independently, and a mismatch fails.

The codec does not have that property: it decides how the body is **spelled**, not how well it is protected.
Encryption still wraps the outside, and the HMAC still covers the same bytes. Rewriting the codec name in transit
yields a body that cannot be decoded, not a body that is less protected.

**The difference between the two is not "whether to trust the client", but whether the field carries security
meaning.**

The name itself is still treated as untrusted input: its shape is validated first (lowercase alphanumerics and
hyphens, a length limit) before anything else, and a name that fails is rejected with a fixed message; arbitrary
caller text is not carried into the error message or into wherever it is logged.

### 3. Rejected: "mirror the MessagePack formatters in TypeScript"

This is the most intuitive option, and it **requires no change to the framework at all**, which is exactly what makes
it tempting.

There is only one reason for rejecting it, but it is enough: it trades "a one-time cross-language cost" for "a
long-term risk of cross-language drift". When a wire member is added on the `Polhem.Api.Core` side,
`WireContractDriftTests` turns red; the TypeScript mirror does not, and nothing reminds anyone. The symptom of drift
is fields silently disappearing or being misplaced, not an exception.

### 4. Rejected: "gate acceptance of the JSON codec behind a deployment setting"

During the implementation of this decision, an `AllowedSerializers` setting was added at one point, on the grounds that
"every acceptable codec is a parser an anonymous caller can reach", so it should be off by default and explicitly
turned on by the deployment. **That premise was wrong, and the setting has been removed.**

System.Text.Json has long been on the anonymous path: the JSON-RPC envelope itself is JSON, and a Plain body is even
deserialized directly into the target type by
[`ApiInputConverter`](../../src/Polhem.Api.Core/Conversion/ApiInputConverter.cs), while `System.Login` allows anonymous
calls. The JSON body codec introduces no new class of parser; that gate was guarding a door that was already open.

The cost, on the other hand, was real: whether a browser client works would depend on someone remembering to turn on
a setting; the symptom of forgetting is rejected calls, and the error message does not point back to that setting.

**This section is recorded here because "one more codec means one more attack surface" sounds very reasonable.** Why it
does not hold only becomes visible after actually tracing the parsing paths of the envelope and the Plain body.

### 5. `ApiPayloadOptions.Serializer` is removed

Once the codec is declared per request, this setting has only one role left: which codec the server decodes with when
the client declares none. And that answer **can only be MessagePack**: existing clients declare nothing and all send
MessagePack, so setting it to `json` would make all of them fail to decode. It is no longer a deployment choice but a
compatibility constant, and keeping it only leaves a foot-gun lying around.

By contrast, `Compressor` and `Encryptor` rightly stay in the settings file: they are **deployment policy** (whether to
protect, and with what), whereas the codec is **client capability** (which kind of body this client can produce).
This distinction is exactly why "only the serializer needs negotiating".

This is a **breaking change** (see "Consequences"). An existing `SystemSettings.xml` that still has the element is
harmless: `XmlSerializer` ignores unknown elements, so old settings files can be upgraded without being edited.

### 6. JSON needs its own discriminated envelope, reusing the same set of discriminator codes

[ADR-037](adr-037-wire-explicit-registration.md) established a discriminated envelope for `object`-typed members in
MessagePack. JSON needs the same thing, for an even more pressing reason: JSON cannot even tell primitive types apart.
`decimal` and `double` are both `1.0`, `Guid`, `DateTime` and `string` are all quoted text, and System.Text.Json reads
all of them back as `JsonElement`. **The symptom of the loss is a wrong value, not an error.**

The discriminator codes reuse the existing set from the MessagePack side, so a code means the same thing on both wires,
and `WireValueCodePinTests` pins both at once.

Three JSON-specific rules are worth writing down, because each of them is "it runs without this, but it is wrong":

- **`decimal`, `int64` and `uint64` go on the wire as JSON strings.** A JSON number is a double for every JavaScript
  reader, which can hold neither decimal precision nor integers above 2^53. Those readers are exactly who this codec
  mainly serves.
- **When an `object` member is null, the whole property disappears** rather than being written as `null`. Readers must
  treat "the property is absent" as null.
- **`DataTable` cells do not go through the envelope.** Their types are restored from the column metadata in the same
  document, so a DataTable's JSON shape is exactly the same as in a Plain payload, and readers only need to understand
  one shape.

### 7. Cross-language behavior is verified with golden samples, and only the raw body is pinned

The samples live in `wire-fixtures/` in the repository and are produced and verified by `WireFixtureTests`: if the
current encoding does not match a sample, the test fails, and the message says to regenerate them and read the diff
entry by entry. **That diff is the change description of the wire.**

The samples cover the **encoding rules** rather than enumerating each message type. One per type would produce a large
number of nearly isomorphic files while missing where things actually go wrong: discriminator codes, the DataTable
shape, camelCase, how enums are stringified. The message types themselves are property bags, and another language can
generate them from the type definitions.

**Only the raw body is pinned, not the bytes after compression or encryption.** gzip output is not guaranteed to be
the same across .NET versions, and AES-CBC uses a random IV for every message, so pinning is inherently impossible.
Those two layers are standard algorithms guaranteed by each language's own library; what needs pinning is the JSON
shape only this framework knows.

## Consequences

- A browser client can implement the complete encryption pipeline with native APIs only, without mirroring any custom
  binary contract.
- The envelope of a request that declares no codec is unchanged bit for bit, so the behavior of existing clients is not
  affected at all.
- **Breaking change**: `ApiPayloadOptions.Serializer` has been removed, and the version number decision at release time
  must take it into account.
- Additions to the public API surface: `ApiPayload.Codec`, `ApiServiceOptions.ResolvePayloadSerializer`,
  `PayloadCodecNames`, `JsonPayloadSerializer`, `ApiConnector.PayloadCodec`.
- A custom `IApiPayloadTransformer` that wants to serve negotiated codecs must implement the new overload; the default
  implementation **throws explicitly instead of falling back to the default codec**. A silent fallback would encode with
  a codec the caller did not ask for, while the other side decodes with the one it asked for. Calls that declare no
  codec still go through the existing overload, so existing implementations are not affected.
- **This decision exposed an existing defect and fixes it along the way**: a member declared as `FilterNode` (the
  filter condition of a list query) wrote only the discriminator code in JSON, and the operator and the whole subtree
  silently disappeared. `FilterGroup.Nodes` has long had a collection converter, but the single-node half did not, and
  encoded bodies had only ever gone through MessagePack, so the defect was only broken on the Plain path. For the fix
  see [`FilterNodeJsonConverter`](../../src/Polhem.Definition/Filters/FilterNodeJsonConverter.cs); it **must be applied
  on the property, not on the type**: applied on the base type, it is inherited by the subclasses and recurses
  infinitely until the stack is exhausted.
- Time zone responsibility does not move with the codec. [ADR-032](adr-032-datetime-timezone.md) places the conversion
  point in the Connector, and the server neither converts nor checks; a client in another language must take care of
  UTC normalization itself, and **the symptom of missing it is dates silently shifting, not an error**.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: the programmatic override of the default codec is gone too.** Decision 5 removed the `Serializer`
  setting, but `ApiServiceOptions.PayloadSerializer` could still be replaced in code. It is now read-only, and
  `ApiServiceOptions.Initialize` no longer takes a serializer. A codec the framework does not ship is added with
  `ApiServiceOptions.RegisterPayloadCodec`, which refuses the built-in names; a request that names no codec is always
  read with MessagePack (`src/Polhem.Api.Core/ApiServiceOptions.cs`).
