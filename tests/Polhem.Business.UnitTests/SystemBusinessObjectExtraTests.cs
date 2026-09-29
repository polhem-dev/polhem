using Polhem.Definition.Collections;
using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Core.Exceptions;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Additional tests for <see cref="SystemBusinessObject"/> beyond Login:
    /// the Ping / GetCommonConfiguration / GetDefine / SaveDefine branches
    /// and the basic ExecFunc path (local call).
    /// </summary>
    public class SystemBusinessObjectExtraTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectExtraTests(SharedDbFixture fx) { _fx = fx; }
        private static readonly string[] s_departmentKeys = { "Department" };

        [Fact]
        [DisplayName("Ping returns a PingResult with a TraceId and an OK status")]
        public void Ping_ReturnsOkResult()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var result = bo.Ping(new PingArgs { TraceId = "T-42", ClientName = "unit" });

            Assert.NotNull(result);
            Assert.Equal("ok", result.Status);
            Assert.Equal("T-42", result.TraceId);
            Assert.True(result.ServerTime <= DateTime.UtcNow);
        }

        [Fact]
        [DisplayName("GetCommonConfiguration returns non-empty XML")]
        public void GetCommonConfiguration_ReturnsXml()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var result = bo.GetCommonConfiguration(new GetCommonConfigurationArgs());

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.CommonConfiguration));
        }

        [Fact]
        [DisplayName("GetDefine(SystemSettings) throws UserMessageException for a non-local call")]
        public void GetDefine_SystemSettings_NonLocal_Throws()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            Assert.Throws<UserMessageException>(() =>
                bo.GetDefine(new GetDefineArgs { DefineType = DefineType.SystemSettings }));
        }

        [Fact]
        [DisplayName("GetDefine(DatabaseSettings) throws UserMessageException for a non-local call")]
        public void GetDefine_DatabaseSettings_NonLocal_Throws()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            Assert.Throws<UserMessageException>(() =>
                bo.GetDefine(new GetDefineArgs { DefineType = DefineType.DatabaseSettings }));
        }

        [Fact]
        [DisplayName("GetDefine(FormSchema) returns a result with XML for a local call")]
        public void GetDefine_FormSchema_ReturnsXml()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var result = bo.GetDefine(new GetDefineArgs
            {
                DefineType = DefineType.FormSchema,
                Keys = s_departmentKeys
            });

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        [Fact]
        [DisplayName("SaveDefine(SystemSettings) throws NotSupportedException for a non-local call")]
        public void SaveDefine_SystemSettings_NonLocal_Throws()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            Assert.Throws<NotSupportedException>(() =>
                bo.SaveDefine(new SaveDefineArgs { DefineType = DefineType.SystemSettings, Xml = "<x/>" }));
        }

        [Fact]
        [DisplayName("SaveDefine(DatabaseSettings) throws NotSupportedException for a non-local call")]
        public void SaveDefine_DatabaseSettings_NonLocal_Throws()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            Assert.Throws<NotSupportedException>(() =>
                bo.SaveDefine(new SaveDefineArgs { DefineType = DefineType.DatabaseSettings, Xml = "<x/>" }));
        }

        [Fact]
        [DisplayName("ExecFuncAnonymous(Hello) returns the Hello greeting")]
        public void ExecFuncAnonymous_Hello_ReturnsGreeting()
        {
            // `SystemExecFuncHandler.Hello` is marked `ApiAccessRequirement.Anonymous` and is called through `DoExecFuncAnonymous`.
            var bo = new TestableSystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, _ => (false, string.Empty));
            var args = new ExecFuncArgs("Hello");

            var result = bo.ExecFuncAnonymous(args);

            Assert.True(result.Parameters.Contains("Hello"));
            Assert.Contains("Hello system-level", result.Parameters.GetValue<string>("Hello"));
        }

        [Fact]
        [DisplayName("ExecFunc calling Hello as an authenticated caller returns the greeting (covers the DoExecFunc path)")]
        public void ExecFunc_Hello_AuthenticatedCall_ReturnsGreeting()
        {
            // Hello is marked Anonymous, which an authenticated caller may access, so this covers the `DoExecFunc` path.
            var bo = new TestableSystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, _ => (false, string.Empty));
            var args = new ExecFuncArgs("Hello");

            var result = bo.ExecFunc(args);

            Assert.True(result.Parameters.Contains("Hello"));
            Assert.Contains("Hello system-level", result.Parameters.GetValue<string>("Hello"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecFunc UpgradeTableSchema runs and includes the Upgraded status in the result")]
        public void ExecFunc_UpgradeTableSchema_ReturnsUpgradedStatus()
        {
            var bo = new TestableSystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, _ => (false, string.Empty));
            var args = new ExecFuncArgs("UpgradeTableSchema");
            args.Parameters.Add("DatabaseId", "common_sqlserver");
            args.Parameters.Add("CategoryId", "common");
            args.Parameters.Add("TableName", "st_user");

            var result = bo.ExecFunc(args);

            Assert.True(result.Parameters.Contains("Upgraded"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecFunc TestConnection with valid database settings does not throw")]
        public void ExecFunc_TestConnection_ValidDatabaseItem_Succeeds()
        {
            var bo = new TestableSystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, _ => (false, string.Empty));
            var args = new ExecFuncArgs("TestConnection");
            var dbItem = _fx.GetRequiredService<IDefineAccess>().GetDatabaseSettings().Items!["common_sqlserver"];
            args.Parameters.Add("DatabaseItem", dbItem);

            var exception = Record.Exception(() => bo.ExecFunc(args));

            Assert.Null(exception);
        }
    }
}
