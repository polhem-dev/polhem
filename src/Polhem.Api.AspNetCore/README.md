# Polhem.Api.AspNetCore

> ASP.NET Core controller library providing a unified JSON-RPC 2.0 API endpoint.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: API Layer (hosting)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Consumed by application code: the user inherits the controller.

## Target Frameworks

- `net10.0` -- ASP.NET Core hosting requires the modern runtime

## Usage

Register the framework with `AddPolhemFramework` (`Polhem.Hosting`, see its README), derive a controller, and call
`UsePolhemFramework` once the application is built:

```csharp
// Controllers/ApiController.cs
using Polhem.Api.AspNetCore.Controllers;

public sealed class ApiController : ApiServiceController { }
```

```csharp
// Program.cs, after builder.Services.AddPolhemFramework(...) and builder.Services.AddControllers()
var app = builder.Build();
app.UsePolhemFramework();   // using Polhem.Api.AspNetCore;
app.MapControllers();
app.Run();
```

`UsePolhemFramework` runs the host-side startup checks. Today it logs while no API key has been issued, because until
then the `X-Api-Key` header is only checked for presence: an error, or a warning when the host runs in the Development
environment. Startup still proceeds either way.

## Key Features

### Single POST Endpoint

- Exposes a single `POST /api` route decorated with `[ApiController]` and `[Produces("application/json")]`.
- Validates `Content-Type: application/json` before processing; returns `415 Unsupported Media Type` for other media types.

### Async Request Pipeline

- `PostAsync` orchestrates the lifecycle: read request, validate authorization, execute handler.
- Each stage is a `protected virtual` method that subclasses can override independently.

### JSON-RPC Request Parsing

- `ReadRequestAsync` reads the raw body, deserializes it into a `JsonRpcRequest`, and validates the `Method` field.
- Returns structured `JsonRpcException` errors for empty bodies, missing methods, and malformed JSON.

### Authorization Validation

- `ValidateAuthorization` receives the `X-Api-Key` and `Authorization` header values bound via `[FromHeader]`,
  checks the API key through `ValidateApiKey` (the registered `IApiKeyValidator`), and delegates the rest to
  `ApiServiceOptions.AuthorizationValidator`.
- Returns `401 Unauthorized` with a JSON-RPC error when validation fails.

### Request Execution

- `HandleRequestAsync` hands the request to the `JsonRpcExecutor` with the validated access token and the request's
  cancellation token, and returns the result as `application/json`.
- A request the client abandoned is answered with status 499. An exception that escapes the executor returns
  `500 Internal Server Error`, with the root message in Development and an empty message otherwise.

### Structured Error Responses

- `CreateErrorResponse` produces a consistent `JsonRpcResponse` with error code, message, and optional data payload, mapped to the appropriate HTTP status code.

## Extension Points

`PostAsync` is the public action; the other members are `protected` and are used from your derived controller.

| Class / Member | Purpose |
|----------------|---------|
| `ApiServiceController` | Abstract base controller; inherit and register in your ASP.NET Core app |
| `PostAsync` | Entry point for all JSON-RPC requests (`POST /api`) |
| `ReadRequestAsync` (virtual) | Parses and validates the JSON-RPC request body |
| `ValidateAuthorization` (virtual) | Checks the API key and the Bearer token |
| `ValidateApiKey` (virtual) | Runs the API key check through the registered `IApiKeyValidator` |
| `ApiKeyValidation` | The API key verdict for the current request, set by `ValidateAuthorization` |
| `HandleRequestAsync` (virtual) | Dispatches the request to `JsonRpcExecutor` |
| `CreateErrorResponse` (virtual) | Builds a standardized JSON-RPC error response |
| `IsDevelopment` | Whether the host environment is Development (`false` when it cannot be resolved) |
| `PolhemFrameworkApplicationBuilderExtensions.UsePolhemFramework` | Host-side startup checks |

## Design Conventions

- **Template Method Pattern** -- `PostAsync` defines the pipeline skeleton; `ReadRequestAsync`, `ValidateAuthorization`, `ValidateApiKey`, `HandleRequestAsync` and `CreateErrorResponse` are `virtual` for selective overriding.
- **Development vs. Production error messages** -- exception details are included only when `IsDevelopment` is `true`.
- **No direct dependency on DI container** -- services are resolved from `HttpContext.RequestServices` so the controller works in any ASP.NET Core host without additional setup.
- **External dependency**: `FrameworkReference: Microsoft.AspNetCore.App`.

## Directory Structure

- `Controllers/ApiServiceController.cs` -- the abstract base controller
- `PolhemFrameworkApplicationBuilderExtensions.cs` -- `UsePolhemFramework`
