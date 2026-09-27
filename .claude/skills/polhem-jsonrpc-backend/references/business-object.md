# Business Object + custom credential check

Replace `Xxx` with your project name. Based on `samples/QuickStart.Server` and `samples/Polhem.Samples.Shared`; when
this file and those samples disagree, the samples win.

## args / result (plain POCOs)

Inherit `BusinessArgs` / `BusinessResult`; **no** serialization attributes.

```csharp
using Polhem.Business;

namespace Xxx.Server.Contracts;

public sealed class GetLevelsArgs : BusinessArgs { }          // empty args still need a type

public sealed class GetLevelsResult : BusinessResult
{
    public List<Level> Levels { get; set; } = [];            // use settable properties
}
```

Where these types live decides what a client can do with them:

- **Plain calls** (Public actions) carry JSON and are bound by property name, so a client can declare its own
  look-alike DTOs.
- **Encoded / Encrypted calls** name the body type on the envelope, and the server decodes the body into the action's
  parameter type. The client must send that exact type, so put args/results in an assembly both sides reference, and
  add its namespace to `AllowedTypeNamespaces` (define-config.md).

## Custom BO — pick the right base class

**Two base classes; choose by purpose**:

| Base class | When to use | What it brings |
|---|---|---|
| **`BusinessObject`** (`Polhem.Business`) | **Custom RPC BO** — you only want to expose your own actions | Minimal base, no CRUD; clean |
| `FormBusinessObject` (`Polhem.Business.Form`) | **ERP definition-driven form** — you want the framework's built-in `GetList`/`GetData`/`Save`/`Delete` for CRUD on a table | A full set of FormSchema-driven CRUD actions |

Most "the app's own business endpoints" should use **`BusinessObject`**; `FormBusinessObject` is for standard data
forms. (`QuickStart.Server`'s `EchoBusinessObject` derives from `FormBusinessObject`; that is a sample choice, not a
requirement: the resolver accepts any `BusinessObject` subclass for an ordinary progId.)

```csharp
using Polhem.Business;                 // BusinessObject
using Polhem.Definition;               // IBusinessObjectContext
using Polhem.Definition.Attributes;    // ApiAccessControlAttribute
using Polhem.Definition.Security;      // ApiProtectionLevel / ApiAccessRequirement
using Xxx.Server.Contracts;

namespace Xxx.Server.BusinessObjects;

public sealed class GameBO : BusinessObject
{
    // The factory calls Activator.CreateInstance(type, ctx, token, progId, isLocalCall), and the
    // BusinessObject base takes the same four arguments. Keep isLocalCall's default false: the base
    // documents why a hand-constructed BO must not be treated as a trusted local caller by default.
    public GameBO(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = false)
        : base(ctx, accessToken, progId, isLocalCall) { }

    // Every action must be marked [ApiAccessControl] or it is rejected. Single args in, single result out.
    [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
    public GetLevelsResult GetLevels(GetLevelsArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new GetLevelsResult { Levels = /* seed or DB */ };
    }
}
```

**`ApiProtectionLevel` / `ApiAccessRequirement`**: the values and what each one admits are in their XML docs
(`src/Polhem.Definition/Security/ApiProtectionLevel.cs`, `ApiAccessRequirement.cs`); this file does not copy them.
How a protection level combines with the `PayloadFormat` the client sends is in `ApiAccessValidator.ValidateAccess`:
a Plain call reaches only the lowest level, and a local call bypasses the check.

Put `[ApiAccessControl]` on each method. A class-level attribute publishes every public one-parameter method of the
class, including ones added later for internal use (`rules/security.md`).

## progId → BO: `ProgramSettings.xml`

```xml
<ProgramItem ProgId="Game" DisplayName="Game" BusinessObject="Xxx.Server.BusinessObjects.GameBO, Xxx.Server" />
```

`BusinessObject` is an **assembly-qualified type name** (`"Namespace.Type, AssemblyName"`). The whole file is in
define-config.md. `ProgramSettingsBoTypeResolver` reads it; its remarks describe the rules:

- no entry, or an empty `BusinessObject` → the framework default (`FormBusinessObject`, or the framework's own type
  for a reserved progId);
- a name that does not load, or a type that does not derive from the expected base → the request fails with a
  message naming the progId and the layer that declared it;
- a reserved progId (the list is `ReservedProgIds` in `Polhem.Business`, `System` among them) accepts only a type
  derived from the base that list names for it.

## Custom credential check (optional)

By default `System.Login` checks the password against the hash stored in `st_user`, so a seeded user with a hashed
password needs no code. To skip password hashing in a demo, override the check and nothing else. Login still reads
the user's locale from `st_user` and writes `st_session`, so both tables must exist either way.

### Credentials

```csharp
namespace Xxx.Server.Auth;

public static class XxxCredentials
{
    public const string UserId = "demo";
    public const string Password = "demo";
    public const string DisplayName = "Demo User";
}
```

### Authenticating System BO

```csharp
using Polhem.Business.System;
using Polhem.Definition;

namespace Xxx.Server.Auth;

public sealed class XxxAuthenticatingSystemBusinessObject : SystemBusinessObject
{
    public XxxAuthenticatingSystemBusinessObject(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = false)
        : base(ctx, accessToken, progId, isLocalCall) { }

    protected override bool AuthenticateUser(LoginArgs args, out string userName)
    {
        if (args is { UserId: XxxCredentials.UserId, Password: XxxCredentials.Password })
        {
            userName = XxxCredentials.DisplayName;
            return true;
        }
        userName = string.Empty;
        return false;
    }
}
```

Read the WARNING on `SystemBusinessObject.AuthenticateUser` before writing a real override: an unknown user and a
wrong password must stay indistinguishable, in the message and in the response time.

### Binding

Bind the reserved progId `System` to the subclass; no DI registration is involved:

```xml
<ProgramItem ProgId="System" DisplayName="System" BusinessObject="Xxx.Server.Auth.XxxAuthenticatingSystemBusinessObject, Xxx.Server" />
```

`samples/Define/ProgramSettings.xml` does exactly this for `DemoAuthenticatingSystemBusinessObject`.

## System methods (built into the framework, available to the client directly)

The full list is `SystemActions` (`src/Polhem.Definition/SystemActions.cs`) with each method's access declared on
`SystemBusinessObject`. The ones a new client meets first:

- `System.Ping` (anonymous, no API key required) → health check
- `System.GetCommonConfiguration` (anonymous) → the payload options a remote client adopts
  (`SystemApiConnector.InitializeAsync`)
- `System.Login` (anonymous) → exchanges username/password for an `AccessToken` (Guid) + a per-session encryption key
- Later calls carry `Authorization: Bearer <token>`
