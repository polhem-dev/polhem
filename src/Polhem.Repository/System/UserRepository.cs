using System.Security.Cryptography;
using Polhem.Base;
using Polhem.Base.Security;
using Polhem.Db;
using Polhem.Definition;
using Polhem.Repository.Abstractions.System;

namespace Polhem.Repository.System
{
    /// <summary>
    /// Reads the common <c>st_user</c> table. Resolves a user's <c>sys_rowid</c> from its
    /// <c>sys_id</c> so company-scoped lookups (e.g. the employee link) can be keyed by row id.
    /// </summary>
    internal sealed class UserRepository : RepositoryBase, IUserRepository
    {
        private const string TableName = "st_user";
        private const string SysIdColumn = "sys_id";

        /// <summary>
        /// Initializes a new <see cref="UserRepository"/>.
        /// </summary>
        /// <param name="ctx">The shared repository context.</param>
        /// <param name="accessToken">The current request's access token.</param>
        /// <param name="progId">Unused on the framework axis; accepted for signature uniformity.</param>
        public UserRepository(IRepositoryContext ctx, Guid accessToken, string progId)
            : base(ctx, accessToken, progId, DbScope.Common)
        {
        }

        /// <inheritdoc/>
        public Guid GetRowIdBySysId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) { return Guid.Empty; }

            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colRowId = dbType.QuoteIdentifier("sys_rowid");
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"SELECT {colRowId} FROM {tbl} WHERE {colId} = {{0}}";
            var dbAccess = CreateDbAccess();
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, userId));
            // Scalar is null when the user id matches no row → no user, empty row id.
            return result.Scalar == null ? Guid.Empty : ValueUtilities.CGuid(result.Scalar);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// An unknown account, and an account with a blank stored hash, are still run through a full
        /// PBKDF2 verification against <see cref="s_timingDecoyHash"/>, so their response time matches a
        /// wrong password's. The results of these paths are covered by <c>UserRepositoryPasswordTests</c>;
        /// the timing itself is not asserted. On success, a hash stored with weaker parameters than
        /// <see cref="PasswordHasher.HashPassword"/> uses today is replaced by a fresh one.
        /// </remarks>
        public bool VerifyPassword(string userId, string password)
        {
            if (string.IsNullOrWhiteSpace(userId)) { return false; }
            password ??= string.Empty;

            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colPassword = dbType.QuoteIdentifier(ProtectedFields.Password);
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"SELECT {colPassword} FROM {tbl} WHERE {colId} = {{0}}";
            var dbAccess = CreateDbAccess();
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, userId));
            string? hash = result.Scalar == null || result.Scalar == DBNull.Value
                ? null
                : ValueUtilities.CStr(result.Scalar);

            // A blank stored hash is an account with no password set, not an account that accepts any
            // password. Whitespace counts as blank: Oracle cannot store '' in a NOT NULL column, so a
            // single space is how such a row is written there.
            if (string.IsNullOrWhiteSpace(hash))
            {
                PasswordHasher.VerifyPassword(password, s_timingDecoyHash.Value);
                return false;
            }

            if (!PasswordHasher.VerifyPassword(password, hash)) { return false; }

            if (PasswordHasher.NeedsRehash(hash))
            {
                UpgradePasswordHash(userId, hash, PasswordHasher.HashPassword(password));
            }
            return true;
        }

        /// <summary>
        /// A hash of a random password nobody knows, verified in place of a missing one so every
        /// sign-in attempt pays the same key-derivation cost.
        /// </summary>
        /// <remarks>
        /// Built lazily with the current parameters, so its cost tracks <see cref="PasswordHasher.Iterations"/>
        /// without a second constant to keep in step.
        /// </remarks>
        private static readonly Lazy<string> s_timingDecoyHash =
            new(() => PasswordHasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

        /// <summary>
        /// Replaces a stored hash that was just verified with one made from the current parameters.
        /// </summary>
        /// <param name="userId">The account whose hash is replaced.</param>
        /// <param name="verifiedHash">The stored value the password was verified against.</param>
        /// <param name="newHash">The replacement.</param>
        /// <remarks>
        /// The update is conditional on the stored value still being <paramref name="verifiedHash"/>, so a
        /// password change that lands between the read and this write is never overwritten with the
        /// old password.
        /// </remarks>
        private void UpgradePasswordHash(string userId, string verifiedHash, string newHash)
        {
            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colPassword = dbType.QuoteIdentifier(ProtectedFields.Password);
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"UPDATE {tbl} SET {colPassword} = {{0}} WHERE {colId} = {{1}} AND {colPassword} = {{2}}";
            CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, newHash, userId, verifiedHash));
        }

        /// <inheritdoc/>
        public UserLocale GetLocale(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) { return UserLocale.Empty; }

            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colTimeZone = dbType.QuoteIdentifier("time_zone");
            string colCulture = dbType.QuoteIdentifier("culture");
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"SELECT {colTimeZone}, {colCulture} FROM {tbl} WHERE {colId} = {{0}}";
            var dbAccess = CreateDbAccess();
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, userId));
            var table = result.Table;
            if (table == null || table.Rows.Count == 0) { return UserLocale.Empty; }

            // Null covers both "column never populated" and, on Oracle, a stored empty string.
            // Both mean the same thing to the caller: no preference, use the deployment default.
            var row = table.Rows[0];
            return new UserLocale(ReadText(row[0]), ReadText(row[1]));

            static string ReadText(object? value)
                => value == null || value == DBNull.Value ? string.Empty : ValueUtilities.CStr(value).Trim();
        }

        /// <inheritdoc/>
        public string? GetName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) { return null; }

            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colName = dbType.QuoteIdentifier("sys_name");
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"SELECT {colName} FROM {tbl} WHERE {colId} = {{0}}";
            var dbAccess = CreateDbAccess();
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, userId));
            var table = result.Table;
            // No row means no such user. A row carrying a null name is a user with a blank name,
            // which is a different answer and must not collapse into the same one.
            if (table == null || table.Rows.Count == 0) { return null; }

            var value = table.Rows[0][0];
            return value == null || value == DBNull.Value ? string.Empty : ValueUtilities.CStr(value).Trim();
        }

        /// <inheritdoc/>
        public bool IsDeploymentAdmin(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) { return false; }

            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colFlag = dbType.QuoteIdentifier("deployment_admin");
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"SELECT {colFlag} FROM {tbl} WHERE {colId} = {{0}}";
            var dbAccess = CreateDbAccess();
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, userId));
            // Null covers "no such user" and a column not yet populated by an older row. Both deny.
            return result.Scalar != null && result.Scalar != DBNull.Value && ValueUtilities.CBool(result.Scalar);
        }

        /// <inheritdoc/>
        public bool SetDeploymentAdmin(string userId, bool isDeploymentAdmin)
        {
            if (string.IsNullOrWhiteSpace(userId)) { return false; }

            var dbType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            string tbl = dbType.QuoteIdentifier(TableName);
            string colFlag = dbType.QuoteIdentifier("deployment_admin");
            string colId = dbType.QuoteIdentifier(SysIdColumn);

            string sql = $"UPDATE {tbl} SET {colFlag} = {{0}} WHERE {colId} = {{1}}";
            var dbAccess = CreateDbAccess();
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, sql, isDeploymentAdmin, userId);
            return dbAccess.Execute(spec).RowsAffected > 0;
        }
    }
}
