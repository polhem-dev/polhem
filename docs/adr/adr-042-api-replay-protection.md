# ADR-042: API replay protection: a wire frame inside the encrypted envelope

[繁體中文](adr-042-api-replay-protection.zh-TW.md)

## Status

**Accepted (2026-09-01)**

## Context

When a legitimate JSON-RPC packet was resent unchanged, the server used to execute it a second time in full.
The payload pipeline established by [ADR-036](adr-036-wire-serialization-externalized.md) uses AES-CBC-HMAC to
guarantee that a packet **cannot be altered**, but not that it **has not been sent a second time**: encryption
protects confidentiality and integrity, not against repetition.

An attacker does not need to be able to decrypt the packet; resending the whole request unchanged is enough. The ways
to obtain one, from most to least likely: a legitimate but malicious client (capturing and resending its own packets,
against which TLS is completely useless), leaked logs (a gateway or APM recording full request bodies), a corporate
MITM proxy, and internal deployments without TLS.

The most easily underestimated is the first: the attacker is often **a legitimately logged-in user** who resends the
packet for their own already-approved document dozens of times. Transport-layer encryption does nothing against this.

## Decision

### 1. The frame goes inside the encrypted envelope, not into plaintext fields of `ApiPayload`

Replay protection needs a binding the attacker cannot forge, and a binding needs a secret. `Format` and `TypeName` of
`ApiPayload` are a **plaintext envelope**; only `Value` is encrypted. Adding `Timestamp` / `Sequence` properties to it
would put both values in the plaintext layer, where the attacker can rewrite them at will (to the current time, to a
larger sequence number). That makes a brand-new legitimate request, which is no protection at all.

The correct approach is to prepend the frame to the bytes after `Encode` (serialize + compress) and before `Encrypt`:

```
[ version(1) | timestamp(8, Unix ms) | sequence(8) ] ++ body
```

All three are big-endian, and version 1 is a fixed 17 bytes. For the implementation see
[`ApiPayloadFrame`](../../src/Polhem.Api.Core/JsonRpc/ApiPayloadFrame.cs) and
[`ApiPayloadConverter`](../../src/Polhem.Api.Core/JsonRpc/ApiPayloadConverter.cs).
The frame hangs on `ApiPayload.Frame` (`[JsonIgnore]`) so callers can access it, but it is not serialized with the
envelope.

### 2. The version byte exists because the frame cannot describe its own length

The frame has no length prefix, and there is no separator before the body that follows it: the reader must know how
many bytes to consume before it touches the body. If the frame ever gains a field, the old and new lengths differ and
there is no way to tell which is which; without a version, the only option would be another all-at-once breaking
upgrade.

**In version 1 this byte contributes nothing to security**: an attacker can forge it, but still has to get past the
HMAC. Its only role right now is to give an old client a clear error, instead of reading random body bytes as a
timestamp and reporting an absurd clock skew.

### 3. The strength of the protection depends on `PayloadFormat`, and the documentation must be honest about it

| Format | HMAC? | Effect |
|--------|-------|--------|
| `Encrypted` | Yes | Complete: the frame cannot be altered |
| `Encoded` | No | Only blocks naive unchanged resends; an attacker who alters packets can alter the frame |
| `Plain` | No | No protection, and no frame |

The `Encoded` row is **a limitation, not a defect**, but no outward-facing description may claim that it protects
against replay.

### 4. Whether a frame is present is decided by deployment settings, not by the packet itself

Both ends read the same switch, `ApiServiceOptions.RequireWireFrame`. **The server does not "detect" whether a frame is
present**: once "if it looks like there is no frame, treat it as having none" is allowed, an attacker can turn off the
protection just by removing the frame, which is exactly the downgrade attack to be prevented.

The cost is that mismatched settings on the two ends always fail, and that is deliberate. The switch is off by default
(behavior exactly the same as before this was introduced), and the order for enabling it is: **upgrade the packages on
both ends first, then turn on the switch on both ends together**.

### 5. Sequence numbers use a sliding window, not a nonce set

A nonce set needs unbounded storage or a database round trip every time. Instead, a per-session monotonically
increasing sequence number plus a 64-bit bitmap is used (the IPsec anti-replay window, the approach of RFC 6479): each
session stores only `highest` and the bitmap, 16 bytes in total, the check is a few bit operations, and there are
**zero database round trips**. For the implementation see
[`ReplayWindow`](../../src/Polhem.Api.Core/JsonRpc/ReplayWindow.cs).

Tolerating out-of-order arrival is a requirement, not an extra benefit: taking a number is atomic, but concurrent
requests do not arrive in a fixed order, and strictly increasing numbers would reject normal traffic by mistake.

