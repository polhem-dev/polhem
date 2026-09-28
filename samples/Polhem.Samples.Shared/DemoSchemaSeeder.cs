using System.Globalization;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Storage;

namespace Polhem.Samples.Shared;

/// <summary>
/// Process-once helper that creates every table <c>Define/DbCategorySettings.xml</c> registers and
/// seeds the rows the demos need: the demo account, the demo company and its access grant, and a
/// few staff and team rows so the list views are not empty on first run. Idempotent: a
/// second invocation is a no-op once schema + rows are in place.
/// </summary>
/// <remarks>
/// Reads schema definitions through <see cref="IDefineAccess"/> (which the host has already wired
/// through <c>AddPolhemFramework</c>) and writes through <see cref="IDbAccessFactory"/>. SQLite is
/// the only target — adding other databases would need engine-specific UUID literals.
/// </remarks>
public static class DemoSchemaSeeder
{
    private const string CommonDatabaseId = "common";
    private const string StaffTable = "ft_staff";
    private const string TeamTable = "ft_team";
    private const string UserTable = "st_user";
    private const string CompanyTable = "st_company";
    private const string UserCompanyTable = "st_user_company";

    public static void EnsureSchemaAndSeed(IDefineAccess defineAccess, IDbConnectionManager connectionManager, IDbAccessFactory dbAccessFactory)
    {
        ArgumentNullException.ThrowIfNull(defineAccess);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(dbAccessFactory);

        EnsureSchema(defineAccess, connectionManager);

        var common = dbAccessFactory.Create(CommonDatabaseId);
        SeedDemoUser(common);
        SeedDemoCompany(common);
        SeedCompanyAccess(common);

        // Business data belongs to the company, so it lands in the demo company's database.
        var company = dbAccessFactory.Create(DemoCredentials.CompanyDatabaseId);
        SeedStaff(company);
        SeedTeams(company);
    }

    /// <summary>
    /// Builds every table registered in <c>DbCategorySettings</c>, so adding a table to the demo is
    /// a TableSchema file plus a <c>TableItem</c> entry and no edit here. Each category id names
    /// both the target database and the <c>TableSchema/&lt;id&gt;/</c> folder.
    /// </summary>
    private static void EnsureSchema(IDefineAccess defineAccess, IDbConnectionManager connectionManager)
    {
        var settings = defineAccess.GetDbCategorySettings();
        if (settings.Categories == null) { return; }

        foreach (var category in settings.Categories)
        {
            if (category.Tables == null) { continue; }
            var builder = new TableSchemaBuilder(category.Id, defineAccess, connectionManager);
            foreach (var table in category.Tables)
                builder.Execute(category.Id, table.TableName);
        }
    }

