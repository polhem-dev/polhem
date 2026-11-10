using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// A form plugin acts for the company of the call it extends (ADR-063).
    /// </summary>
    public class FormBusinessPluginCompanyScopeTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public FormBusinessPluginCompanyScopeTests(PolhemTestFixture fx) { _fx = fx; }

        private static SessionCompanyScope Scope(string companyId)
            => new(companyId, string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);

        [Fact]
        [DisplayName("A plugin takes the company scope its context carries, not the session's current one")]
        public void CompanyScope_ContextCarriesSnapshot_UsesIt()
        {
            var token = TestSessionFactory.CreateAccessToken(_fx, companyId: "c-b");
            var ctx = new CompanyScopedBusinessObjectContext(TestBusinessObjectContext.Create(_fx), Scope("c-a"));

            var plugin = new ProbePlugin(ctx, token);

            Assert.Equal("c-a", plugin.Company);
        }

        [Fact]
        [DisplayName("A plugin whose context carries no snapshot reads the session once and keeps that read")]
        public void CompanyScope_NoSnapshot_ReadsTheSessionOnce()
        {
            var sessions = _fx.GetRequiredService<ISessionInfoService>();
            var token = TestSessionFactory.CreateAccessToken(_fx, companyId: "c-a");
            var plugin = new ProbePlugin(TestBusinessObjectContext.Create(_fx), token);

            Assert.Equal("c-a", plugin.Company);
            sessions.Get(token)!.CompanyScope = Scope("c-b");
            Assert.Equal("c-a", plugin.Company);
        }

        private sealed class ProbePlugin(IBusinessObjectContext ctx, Guid accessToken)
            : FormBusinessPlugin(ctx, accessToken, "Order")
        {
            public string? Company => CompanyScope.CompanyId;
        }
    }
}