Three derived decisions:

- **The window lives for 2× the timestamp tolerance.** A replay with an old sequence number necessarily has an
  expired timestamp too and is already stopped by the time-window check, so the window only matters within that
  period. This decouples cleanup completely from the session lifecycle, and the memory bound is "the number of
  sessions active within the time window".
- **Forward jumps are capped (`MaxForwardJump`).** Without a cap, a single integer arithmetic mistake on a client that
  sends a sequence number close to `long.MaxValue` would leave every later normal request of that session outside the
  window, stuck: the token is valid and the key is correct, yet everything fails, which is almost impossible to
  diagnose.
- **Anonymous calls do not check sequence numbers.** Sequence numbers are per session, and anonymous calls share the
  same empty token; if they were checked too, different clients would use up each other's sequence numbers and cause
  large numbers of false rejections.

### 6. Declared per method, not applied globally

`ApiAccessControlAttribute` gains a third dimension,
[`ApiReplayProtection`](../../src/Polhem.Definition/Security/ApiReplayProtection.cs), with a default of `None`.
Replaying a query method is harmless, and applying it everywhere would only add a check to every call.

The methods that currently declare `UniqueSequence` are `Save`, `Delete`, `ExecFunc`, `EnterCompany` and
`LeaveCompany`: this is the complete set of "remotely reachable and with side effects". The other write methods
(`SaveDefine`, `SaveCustomizePluginSettings`, `SetDeploymentAdmin`) are all `LocalOnly` and cannot be called remotely.

The new dimension is **a property, not a constructor parameter**: adding an optional parameter to a published public
constructor is a binary breaking change.

### 7. Degradation on multiple nodes is acceptable, and a way out is left open

The default implementation of `IReplayWindowStore` is process-local. With multiple nodes and no token affinity, each
node holds its own window, so **the maximum number of replays equals the number of nodes, not infinity**: an order of
magnitude better than "unlimited replays within the time window". A deployment that needs strong consistency across
nodes can replace it with a shared implementation without changing the framework.

## Explicitly out of scope

- **Idempotency keys.** Sequence numbers solve "reject replays"; idempotency keys solve "retry safely". Neither can
  replace the other. If a request times out after taking a number, resending it with the same number is rejected by
  the window (even if the server actually processed it successfully and only the response was lost), while using a
  new number makes the business layer execute twice. Both are wrong, because this is not a problem sequence numbers
  can solve. **Once sequence checking is enabled, a resend after a timeout fails instead of succeeding as a retry**;
  scenarios that need safe retries should implement idempotency keys themselves. The framework currently has no
  automatic retry mechanism, so enabling it does not break existing framework behavior, but retry loops wrapped by
  the application layer, and a user manually choosing "submit again", will both run into it.
- **Tightening `ApiProtectionLevel.Public`.** `Save` / `Delete` / `ExecFunc` currently allow calls in `Plain`; that
  path carries no frame and is not checked, so it is a downgrade detour. Raising the protection level would require
  callers to implement MessagePack, compression, AES-CBC-HMAC and RSA key exchange; JS callers cannot do that whole
  set, and raising it rashly would lock them out outright. This issue needs its own evaluation and is handled
  separately.

  **The real cost of the detour is smaller than it looks**: to exploit it, an actor has to know the packet's contents
  to construct an equivalent `Plain` request, and the main threat, an attacker who picks up an encrypted packet and
  resends it unchanged, cannot read the contents and cannot construct one. Those who can take the detour know the
  contents and hold a valid token, and such a person could send arbitrary requests directly anyway.
- **Business-layer guards are not dropped because of this.** State machine checks (an approved document cannot be
  approved again) and optimistic-locking version numbers should still exist independently; they also stop non-attack
  cases such as a user double-clicking submit.

## Consequences

- The switch is off by default, so introducing this has **zero behavior change** in itself.
- Once enabled, every Encoded / Encrypted request carries 17 more bytes; the response direction carries a frame too (a
  natural result of both ends sharing the same converter), which the client strips and discards without checking, so
  that direction is currently pure overhead.
- A replay rejection returns a dedicated error code, `JsonRpcErrorCode.ReplayRejected` (-32005), so callers can tell
  "retrying will not succeed" from "invalid credentials"; and it is recorded as
  [`AnomalyKind.Replay`](../../src/Polhem.Definition/Logging/AnomalyKind.cs) rather than the generic `Error`. Folded
  into `Error`, the signal "a session is being rejected repeatedly" would disappear, and that is exactly what tells a
  client clock skew from someone resending packets.
- Local calls (`IsLocalCall`) are not affected.
