using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// One call of a business object authorizes, scopes and reaches its data for the same company, even when the
    /// session enters another company in the middle of the call.
    /// </summary>
    /// <remarks>
    /// A concurrent <c>EnterCompany</c> on the same token replaces the session's company scope at any moment. These
    /// tests do it from inside the authorization check, the moment after the call has been authorized for company A,
    /// and then look at what the record scope and the repository were handed (ADR-063).
    /// </remarks>
    public class FormBusinessObjectCompanyScopeSnapshotTests : IClassFixture<PolhemTestFixture>
    {
        // FormSchema 'PermGateForm' declares a PermissionModelId, so the call goes through both permission layers.
        private const string GatedProgId = "PermGateForm";
        private const string CompanyA = "c-a";
        private const string CompanyB = "c-b";

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectCompanyScopeSnapshotTests(PolhemTestFixture fx) { _fx = fx; }

        private static SessionCompanyScope Scope(string companyId)
            => new(companyId, string.Empty, ["role"], Guid.Empty, Guid.Empty, Guid.Empty);

        private Guid SessionIn(string companyId)
        {
            var token = TestSessionFactory.CreateAccessToken(_fx);
            _fx.GetRequiredService<ISessionInfoService>().Get(token)!.CompanyScope = Scope(companyId);
            return token;
        }

        [Fact]
        [DisplayName("GetList scopes and reaches its data for the company it was authorized for, when the session switches company right after authorization")]
        public void GetList_SessionSwitchesAfterAuthorization_StaysOnTheAuthorizedCompany()
        {
            var token = SessionIn(CompanyA);
            var recorder = new Recorder(_fx.GetRequiredService<ISessionInfoService>(), token);
            var bo = new FormBusinessObject(
                TestBusinessObjectContext.CreateWithOverrides(_fx,
                    (typeof(ICompanyAuthorizationService), recorder),
                    (typeof(IScopeResolver), recorder),
                    (typeof(IRepositoryFactory), recorder)),
                token, GatedProgId);

            Assert.Throws<StopException>(() => bo.GetList(new GetListArgs()));

            Assert.Equal(CompanyA, recorder.AuthorizedFor);
            Assert.Equal(CompanyA, recorder.ScopedFor);
            Assert.Equal(CompanyA, recorder.RepositoryFor);
            // The switch did happen: the session itself is in company B now.
            Assert.Equal(CompanyB, _fx.GetRequiredService<ISessionInfoService>().Get(token)!.CompanyId);
        }

        [Fact]
        [DisplayName("The business object factory hands the business object the snapshot it read, and a business object created with one never reads the session")]
        public void Factory_CompanyScopeGiven_BusinessObjectUsesIt()
        {
            var token = SessionIn(CompanyB);
            var recorder = new Recorder(_fx.GetRequiredService<ISessionInfoService>(), token);
            var factory = new BusinessObjectFactory(
                new TestOverrideServiceProvider(_fx.Provider,
                    (typeof(ICompanyAuthorizationService), recorder),
                    (typeof(IScopeResolver), recorder),
                    (typeof(IRepositoryFactory), recorder)),
                _fx.GetRequiredService<Polhem.Definition.Storage.IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<Polhem.Definition.Language.ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var bo = (FormBusinessObject)factory.CreateBusinessObject(token, GatedProgId, isLocalCall: false, Scope(CompanyA));
            Assert.Throws<StopException>(() => bo.GetList(new GetListArgs()));

            Assert.Equal(CompanyA, recorder.AuthorizedFor);
            Assert.Equal(CompanyA, recorder.ScopedFor);
            Assert.Equal(CompanyA, recorder.RepositoryFor);
        }

        private sealed class StopException : Exception;

        /// <summary>
        /// Allows the call, switches the session to company B the first time it is asked, and records which company
        /// each layer was handed. The repository factory stops the call once it has recorded.
        /// </summary>
        private sealed class Recorder(ISessionInfoService sessions, Guid token)
            : ICompanyAuthorizationService, IScopeResolver, IRepositoryFactory
        {
            public string? AuthorizedFor { get; private set; }
            public string? ScopedFor { get; private set; }
            public string? RepositoryFor { get; private set; }

            public bool Can(SessionCompanyScope companyScope, string modelId, PermissionActions action)
            {
                AuthorizedFor ??= companyScope.CompanyId;
                sessions.Get(token)!.CompanyScope = Scope(CompanyB);
                return true;
            }

            public bool Can(Guid accessToken, string modelId, PermissionActions action)
                => Can(sessions.Get(accessToken)!.CompanyScope, modelId, action);

            public FilterNode? ResolveFilter(SessionCompanyScope companyScope, string modelId, PermissionActions action, FormSchema formSchema)
            {
                ScopedFor ??= companyScope.CompanyId;
                return null;
            }

            public FilterNode? ResolveFilter(Guid accessToken, string modelId, PermissionActions action, FormSchema formSchema)
                => ResolveFilter(sessions.Get(accessToken)!.CompanyScope, modelId, action, formSchema);

            public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope)
                where T : class, IDataFormRepository
            {
                RepositoryFor ??= companyScope.CompanyId;
                throw new StopException();
            }

            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => CreateFormRepository<T>(accessToken, progId, sessions.Get(accessToken)!.CompanyScope);

            public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
        }
    }
}
