using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Creating business objects for a given company scope: the factory extension overloads that take a
    /// <see cref="SessionCompanyScope"/>, and the context wrapper that carries the scope while delegating everything
    /// else to the context it wraps.
    /// </summary>
    public class CompanyScopedCreationTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public CompanyScopedCreationTests(PolhemTestFixture fx) { _fx = fx; }

        private static SessionCompanyScope Scope(string companyId)
            => new(companyId, string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);

        [Fact]
        [DisplayName("CreateFormBO with a company scope returns a form business object keeping the local-call flag")]
        public void CreateFormBO_WithCompanyScope_ReturnsFormBusinessObject()
        {
            var factory = _fx.GetRequiredService<IBusinessObjectFactory>();

            var bo = factory.CreateFormBO(TestSessionFactory.CreateAccessToken(_fx), "prog01", isLocalCall: true, Scope("co1"));

            Assert.True(Assert.IsType<FormBusinessObject>(bo).IsLocalCall);
        }

        [Fact]
        [DisplayName("CreateSystemBO with a company scope returns the system business object")]
        public void CreateSystemBO_WithCompanyScope_ReturnsSystemBusinessObject()
        {
            var factory = _fx.GetRequiredService<IBusinessObjectFactory>();

            var bo = factory.CreateSystemBO(TestSessionFactory.CreateAccessToken(_fx), isLocalCall: false, Scope("co1"));

            Assert.False(Assert.IsType<SystemBusinessObject>(bo).IsLocalCall);
        }

        [Fact]
        [DisplayName("The company-scope overloads refuse a null factory")]
        public void CompanyScopeOverloads_NullFactory_Throw()
        {
            IBusinessObjectFactory? factory = null;

            Assert.Throws<ArgumentNullException>(() => factory!.CreateFormBO(Guid.NewGuid(), "prog01", true, Scope("co1")));
            Assert.Throws<ArgumentNullException>(() => factory!.CreateSystemBO(Guid.NewGuid(), true, Scope("co1")));
        }

        [Fact]
        [DisplayName("The company-scoped context carries its scope and delegates every service to the wrapped context")]
        public void CompanyScopedContext_DelegatesToInner()
        {
            var inner = new BusinessObjectContext
            {
                DefineAccess = _fx.GetRequiredService<IDefineAccess>(),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = _fx.Provider,
            };
            var scope = Scope("co1");

            var scoped = new CompanyScopedBusinessObjectContext(inner, scope);

            Assert.Same(scope, scoped.CompanyScope);
            Assert.Same(inner.DefineAccess, scoped.DefineAccess);
            Assert.Same(inner.SessionInfoService, scoped.SessionInfoService);
            Assert.Same(inner.LanguageService, scoped.LanguageService);
            Assert.Same(inner.BoFactory, scoped.BoFactory);
            Assert.Same(inner.Services, scoped.Services);
        }

        [Fact]
        [DisplayName("The company-scoped context refuses a null context or a null scope")]
        public void CompanyScopedContext_NullArguments_Throw()
        {
            var inner = new BusinessObjectContext
            {
                DefineAccess = _fx.GetRequiredService<IDefineAccess>(),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = _fx.Provider,
            };

            Assert.Throws<ArgumentNullException>(() => new CompanyScopedBusinessObjectContext(null!, Scope("co1")));
            Assert.Throws<ArgumentNullException>(() => new CompanyScopedBusinessObjectContext(inner, null!));
        }
    }
}
