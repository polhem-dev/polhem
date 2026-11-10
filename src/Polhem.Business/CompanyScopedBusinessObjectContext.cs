using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;

namespace Polhem.Business
{
    /// <summary>
    /// A business object's context that carries the business object's own snapshot of the session's company scope,
    /// for what it creates during the call (form plugins), so they act for the same company it does.
    /// </summary>
    internal sealed class CompanyScopedBusinessObjectContext : IBusinessObjectContext
    {
        private readonly IBusinessObjectContext _inner;

        public CompanyScopedBusinessObjectContext(IBusinessObjectContext inner, SessionCompanyScope companyScope)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            CompanyScope = companyScope ?? throw new ArgumentNullException(nameof(companyScope));
        }

        public IDefineAccess DefineAccess => _inner.DefineAccess;

        public ISessionInfoService SessionInfoService => _inner.SessionInfoService;

        public ILanguageService LanguageService => _inner.LanguageService;

        public IBusinessObjectFactory BoFactory => _inner.BoFactory;

        public IServiceProvider Services => _inner.Services;

        public SessionCompanyScope CompanyScope { get; }
    }
}
