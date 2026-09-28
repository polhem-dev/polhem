using System.ComponentModel;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// <see cref="BusinessObjectFactory"/> builds each business object with the progId's declared spelling, whatever
    /// casing the caller used.
    /// </summary>
    /// <remarks>
    /// Resolution finds a business object case-insensitively, but the audit-policy exemption compares the progId
    /// exactly. Before the factory canonicalized it, a JSON-RPC call to <c>auditrule.Save</c> reached the audit-rule
    /// form while skipping its always-audited exemption. A progId the registry does not list is not canonicalized
    /// here; that a form reached that way still gets its audit rule is covered by
    /// <c>FormBusinessObjectAuditRuleTests.Save_UnregisteredFormInOtherCasing_StillGetsItsAuditRule</c>.
    /// </remarks>
    public class BusinessObjectFactoryProgIdCasingTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public BusinessObjectFactoryProgIdCasingTests(PolhemTestFixture fx) { _fx = fx; }

        [Theory]
        [InlineData("auditrule", SysProgIds.AuditRule)]
        [InlineData("AUDITRULE", SysProgIds.AuditRule)]
        [InlineData("system", SysProgIds.System)]
        [DisplayName("A reserved progId requested in another casing reaches the business object in its declared casing")]
        public void CreateBusinessObject_ReservedProgIdInOtherCase_UsesDeclaredCasing(string requested, string expected)
        {
            var bo = (BusinessObject)CreateFactory(new FakeDefineAccess()).CreateBusinessObject(Guid.Empty, requested, isLocalCall: false);

            Assert.Equal(expected, bo.ProgId);
        }

        [Fact]
        [DisplayName("A registered progId requested in another casing reaches the business object in the registry's casing")]
        public void CreateBusinessObject_RegisteredProgIdInOtherCase_UsesRegistryCasing()
        {
            var defineAccess = new FakeDefineAccess();
            defineAccess.ProgramSettings.Items!.Add(new ProgramItem("Employee", "Employee"));

            var bo = (BusinessObject)CreateFactory(defineAccess).CreateBusinessObject(Guid.Empty, "EMPLOYEE", isLocalCall: false);

            Assert.Equal("Employee", bo.ProgId);
        }

        private BusinessObjectFactory CreateFactory(FakeDefineAccess defineAccess)
            => new(
                _fx.Provider,
                defineAccess,
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                new ProgramSettingsBoTypeResolver(defineAccess));
    }
}
