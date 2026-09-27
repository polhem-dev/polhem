# Polhem.Business: when to put a method on a BO interface

This file loads automatically when an agent touches any file under `src/Polhem.Business/` (nested `CLAUDE.md` files
are lazily loaded).

> This section used to live in `.claude/rules/definition.md`, but `IBusinessObject` /
> `ISystemBusinessObject` / `IFormBusinessObject` are all in **this project**, so it moved here on 2026-08-12.

## BO interfaces are a BO-to-BO decoupling layer, independent of the API surface

The axis interfaces are a **BO-to-BO decoupling layer**: a caller resolves by progId through
`IBusinessObjectFactory`, casts to the interface and calls it, without binding to a concrete BO class. That way,
customizing BOs on the host side (a multi-tenant host swapping a SystemBO subclass, a business replacing a FormBO
subclass) does not break callers.

**The two surfaces are independent; neither implies the other**: `[ApiAccessControl]` is the surface for the
**outside** (a client calling through `JsonRpcExecutor`), and the axis interfaces are the surface for **internal**
calls. **There is no hard rule**: a method open to the API does not have to be on an interface, and a method on an
interface does not have to be open to the API.

There is only one criterion. When adding a BO method, ask: "Will another BO, a background job or a schedule get it
through `_ctx.BoFactory.CreateXxxBO(...)` and call it?" Yes → put it on the interface; no → leave it off.
An interface that grows into "the set of all public methods" loses its meaning and adds to the host's customization
burden.

> **`CreateFormBO` / `CreateSystemBO` having zero callers in `src/` is expected, not dead code.**
> Inside the framework there is no BO-to-BO scenario (`JsonRpcExecutor` dispatches by progId and does not know which
> axis it is); the callers are **the host's business BOs**. The unused-type inventory on 2026-08-12 listed them as
> cleanup candidates; after checking, they were kept.
>
> **Not every axis needs an interface; only axes that "are called by another BO" do.** So `ILogBusinessObject`
> and `CreateLogBO` were removed on 2026-08-12 (their XML doc admitted they were "reserved for future", which is a
> reservation, not a need). The methods of `AuditLogBusinessObject` are still exposed through `JsonRpcExecutor` as before.
>
> **Count the server-side background callers too, not just the client.** This rule once wrongly listed `Login` as
> "client-only, not on the interface" (corrected 2026-08-12). It has real internal callers: **a background job logs
> in as some identity to establish a connection**, then acts as that user. The criterion was right; the mistake was
> missing background jobs as callers.
