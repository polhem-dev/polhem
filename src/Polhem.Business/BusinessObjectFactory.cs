using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Business
{
    /// <summary>
    /// Default implementation of <see cref="IBusinessObjectFactory"/>; creates the business logic
    /// object bound to a progId for incoming API calls.
    /// </summary>
    /// <remarks>
    /// Dependencies are supplied via constructor injection by the host DI container. The injected
    /// <see cref="IServiceProvider"/> is the same provider that backs <see cref="IBusinessObjectContext.Services"/>
    /// — it is forwarded to every BO instance so the rare escape-hatch resolutions (login-only
    /// helpers etc.) reach the host's request scope.
    /// </remarks>
    public class BusinessObjectFactory : IBusinessObjectFactory
    {
        private readonly IServiceProvider _services;
        private readonly IDefineAccess _defineAccess;
        private readonly ISessionInfoService _sessionInfoService;
        private readonly ILanguageService _languageService;
        private readonly IBoTypeResolver _resolver;

        /// <summary>
        /// Initializes a new <see cref="BusinessObjectFactory"/>.
        /// </summary>
        /// <param name="services">The host service provider used as the BO escape hatch.</param>
        /// <param name="defineAccess">The define access service.</param>
        /// <param name="sessionInfoService">The session info access service.</param>
        /// <param name="languageService">The language resource service.</param>
        /// <param name="resolver">The progId → BO type resolver.</param>
        public BusinessObjectFactory(
            IServiceProvider services,
            IDefineAccess defineAccess,
            ISessionInfoService sessionInfoService,
            ILanguageService languageService,
            IBoTypeResolver resolver)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
            _sessionInfoService = sessionInfoService ?? throw new ArgumentNullException(nameof(sessionInfoService));
            _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Creates the business logic object registered for the supplied progId.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        /// <param name="isLocalCall">Whether the call originates from a local source.</param>
        /// <remarks>
        /// <para>
        /// One method for every business object, of every family. The type comes from the registry
        /// and construction is uniform, so the factory needs to know nothing about which family the
        /// resolved type belongs to — the same property that lets COM+ expose a single
        /// <c>CoCreateInstance</c>.
        /// </para>
        /// <para>
        /// The business object receives the progId in its declared casing — the reserved progId's own
        /// spelling, or the <see cref="ProgramItem.ProgId"/> of the registry entry — whatever casing the
        /// caller used. Resolution matches a progId case-insensitively, while audit rules and the
        /// audit-policy form compare it exactly; building every object with one canonical spelling
        /// keeps a caller from slipping past those comparisons by changing case. A progId the registry
        /// does not list keeps the caller's casing.
        /// </para>
        /// </remarks>
        public object CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);

            var type = _resolver.Resolve(GetCustomizeId(accessToken), progId);
            var ctx = BuildContext();
            return Activator.CreateInstance(type, ctx, accessToken, CanonicalProgId(progId), isLocalCall)!;
        }

        /// <summary>
        /// Returns the declared spelling of a progId: the reserved binding's, then the base registry
        /// entry's, else the supplied value.
        /// </summary>
        /// <param name="progId">The progId as the caller spelled it.</param>
        private string CanonicalProgId(string progId)
        {
            var reserved = ReservedProgIds.Find(progId);
            if (reserved != null) { return reserved.ProgId; }

            ProgramSettings registry;
            try
            {
                registry = _defineAccess.GetProgramSettings();
            }
            catch (FileNotFoundException)
            {
                // No registry file: every progId resolves to the framework default, and there is no
                // declared spelling to adopt.
                return progId;
            }

            var items = registry.Items;
            return items != null && items.Contains(progId) ? items[progId].ProgId : progId;
        }

        /// <summary>
        /// Reads the session's tenant customization code so the resolver can overlay the
        /// customization <see cref="Polhem.Definition.Settings.ProgramSettings"/> on top of the base one. Anonymous calls and
        /// sessions that have not entered a company yield an empty code, which resolves against
        /// the base layer exactly as before.
        /// </summary>
        /// <param name="accessToken">The access token identifying the session.</param>
        /// <remarks>
        /// The session is the only accepted source: the code selects which tenant's customization
        /// files are read, so a caller-supplied value would be a cross-tenant read.
        /// </remarks>
        private string GetCustomizeId(Guid accessToken)
        {
            if (accessToken == Guid.Empty)
                return string.Empty;
            return _sessionInfoService.Get(accessToken)?.CustomizeId ?? string.Empty;
        }

        private BusinessObjectContext BuildContext() => new BusinessObjectContext
        {
            DefineAccess = _defineAccess,
            SessionInfoService = _sessionInfoService,
            LanguageService = _languageService,
            BoFactory = this,
            Services = _services,
        };
    }
}
