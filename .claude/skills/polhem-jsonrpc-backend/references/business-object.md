# Business Object + demo login

Replace `Xxx` with your project name. Based on `QuickStart.Server` / `Polhem.Samples.Shared` and verified on a real
project's server.

## args / result (plain POCOs)

Put them in `Xxx.Server.Contracts` (listed in `AllowedTypeNamespaces`). Inherit `BusinessArgs` / `BusinessResult`;
**no** MessagePack attributes needed.

```csharp
using Polhem.Business;

namespace Xxx.Server.Contracts;

public sealed class GetLevelsArgs : BusinessArgs { }          // empty args still need a type

public sealed class GetLevelsResult : BusinessResult
{
    public List<Level> Levels { get; set; } = [];            // use settable properties
}
```

## Custom BO — pick the right base class

**Two base classes; choose by purpose**:

| Base class | When to use | What it brings |
|---|---|---|
| **`BusinessObject`** (`Polhem.Business`) | **Custom RPC BO** — you only want to expose your own actions | Minimal base, no CRUD; clean |
| `FormBusinessObject` (`Polhem.Business.Form`) | **ERP definition-driven form** — you want the framework's built-in `GetList`/`GetData`/`Save`/`Delete` for CRUD on a table | A full set of FormSchema-driven CRUD actions |

Most "the app's own business endpoints" should use **`BusinessObject`**; `FormBusinessObject` is for standard data
forms.

```csharp
using Polhem.Business;                 // BusinessObject
using Polhem.Definition;
using Polhem.Definition.Attributes;    // ApiAccessControlAttribute
using Polhem.Definition.Security;      // ApiProtectionLevel / ApiAccessRequirement
using Xxx.Server.Contracts;

namespace Xxx.Server.BusinessObjects;

public sealed class GameBO : BusinessObject
{
    // The 4-arg ctor must match the factory's Activator.CreateInstance(type, ctx, token, progId, isLocalCall).
    // The BusinessObject base only takes (ctx, token, isLocalCall) → just drop progId (the base does not use it).
    public GameBO(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
        : base(ctx, accessToken, isLocalCall) { }

    // Every action must be marked [ApiAccessControl] or it is rejected. Single args in, single result out.
    [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
    public GetLevelsResult GetLevels(GetLevelsArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new GetLevelsResult { Levels = /* seed or DB */ };
    }
}
```

> **Note the ctor difference**: the `BusinessObject` base is 3-arg `(ctx, token, isLocalCall)`, but the factory's
> `CreateFormBusinessObject` path (every progId other than System/AuditLog goes through it) uses a **4-arg**
> `Activator.CreateInstance`. So your BO still declares a 4-arg ctor (take progId, then pass the rest to the 3-arg
> base). `FormBusinessObject` is 4-arg itself. `IFormBoTypeResolver` can return any `Type` with a matching ctor; it is
> not limited to `FormBusinessObject`.

**`ApiProtectionLevel`**: `Public` (plaintext allowed) / `Encoded` (serialized + compressed) / `Encrypted` (also
encrypted) / `LocalOnly` (local calls only).
**`ApiAccessRequirement`**: `Anonymous` (no token) / `Authenticated` (login required).
Protection versus the `PayloadFormat` the client sends: Plain requires Public; Encrypted is allowed for anything that is
not LocalOnly.

## progId → BO: resolver (code, AOT-friendly)

```csharp
using Polhem.Business;
using Polhem.Business.Form;

namespace Xxx.Server.BusinessObjects;

public sealed class XxxFormBoTypeResolver : IFormBoTypeResolver
{
    public Type Resolve(string progId) => progId switch
    {
        "Game" => typeof(GameBO),
        _ => typeof(FormBusinessObject),   // unknown progId → the framework's default definition-driven CRUD
    };
}
```
(Or use the declarative `BusinessObject=` in `ProgramSettings.xml`, see define-config.md.)

## The demo login three-piece set

The framework's `SystemBusinessObject.AuthenticateUser` returns false by default → `System.Login` only succeeds if you
override it. The demo hard-codes one username/password pair and needs no `st_user` seed.

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
    public XxxAuthenticatingSystemBusinessObject(IBusinessObjectContext ctx, Guid accessToken, bool isLocalCall = true)
        : base(ctx, accessToken, isLocalCall) { }

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

### Factory

```csharp
using Polhem.Business;
using Polhem.Business.AuditLog;   // AuditLogBusinessObject
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Xxx.Server.Auth;

namespace Xxx.Server.BusinessObjects;

public sealed class XxxBusinessObjectFactory : IBusinessObjectFactory
{
    private readonly IServiceProvider _services;
    private readonly IDefineAccess _defineAccess;
    private readonly ISessionInfoService _sessionInfoService;
    private readonly ILanguageService _languageService;
    private readonly IFormBoTypeResolver _resolver;

    public XxxBusinessObjectFactory(
        IServiceProvider services, IDefineAccess defineAccess,
        ISessionInfoService sessionInfoService, ILanguageService languageService,
        IFormBoTypeResolver resolver)
    {
        _services = services; _defineAccess = defineAccess;
        _sessionInfoService = sessionInfoService; _languageService = languageService;
        _resolver = resolver;
    }

    public object CreateSystemBusinessObject(Guid accessToken, bool isLocalCall = true)
        => new XxxAuthenticatingSystemBusinessObject(BuildContext(), accessToken, isLocalCall);

    public object CreateFormBusinessObject(Guid accessToken, string progId, bool isLocalCall = true)
        => Activator.CreateInstance(_resolver.Resolve(progId), BuildContext(), accessToken, progId, isLocalCall)!;

    // IBusinessObjectFactory has had this member since 4.14.0; delegate to the framework default.
    // The compile error tells you which member is missing.
    public object CreateLogBusinessObject(Guid accessToken, bool isLocalCall = true)
        => new AuditLogBusinessObject(BuildContext(), accessToken, isLocalCall);

    private BusinessObjectContext BuildContext() => new()
    {
        DefineAccess = _defineAccess,
        SessionInfoService = _sessionInfoService,
        LanguageService = _languageService,
        BoFactory = this,
        Services = _services,
    };
}
```

Register `AddSingleton<IFormBoTypeResolver, ...>()` + `AddSingleton<IBusinessObjectFactory, ...>()` **after**
`AddPolhemFramework` (see backend-bootstrap.md).

## System methods (built into the framework, available to the client directly)

- `System.Ping` (anonymous) → health check
- `System.Login` (anonymous) → exchanges username/password for an `AccessToken` (Guid) + a per-session encryption key
- Later calls carry `Authorization: Bearer <token>`; auth-exempt: Ping / Login / GetApiPayloadOptions