    private static void SeedStaff(DbAccess dbAccess)
    {
        if (CountRows(dbAccess, StaffTable) > 0) return;

        InsertStaff(dbAccess, "S001", "Alice Chen",   new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), isActive: true);
        InsertStaff(dbAccess, "S002", "Bob Liu",      new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), isActive: true);
        InsertStaff(dbAccess, "S003", "Carol Wang",   new DateTime(2023, 7, 20, 0, 0, 0, DateTimeKind.Utc), isActive: false);
    }

    private static void SeedTeams(DbAccess dbAccess)
    {
        if (CountRows(dbAccess, TeamTable) > 0) return;

        InsertTeam(dbAccess, "T001", "Engineering");
        InsertTeam(dbAccess, "T002", "Sales");
    }

    /// <summary>
    /// Seeds the row <c>Login</c> reads the signing-in user's locale from.
    /// </summary>
    /// <remarks>
    /// Credentials are deliberately not seeded: <see cref="DemoAuthenticatingSystemBusinessObject"/>
    /// authenticates against <see cref="DemoCredentials"/> and never reads this row's password,
    /// which stays blank — and a blank stored hash is rejected outright by
    /// <c>UserRepository.VerifyPassword</c>, so this row cannot be signed in to on its own.
    /// Time zone and culture stay blank too, which is what makes the session fall back to the
    /// deployment-wide defaults in <c>BackendConfiguration</c>.
    /// </remarks>
    private static void SeedDemoUser(DbAccess dbAccess)
    {
        if (CountBySysId(dbAccess, UserTable, DemoCredentials.UserId) > 0) return;

        var spec = new DbCommandSpec(
            DbCommandKind.NonQuery,
            $"INSERT INTO {UserTable} (sys_rowid, sys_id, sys_name, password, time_zone, culture, sys_insert_time) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            Guid.NewGuid(), DemoCredentials.UserId, DemoCredentials.DisplayName,
            string.Empty, string.Empty, string.Empty, DateTime.UtcNow);
        dbAccess.Execute(spec);
    }

    /// <summary>
    /// Seeds the demo company into <c>st_company</c>, which <c>EnterCompany</c> reads to find the
    /// company's database.
    /// </summary>
    /// <remarks>
    /// The XML columns get explicit empty strings rather than being left out. They are
    /// <c>DbType="Text"</c>, and MySQL does not allow a DEFAULT on TEXT, so the framework emits no
    /// default for them and an INSERT that omits them fails on that provider.
    /// </remarks>
    private static void SeedDemoCompany(DbAccess dbAccess)
    {
        if (CountBySysId(dbAccess, CompanyTable, DemoCredentials.CompanyId) > 0) return;

        var spec = new DbCommandSpec(
            DbCommandKind.NonQuery,
            $"INSERT INTO {CompanyTable} (sys_rowid, sys_id, sys_name, company_database_id, customize_id, " +
            "number_formats_xml, default_currency, cash_rounding_xml, allowed_currencies_xml, enabled, sys_insert_time) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            Guid.NewGuid(), DemoCredentials.CompanyId, DemoCredentials.CompanyName,
            DemoCredentials.CompanyDatabaseId, string.Empty, string.Empty, DemoCredentials.DefaultCurrency,
            string.Empty, string.Empty, true, DateTime.UtcNow);
        dbAccess.Execute(spec);
    }

    /// <summary>
    /// Grants the demo account access to the demo company through the <c>st_user_company</c> row
    /// <c>EnterCompany</c> checks.
    /// </summary>
    /// <remarks>
    /// A missing grant aborts startup rather than being skipped: without it sign-in still succeeds
    /// and every form then fails with "Company access denied", a long way from the cause.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The user or company row could not be resolved.</exception>
    private static void SeedCompanyAccess(DbAccess dbAccess)
    {
        var userRowId = ResolveRowId(dbAccess, UserTable, DemoCredentials.UserId);
        var companyRowId = ResolveRowId(dbAccess, CompanyTable, DemoCredentials.CompanyId);
        if (userRowId == Guid.Empty || companyRowId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Demo startup aborted: {UserTable} '{DemoCredentials.UserId}' and {CompanyTable} " +
                $"'{DemoCredentials.CompanyId}' must both exist before the company grant is seeded. " +
                "Delete quickstart.db to reseed from scratch.");
        }

        var countSpec = new DbCommandSpec(
            DbCommandKind.Scalar,
            $"SELECT COUNT(*) FROM {UserCompanyTable} WHERE user_rowid = {{0}} AND company_rowid = {{1}}",
            userRowId, companyRowId);
        if (Convert.ToInt32(dbAccess.Execute(countSpec).Scalar, CultureInfo.InvariantCulture) > 0) return;

        dbAccess.Execute(new DbCommandSpec(
            DbCommandKind.NonQuery,
            $"INSERT INTO {UserCompanyTable} (sys_rowid, user_rowid, company_rowid) VALUES ({{0}}, {{1}}, {{2}})",
            Guid.NewGuid(), userRowId, companyRowId));
    }

    private static int CountRows(DbAccess dbAccess, string table)
    {
        var spec = new DbCommandSpec(DbCommandKind.Scalar, $"SELECT COUNT(*) FROM {table}");
        return Convert.ToInt32(dbAccess.Execute(spec).Scalar, CultureInfo.InvariantCulture);
    }

    private static int CountBySysId(DbAccess dbAccess, string table, string sysId)
    {
        var spec = new DbCommandSpec(DbCommandKind.Scalar, $"SELECT COUNT(*) FROM {table} WHERE sys_id = {{0}}", sysId);
        return Convert.ToInt32(dbAccess.Execute(spec).Scalar, CultureInfo.InvariantCulture);
    }

    private static Guid ResolveRowId(DbAccess dbAccess, string table, string sysId)
    {
        var spec = new DbCommandSpec(DbCommandKind.Scalar, $"SELECT sys_rowid FROM {table} WHERE sys_id = {{0}}", sysId);
        var value = dbAccess.Execute(spec).Scalar;
        return value switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var parsed) => parsed,
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            _ => Guid.Empty,
        };
    }

    private static void InsertTeam(DbAccess dbAccess, string sysId, string name)
    {
        var spec = new DbCommandSpec(
            DbCommandKind.NonQuery,
            $"INSERT INTO {TeamTable} (sys_rowid, sys_id, sys_name) VALUES ({{0}}, {{1}}, {{2}})",
            Guid.NewGuid(), sysId, name);
        dbAccess.Execute(spec);
    }

    private static void InsertStaff(DbAccess dbAccess, string sysId, string name, DateTime hireDate, bool isActive)
    {
        var spec = new DbCommandSpec(
            DbCommandKind.NonQuery,
            $"INSERT INTO {StaffTable} (sys_rowid, sys_id, sys_name, hire_date, is_active) " +
            "VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), sysId, name, hireDate, isActive ? 1 : 0);
        dbAccess.Execute(spec);
    }
}
