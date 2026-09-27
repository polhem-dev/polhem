# Test templates

Copy-ready templates for this repo's own test suites (`tests/Polhem.*.UnitTests`). **A maintainer document, not a
public document**: its readers are polhem maintainers, not framework users.

**The rules and criteria are always loaded from `.claude/rules/testing.md`** (which attribute and which fixture to use
in which situation, the env var naming rule, the parallel-safety requirements for global state). This file holds only
the shape of the code and **does not repeat the rules**.
When the two conflict, `rules/testing.md` wins.

---

## Basic shapes

### A single check: `[Fact]`

```csharp
[Fact]
[DisplayName("CreateSession returns a valid token")]
public void CreateSession_ReturnsValidToken()
{
    var token = _repo.CreateSession(user);
    Assert.NotNull(token);
}
```

### Parameterized: `[Theory]` + `[InlineData]`

```csharp
[Theory]
[InlineData(DefineType.SystemSettings, typeof(SystemSettings))]
[InlineData(DefineType.UserSettings, typeof(UserSettings))]
[DisplayName("ToClrType returns the matching CLR type")]
public void ToClrType_ValidType(DefineType defineType, Type expectedType)
{
    var result = defineType.ToClrType();
    Assert.Equal(expectedType, result);
}
```

### Needs a database: `[DbFact(DatabaseType)]`

Write one test per `DatabaseType`. The connection ID is `common_{dbtype_lower}` (produced by
`TestDbConventions.GetDatabaseId`):

```csharp
[DbFact(DatabaseType.SQLServer)]
[DisplayName("ExecuteDataTable query returns a valid DataTable on SQL Server")]
public void ExecuteDataTable_SqlServer_ReturnsDataTable()
{
    var dbAccess = new DbAccess("common_sqlserver");
    var result = dbAccess.Execute(command);
    Assert.NotNull(result.Table);
}

[DbFact(DatabaseType.PostgreSQL)]
[DisplayName("ExecuteDataTable query returns a valid DataTable on PostgreSQL")]
public void ExecuteDataTable_PostgreSQL_ReturnsDataTable()
{
    var dbAccess = new DbAccess("common_postgresql");
    var result = dbAccess.Execute(command);
    Assert.NotNull(result.Table);
}
```

### Needs a local service: `[LocalOnlyFact]` / `[LocalOnlyTheory]`

> **The following is an illustration, not existing code; do not grep for it.** Neither attribute has a user at
> present (measured 2026-08-11). They are kept because the situation "an integration test that needs a local service"
> still exists.

```csharp
[LocalOnlyTheory]
[InlineData("http://localhost/jsonrpc/api")]
[DisplayName("ApiConnectValidator returns the remote connect type for a URL")]
public void Validate_ValidUrl_ReturnsRemoteConnectType(string apiUrl) { ... }
```

---

## Per-class fixture

When you need DI-resolved backend services (`IDefineAccess` / `ISessionInfoService` / `IBusinessObjectFactory` and
so on):

```csharp
public class MyTests : IClassFixture<PolhemTestFixture>
{
    private readonly PolhemTestFixture _fx;
    public MyTests(PolhemTestFixture fx) { _fx = fx; }

    [Fact]
    public void Foo()
    {
        var access = _fx.GetRequiredService<IDefineAccess>();
        // ...
    }
}
```

Choosing the fixture (`PolhemTestFixture` / `UseTempDefinePath` / `SharedDbFixture`) is covered in
`.claude/rules/testing.md`. **The wrong fixture is the number-one cause of "green locally, red in CI"**, so that
criterion stays in the always-loaded section.

---

## Write isolation: the `SaveDefine` family must switch to temp

The rule is in `.claude/rules/testing.md` (`tests/Define/` is fixed data shared by several projects and must not be
written to). The shapes of the two approaches follow.

### Fixture level (recommended)

```csharp
public sealed class WritableDefineFixture : PolhemTestFixture
{
    public WritableDefineFixture() : base(b => b.UseTempDefinePath()) {}
}

public class MySaveTests : IClassFixture<WritableDefineFixture>
{
    private readonly WritableDefineFixture _fx;
    public MySaveTests(WritableDefineFixture fx) { _fx = fx; }

    [Fact]
    public void SaveDbCategorySettings_WritesFile()
    {
        var access = _fx.GetRequiredService<IDefineAccess>();
        access.SaveDbCategorySettings(new DbCategorySettings());
        Assert.True(File.Exists(_fx.PathOptions.GetDbCategorySettingsFilePath()));
    }
}
```

### Method-level inline temp dir

For pure data-writing tests (no DI), an inline temp dir is lighter than creating a fixture subclass:

```csharp
[Fact]
public void SaveSystemSettings_WritesFile()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-save-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        var paths = new PathOptions { DefinePath = tempDir };
        var access = new CacheDefineAccess(new FileDefineStorage(paths), paths);
        access.SaveSystemSettings(new SystemSettings());
        Assert.True(File.Exists(paths.GetSystemSettingsFilePath()));
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
```

---

## Serialization marker for global state

When you need to protect a process-wide static that has not been moved into DI yet (the criteria and the current list
are in `rules/testing.md`):

```csharp
// 1. Declare the collection at the root of the test project: a pure marker with no fixture, whose name is a const.
[CollectionDefinition(Name)]
public static class DbConnectionStateCollection
{
    public const string Name = "DbConnectionState";
}

// 2. Every test class that modifies that static references the constant.
[Collection(DbConnectionStateCollection.Name)]
public class DbConnectionManagerTests { ... }

[Collection(DbConnectionStateCollection.Name)]
public class DbAccessFactoryTests { ... }
```

**Reference the `const`, never repeat the string**: a mistyped literal makes xUnit create an implicit group that
nobody shares. It looks serialized but is not, and there is no compile error; a mistyped constant does not compile.
