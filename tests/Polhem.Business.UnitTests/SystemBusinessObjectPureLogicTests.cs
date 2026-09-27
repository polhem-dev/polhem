using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Tests.Shared;
using Polhem.Base.Exceptions;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Pure logic branch tests of <see cref="SystemBusinessObject"/> that do not depend on the Repository or DefineAccess.
    /// The BO constructor needs an IPolhemContext, which <see cref="TestPolhemContext.Create(PolhemTestFixture)"/>
    /// builds from the DI services of the per-class fixture.
    /// </summary>
    public class SystemBusinessObjectPureLogicTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectPureLogicTests(SharedDbFixture fx) { _fx = fx; }
        [Fact]
        [DisplayName("Ping returns Status=ok, echoes the TraceId and returns a UTC ServerTime")]
        public void Ping_ReturnsExpectedValues()
        {
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var args = new PingArgs { ClientName = "client01", TraceId = "trace-xyz" };
            var before = DateTime.UtcNow.AddSeconds(-1);

            var result = bo.Ping(args);

            Assert.Equal("ok", result.Status);
            Assert.Equal("trace-xyz", result.TraceId);
            Assert.True(result.ServerTime >= before);
            Assert.True(result.ServerTime <= DateTime.UtcNow.AddSeconds(1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(86401)]
        [DisplayName("CreateSession throws ArgumentOutOfRangeException for an out-of-range ExpiresIn")]
        public void CreateSession_InvalidExpiresIn_ThrowsArgumentOutOfRange(int expiresIn)
        {
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new CreateSessionArgs { UserID = "u01", ExpiresIn = expiresIn, OneTime = false };

            Assert.Throws<ArgumentOutOfRangeException>(() => bo.CreateSession(args));
        }

        [Theory]
        [InlineData(DefineType.SystemSettings)]
        [InlineData(DefineType.DatabaseSettings)]
        // ProgramSettings became server-only when it turned into a type registry: it now holds only assembly-qualified type names,
        // and the menu the client needs moved to MenuSettings.
        [InlineData(DefineType.ProgramSettings)]
        [DisplayName("GetDefine throws UserMessageException for a non-local call with a server-only DefineType")]
        public void GetDefine_NonLocalCallWithSensitiveType_ThrowsNotSupported(DefineType defineType)
        {
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            var args = new GetDefineArgs { DefineType = defineType };

            Assert.Throws<UserMessageException>(() => bo.GetDefine(args));
        }

        [Theory]
        [InlineData(DefineType.SystemSettings)]
        [InlineData(DefineType.DatabaseSettings)]
        [InlineData(DefineType.PermissionModels)]
        [InlineData(DefineType.DbCategorySettings)]
        [InlineData(DefineType.FormSchema)]
        [InlineData(DefineType.FormLayout)]
        [InlineData(DefineType.ProgramSettings)]
        [InlineData(DefineType.Language)]
        [DisplayName("SaveDefine throws NotSupportedException for every non-local call (not only sensitive types)")]
        public void SaveDefine_NonLocalCall_ThrowsNotSupported(DefineType defineType)
        {
            // Previously only SystemSettings / DatabaseSettings were blocked, and any authenticated account could overwrite the other definition types,
            // including PermissionModels (the authorization model itself), DbCategorySettings (which database each table maps to)
            // and FormSchema (whose expressions are evaluated on the server). The whole method is now local-only.
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            var args = new SaveDefineArgs { DefineType = defineType, Xml = "<root/>" };

            Assert.Throws<NotSupportedException>(() => bo.SaveDefine(args));
        }
    }
}
