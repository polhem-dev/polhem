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
[DisplayName("ToClrType maps FormSchema to the FormSchema type")]
public void ToClrType_FormSchema_ReturnsFormSchemaType()
{
    var result = DefineType.FormSchema.ToClrType();
    Assert.Equal(typeof(FormSchema), result);
}
```

### Parameterized: `[Theory]` + `[InlineData]`

```csharp
[Theory]
[InlineData(DefineType.SystemSettings, typeof(SystemSettings))]
[InlineData(DefineType.Language, typeof(LanguageResource))]
[DisplayName("ToClrType returns the matching CLR type")]
public void ToClrType_ValidType_ReturnsExpectedType(DefineType defineType, Type expectedType)
{
    var result = defineType.ToClrType();
    Assert.Equal(expectedType, result);
}
```

### Needs a database: `[DbFact(DatabaseType)]`

Write one test per `DatabaseType`. The connection ID is `common_{dbtype_lower}` (produced by
`TestDbConventions.GetDatabaseId`), and `DbAccess` comes from the fixture's `IDbAccessFactory` through
`NewDbAccess`; a test does not construct `DbAccess` itself:

```csharp
public class MyDbTests : IClassFixture<SharedDbFixture>
{
    private readonly SharedDbFixture _fx;
    public MyDbTests(SharedDbFixture fx) { _fx = fx; }

    [DbFact(DatabaseType.SQLServer)]
    [DisplayName("Execute returns a DataTable on SQL Server")]
    public void Execute_SqlServer_ReturnsDataTable()
    {
        var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLServer));
        var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, "SELECT sys_id FROM st_user"));
        Assert.NotNull(result.Table);
    }

    [DbFact(DatabaseType.PostgreSQL)]
    [DisplayName("Execute returns a DataTable on PostgreSQL")]
    public void Execute_PostgreSQL_ReturnsDataTable()
    {
        var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.PostgreSQL));
        var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, "SELECT sys_id FROM st_user"));
        Assert.NotNull(result.Table);
    }
}
```

### Needs a local service: `[LocalOnlyFact]` / `[LocalOnlyTheory]`

> **The following is an illustration, not existing code; do not grep for it** (`ApiConnectValidator` only has
> `ValidateAsync`). Neither attribute had a user when this was measured on 2026-08-11. They are kept because the situation "an integration test that needs a local service"
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
public static class SysInfoStaticCollection
{
    public const string Name = "SysInfoStatic";
}

// 2. Every test class that modifies that static references the constant.
[Collection(SysInfoStaticCollection.Name)]
public class SysInfoTests { ... }

[Collection(SysInfoStaticCollection.Name)]
public class SysInfoSecurityTests { ... }
```

The example is the real one in `tests/Polhem.Base.UnitTests`; the list of existing collections is in
`tests/CLAUDE.md`.

**Reference the `const`, never repeat the string**: a mistyped literal makes xUnit create an implicit group that
nobody shares. It looks serialized but is not, and there is no compile error; a mistyped constant does not compile.
