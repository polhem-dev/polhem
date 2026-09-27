# Source code scanning

This rule covers the secure coding requirements that common SAST tools (CodeQL, SonarQube, Roslyn Analyzers) all
check.

## SQL injection

- **Do not** build SQL statements by string concatenation or `string.Format`
- Always pass parameters through the `{0}` placeholders of `DbCommandSpec`; the framework parameterizes them

```csharp
// ✅ Correct: placeholder
var cmd = new DbCommandSpec(DbCommandKind.Scalar,
    "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", userId);

// ❌ Forbidden: string concatenation
var cmd = new DbCommandSpec(DbCommandKind.Scalar,
    $"SELECT COUNT(*) FROM st_user WHERE sys_id = '{userId}'");
```

## Releasing resources (IDisposable)

- Every object that implements `IDisposable` must have its lifetime managed with `using` or `await using`
- Do not scatter manual `.Dispose()` calls through the code (an exception easily skips the release)

```csharp
// ✅ Correct (connectionManager is the injected IDbConnectionManager)
using var conn = connectionManager.CreateConnection(DbCategoryIds.Common);
conn.Open();

// ❌ Forbidden
var conn = connectionManager.CreateConnection(DbCategoryIds.Common);
conn.Open();
// ... an exception here means Dispose never runs
conn.Dispose();
```

## Exception handling

- **Do not** catch base types such as `Exception` or `SystemException` (they hide unexpected errors)
- **No** empty catch blocks (they swallow the exception and the error disappears silently)
- To rethrow after a catch, use `throw;` (keeps the stack), not `throw ex;`

```csharp
// ✅ Correct: catch only the expected exception type
try { ... }
catch (SqlException ex) { ... }

// ✅ Correct: rethrow and keep the stack
catch (IOException ex)
{
    _logger.LogError(ex, "...");
    throw;
}

// ❌ Forbidden: too broad
catch (Exception) { }

// ❌ Forbidden: empty catch (swallows the exception)
catch { }
```

## XML security (XXE)

- When parsing XML, disable DTDs and external entities to prevent XML External Entity (XXE) attacks
- Set the safe options explicitly with `XmlReaderSettings`

```csharp
// ✅ Correct
var settings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Prohibit,
    XmlResolver = null
};
using var reader = XmlReader.Create(stream, settings);

// ❌ Forbidden: parsing untrusted XML with the default settings
var doc = new XmlDocument();
doc.Load(untrustedStream);
```

## Secure random numbers

- **Security uses** (tokens, IVs, encryption keys, verification codes) always use `RandomNumberGenerator`
- **Never** use `System.Random` for anything security-related (it is predictable)

```csharp
// ✅ Correct: secure random
var iv = RandomNumberGenerator.GetBytes(16);

// ❌ Forbidden for security use
var rng = new Random();
var iv = new byte[16];
rng.NextBytes(iv);
```

## Path security (path traversal)

- Before operating on a file path supplied by a user, verify that the path does not leave the allowed root
  directory
- Normalize with `Path.GetFullPath()`, then confirm the result is inside the expected directory

```csharp
// ✅ Correct
var fullPath = Path.GetFullPath(Path.Combine(allowedRoot, userInput));
if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
    throw new UnauthorizedAccessException("Path traversal detected.");

// ❌ Forbidden: using user input directly as a path
var content = File.ReadAllText(userInput);
```

## Leaking sensitive information

- API responses and exception messages **must not** contain stack traces, internal paths or system details
- Logs must not output complete SQL statements (they may contain parameter values)
- **Never output plaintext keys, tokens or passwords in logs or exception messages**
- The prohibitions in `security.md`, this repository's security rule, also apply
