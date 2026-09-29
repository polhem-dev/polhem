using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Business.AuditLog;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Microsoft.Extensions.Logging;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Audit trail of deployment-level operations (<see cref="SystemBusinessObject.SetDeploymentAdmin"/> /
    /// <see cref="SystemBusinessObject.CreateApiKey"/>): written to the change axis, marked sensitive,
    /// carrying before and after values, and never logging the key's secret segment or hash.
    /// </summary>
    /// <remarks>
    /// These BOs use <c>DbScope.Common</c>, and the test fixture binds <c>common</c> to SQL Server,
    /// so the gate must be <c>SQLServer</c>. When it was marked <c>SQLite</c>, skipping depended on
    /// <c>POLHEM_TEST_CONNSTR_SQLITE</c> while the test actually ran against SQL Server.
    /// </remarks>
    public class SystemBusinessObjectDeploymentAuditTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectDeploymentAuditTests(SharedDbFixture fx) { _fx = fx; }

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        /// <summary>
        /// Creates a BO whose audit writer is replaced by a capturing fake.
        /// </summary>
        /// <param name="writer">The captured audit entries.</param>
        /// <param name="enabled">The global audit switch.</param>
        /// <param name="changeEnabled">The data change category switch. Deployment-level operations deliberately ignore it.</param>
        private SystemBusinessObject CreateBo(out CapturingAuditLogWriter writer,
            bool enabled = true, bool changeEnabled = true)
        {
            writer = new CapturingAuditLogWriter();
            return CreateBo(writer, loggers: null, enabled, changeEnabled);
        }

        /// <summary>
        /// Creates a BO with the audit writer and logger supplied by the caller.
        /// </summary>
        private SystemBusinessObject CreateBo(IAuditLogWriter writer, ILoggerFactory? loggers,
            bool enabled = true, bool changeEnabled = true)
        {
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx,
                (typeof(AuditLogOptions), new AuditLogOptions { Enabled = enabled, ChangeEnabled = changeEnabled }),
                (typeof(IAuditLogWriter), writer),
                (typeof(ILoggerFactory), loggers));
            return new SystemBusinessObject(ctx, Guid.Empty, SysProgIds.System, isLocalCall: true);
        }

        private static ChangeAuditEntry SingleChange(CapturingAuditLogWriter writer)
        {
            var entry = Assert.Single(writer.Entries);
            return Assert.IsType<ChangeAuditEntry>(entry);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetDeploymentAdmin leaves an audit entry marked sensitive that shows the false to true direction")]
        public void SetDeploymentAdmin_Grant_WritesSensitiveAuditWithDirection()
        {
            string userId = TestUsers.Create(ConnectionManager, "audit-grant");
            try
            {
                var bo = CreateBo(out var writer);

                bo.SetDeploymentAdmin(new SetDeploymentAdminArgs { UserId = userId, IsDeploymentAdmin = true });

                var entry = SingleChange(writer);
                Assert.Equal(SysProgIds.System, entry.ProgId);
                Assert.Equal("st_user", entry.ChangeTableName);
                Assert.Equal(ChangeKind.Update, entry.ChangeKind);
                // Privilege changes are always sensitive, so filtering out noise must not filter them out too.
                Assert.True(entry.IsSensitive);
                Assert.Equal("System.SetDeploymentAdmin", entry.Source);

                // Granting and revoking are both Update, so without before and after values the direction is lost.
                var field = Assert.Single(ChangeDiffGramReader.Read(entry.ChangesXml));
                Assert.Equal(ProtectedFields.DeploymentAdmin, field.FieldName);
                Assert.Equal("False", field.OldValue);
                Assert.Equal("True", field.NewValue);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetDeploymentAdmin on revoke leaves an audit entry that shows the true to false direction")]
        public void SetDeploymentAdmin_Revoke_WritesOppositeDirection()
        {
            string userId = TestUsers.Create(ConnectionManager, "audit-revoke");
            try
            {
                var granting = CreateBo(out _);
                granting.SetDeploymentAdmin(new SetDeploymentAdminArgs { UserId = userId, IsDeploymentAdmin = true });

                var bo = CreateBo(out var writer);
                bo.SetDeploymentAdmin(new SetDeploymentAdminArgs { UserId = userId, IsDeploymentAdmin = false });

                var field = Assert.Single(ChangeDiffGramReader.Read(SingleChange(writer).ChangesXml));
                Assert.Equal("True", field.OldValue);
                Assert.Equal("False", field.NewValue);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey leaves an audit entry, and neither the plaintext secret nor the hash is logged")]
        public void CreateApiKey_WritesAuditWithoutSecretOrHash()
        {
            string sysId = "audit-" + Guid.NewGuid().ToString("N");
            try
            {
                var bo = CreateBo(out var writer);

                var result = bo.CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = "Audited app" });

                var entry = SingleChange(writer);
                Assert.Equal(SysProgIds.System, entry.ProgId);
                Assert.Equal("st_api_key", entry.ChangeTableName);
                Assert.Equal(ChangeKind.Insert, entry.ChangeKind);
                Assert.Equal(sysId, entry.RowKey);
                Assert.Equal("System.CreateApiKey", entry.Source);

                var fields = ChangeDiffGramReader.Read(entry.ChangesXml);
                Assert.Contains(fields, f => f.FieldName == SysFields.Id && f.NewValue == sysId);
                Assert.Contains(fields, f => f.FieldName == SysFields.Name && f.NewValue == "Audited app");

                // Readers of the audit rows are not the readers of `st_api_key`, so neither the secret segment nor its hash belongs here.
                Assert.True(ApiKeyFormat.TryParse(result.ApiKey, out _, out string secret));
                Assert.DoesNotContain(secret, entry.ChangesXml, StringComparison.Ordinal);
                Assert.DoesNotContain("hashed_key", entry.ChangesXml, StringComparison.Ordinal);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Disabling data change auditing does not silence deployment-level operations")]
        public void SetDeploymentAdmin_ChangeAuditDisabled_StillWrites()
        {
            string userId = TestUsers.Create(ConnectionManager, "audit-chgoff");
            try
            {
                // `ChangeEnabled` exists for business data history that grows too large. Turning it off must not silence privilege changes.
                var bo = CreateBo(out var writer, enabled: true, changeEnabled: false);

                bo.SetDeploymentAdmin(new SetDeploymentAdminArgs { UserId = userId, IsDeploymentAdmin = true });

                Assert.Single(writer.Entries);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Deployment-level operations leave no trail when global auditing is disabled")]
        public void SetDeploymentAdmin_AuditDisabled_WritesNothing()
        {
            string userId = TestUsers.Create(ConnectionManager, "audit-off");
            try
            {
                var bo = CreateBo(out var writer, enabled: false);

                bo.SetDeploymentAdmin(new SetDeploymentAdminArgs { UserId = userId, IsDeploymentAdmin = true });

                Assert.Empty(writer.Entries);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey succeeds when the application name contains a control character XML forbids, and the audit reads back the original value")]
        public void CreateApiKey_NameWithControlCharacter_WritesReadableAudit()
        {
            string sysId = "audit-" + Guid.NewGuid().ToString("N");
            const string name = "Pasted\u0001app";
            try
            {
                var bo = CreateBo(out var writer);

                var result = bo.CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = name });

                Assert.False(string.IsNullOrEmpty(result.ApiKey));
                var fields = ChangeDiffGramReader.Read(SingleChange(writer).ChangesXml);
                Assert.Contains(fields, f => f.FieldName == SysFields.Name && f.NewValue == name);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey still returns the key when the audit write fails (the key is already stored, and without the secret it is useless) and logs an error")]
        public void CreateApiKey_AuditWriteFails_StillReturnsKeyAndLogsError()
        {
            string sysId = "audit-" + Guid.NewGuid().ToString("N");
            var loggers = new RecordingLoggerFactory();
            try
            {
                var bo = CreateBo(new ThrowingAuditLogWriter(), loggers);

                var result = bo.CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = "Audit sink down" });

                Assert.True(ApiKeyFormat.TryParse(result.ApiKey, out string parsedId, out _));
                Assert.Equal(sysId, parsedId);
                var error = Assert.Single(loggers.Entries, e => e.Level == LogLevel.Error);
                Assert.IsType<InvalidOperationException>(error.Exception);
                Assert.Contains(sysId, error.Message, StringComparison.Ordinal);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetDeploymentAdmin still applies the change when the audit write fails and logs an error")]
        public void SetDeploymentAdmin_AuditWriteFails_StillAppliesAndLogsError()
        {
            string userId = TestUsers.Create(ConnectionManager, "audit-sinkdown");
            var loggers = new RecordingLoggerFactory();
            try
            {
                var bo = CreateBo(new ThrowingAuditLogWriter(), loggers);

                var result = bo.SetDeploymentAdmin(new SetDeploymentAdminArgs { UserId = userId, IsDeploymentAdmin = true });

                Assert.True(result.IsDeploymentAdmin);
                var error = Assert.Single(loggers.Entries, e => e.Level == LogLevel.Error);
                Assert.IsType<InvalidOperationException>(error.Exception);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        private void DeleteKey(string sysId)
        {
            var dbType = ConnectionManager.GetConnectionInfo(DbCategoryIds.Common).DatabaseType;
            string sql = $"DELETE FROM {dbType.QuoteIdentifier("st_api_key")} " +
                         $"WHERE {dbType.QuoteIdentifier("sys_id")} = {{0}}";
            new DbAccess(DbCategoryIds.Common, ConnectionManager)
                .Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, sysId));
        }

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        private sealed class ThrowingAuditLogWriter : IAuditLogWriter
        {
            public void Write(AuditEntry entry) => throw new InvalidOperationException("Audit sink unavailable.");
        }
    }
}
