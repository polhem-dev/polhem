using System.ComponentModel;
using Polhem.Base.Exceptions;
using Polhem.Business.AuditLog;
using Polhem.Business.System;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Integration tests for the key lifecycle (<see cref="SystemBusinessObject.ListApiKeys"/> /
    /// <see cref="SystemBusinessObject.SetApiKeyEnabled"/> /
    /// <see cref="SystemBusinessObject.SetApiKeyExpiry"/>): listing carries no credential material,
    /// disabling takes effect immediately, and a remote caller must be a deployment-level administrator.
    /// </summary>
    /// <remarks>
    /// Each test uses a unique <c>sys_id</c> and cleans up in finally, because the physical database is shared by several parallel test processes.
    /// These BOs use <c>DbScope.Common</c>, and the test fixture binds <c>common</c> to SQL Server,
    /// so the gate must be <c>SQLServer</c>. When it was marked <c>SQLite</c>, skipping depended on
    /// <c>POLHEM_TEST_CONNSTR_SQLITE</c> while the test actually ran against SQL Server.
    /// </remarks>
    public class SystemBusinessObjectApiKeyLifecycleTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectApiKeyLifecycleTests(SharedDbFixture fx) { _fx = fx; }

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        private SystemBusinessObject CreateBo()
            // The local call path; see the helper of the same name in `SystemBusinessObjectApiKeyTests`.
            => new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);

        private static string NewSysId() => "life-" + Guid.NewGuid().ToString("N");

        private string IssueKey(string sysId, DateTime? expiredAt = null)
        {
            CreateBo().CreateApiKey(new CreateApiKeyArgs
            {
                SysId = sysId,
                SysName = "Lifecycle app",
                Contact = "ops@example.com",
                ExpiredAt = expiredAt,
            });
            return sysId;
        }

        private ApiKeySummary? Find(string sysId)
            => CreateBo().ListApiKeys(new ListApiKeysArgs())
                .ApiKeys.FirstOrDefault(k => k.SysId == sysId);

        private void DeleteKey(string sysId)
        {
            var dbType = ConnectionManager.GetConnectionInfo(DbCategoryIds.Common).DatabaseType;
            string sql = $"DELETE FROM {dbType.QuoteIdentifier("st_api_key")} " +
                         $"WHERE {dbType.QuoteIdentifier("sys_id")} = {{0}}";
            new DbAccess(DbCategoryIds.Common, ConnectionManager)
                .Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, sysId));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ListApiKeys lists the issued keys without any credential material")]
        public void ListApiKeys_ReturnsSummaryWithoutCredentialMaterial()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                var summary = Find(sysId);

                Assert.NotNull(summary);
                Assert.Equal("Lifecycle app", summary!.SysName);
                Assert.Equal("ops@example.com", summary.Contact);
                Assert.True(summary.Enabled);
                Assert.NotNull(summary.IssuedAt);
                // The type has no hash property at all. This pins down the rule not to put `ApiKeyInfo` on the wire just for convenience.
                Assert.DoesNotContain("Hashed", typeof(ApiKeySummary).GetProperties().Select(p => p.Name));
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ListApiKeys includes disabled keys")]
        public void ListApiKeys_IncludesDisabledKeys()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                CreateBo().SetApiKeyEnabled(new SetApiKeyEnabledArgs { SysId = sysId, Enabled = false });

                // If a disabled key vanished from the list, its identifier would look unused, and reissuing the same identifier
                // is exactly what must not happen quietly.
                var summary = Find(sysId);
                Assert.NotNull(summary);
                Assert.False(summary!.Enabled);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetApiKeyEnabled revokes the key immediately on disable instead of waiting for the cache to expire")]
        public void SetApiKeyEnabled_Disable_RevokesImmediately()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                var repository = _fx.GetRequiredService<IRepositoryFactory>().Create<IApiKeyRepository>();
                Assert.NotNull(repository.GetEnabledById(sysId));

                CreateBo().SetApiKeyEnabled(new SetApiKeyEnabledArgs { SysId = sysId, Enabled = false });

                // A revocation that waits for the 60-minute absolute expiry of `ApiKeyCache` is not a revocation.
                Assert.Null(repository.GetEnabledById(sysId));
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetApiKeyEnabled makes the key usable again after re-enabling it")]
        public void SetApiKeyEnabled_Reenable_RestoresKey()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                var bo = CreateBo();
                bo.SetApiKeyEnabled(new SetApiKeyEnabledArgs { SysId = sysId, Enabled = false });
                bo.SetApiKeyEnabled(new SetApiKeyEnabledArgs { SysId = sysId, Enabled = true });

                Assert.NotNull(_fx.GetRequiredService<IRepositoryFactory>()
                    .Create<IApiKeyRepository>().GetEnabledById(sysId));
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetApiKeyExpiry writes the expiry and can clear it again")]
        public void SetApiKeyExpiry_SetsThenClears()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                var bo = CreateBo();
                var expiry = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                bo.SetApiKeyExpiry(new SetApiKeyExpiryArgs { SysId = sysId, ExpiredAt = expiry });
                Assert.Equal(expiry, Find(sysId)!.ExpiredAt);

                bo.SetApiKeyExpiry(new SetApiKeyExpiryArgs { SysId = sysId, ExpiredAt = null });
                Assert.Null(Find(sysId)!.ExpiredAt);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetApiKeyExpiry accepts a time in the past (a legitimate way to retire an existing key)")]
        public void SetApiKeyExpiry_PastExpiry_Accepted()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                var past = DateTime.UtcNow.AddMinutes(-1);

                // `CreateApiKey` rejects a past expiry, because issuing a key that is dead on arrival is a mistake.
                // Making an existing key expire from now on is a legitimate operation, so the two should not share one rule.
                var result = CreateBo().SetApiKeyExpiry(new SetApiKeyExpiryArgs { SysId = sysId, ExpiredAt = past });

                Assert.Equal(sysId, result.SysId);
                Assert.NotNull(Find(sysId)!.ExpiredAt);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetApiKeyEnabled rejects an unknown key with a readable message")]
        public void SetApiKeyEnabled_UnknownKey_ThrowsUserMessage()
        {
            var args = new SetApiKeyEnabledArgs { SysId = "no-such-key", Enabled = false };

            var ex = Assert.Throws<UserMessageException>(() => CreateBo().SetApiKeyEnabled(args));
            Assert.Contains("no-such-key", ex.Message, StringComparison.Ordinal);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetApiKeyExpiry rejects an unknown key with a readable message")]
        public void SetApiKeyExpiry_UnknownKey_ThrowsUserMessage()
        {
            var args = new SetApiKeyExpiryArgs { SysId = "no-such-key", ExpiredAt = null };

            Assert.Throws<UserMessageException>(() => CreateBo().SetApiKeyExpiry(args));
        }

        [Theory]
        [DisplayName("The list, enable and expiry management actions all reject a remote caller who is not a deployment-level administrator")]
        [InlineData("list")]
        [InlineData("enable")]
        [InlineData("expiry")]
        public void ManagementActions_RemoteNonAdmin_ThrowUnauthorized(string action)
        {
            var ctx = TestPolhemContext.CreateWithOverrides(_fx,
                (typeof(IDeploymentAuthorizationService), new DenyingDeploymentAuthorization()));
            var bo = new SystemBusinessObject(ctx, Guid.NewGuid(), SysProgIds.System, isLocalCall: false);

            Assert.Throws<UnauthorizedAccessException>(() => Invoke(bo, action));
        }

        private static void Invoke(SystemBusinessObject bo, string action)
        {
            switch (action)
            {
                case "list":
                    bo.ListApiKeys(new ListApiKeysArgs());
                    break;
                case "enable":
                    bo.SetApiKeyEnabled(new SetApiKeyEnabledArgs { SysId = "any", Enabled = false });
                    break;
                default:
                    bo.SetApiKeyExpiry(new SetApiKeyExpiryArgs { SysId = "any", ExpiredAt = null });
                    break;
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Disabling and setting the expiry both leave a deployment-level audit entry with before and after values")]
        public void LifecycleActions_WriteDeploymentAudit()
        {
            string sysId = IssueKey(NewSysId());
            try
            {
                var writer = new CapturingAuditLogWriter();
                var ctx = TestPolhemContext.CreateWithOverrides(_fx,
                    (typeof(AuditLogOptions), new AuditLogOptions { Enabled = true }),
                    (typeof(IAuditLogWriter), writer));
                var bo = new SystemBusinessObject(ctx, Guid.Empty, SysProgIds.System, isLocalCall: true);

                bo.SetApiKeyEnabled(new SetApiKeyEnabledArgs { SysId = sysId, Enabled = false });

                var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
                Assert.Equal("st_api_key", entry.ChangeTableName);
                Assert.Equal(sysId, entry.RowKey);
                Assert.Equal("System.SetApiKeyEnabled", entry.Source);
                Assert.True(entry.IsSensitive);

                var field = Assert.Single(ChangeDiffGramReader.Read(entry.ChangesXml));
                Assert.Equal("enabled", field.FieldName);
                Assert.Equal("True", field.OldValue);
                Assert.Equal("False", field.NewValue);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        private sealed class DenyingDeploymentAuthorization : IDeploymentAuthorizationService
        {
            public bool Can(Guid accessToken, DeploymentAction action) => false;
        }

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public void Write(AuditEntry entry) => Entries.Add(entry);
        }
    }
}
