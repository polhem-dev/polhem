using Polhem.Api.Core.JsonRpc;
using Polhem.Base.Expressions;
using Polhem.Business;
using Polhem.Business.Form;
using Polhem.Business.Permission;
using Polhem.Business.Security;
using Polhem.Business.Session;
using Polhem.Expressions;
using Polhem.Db;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Hosting.Audit;
using Polhem.Hosting.CacheNotify;
using Polhem.Hosting.Session;
using Polhem.Definition;
using Polhem.Definition.Logging;
using Polhem.ObjectCaching;
using Polhem.ObjectCaching.Services;
using Polhem.Definition.Identity;
using Polhem.Definition.Organization;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting
{
    /// <summary>
    /// Registers Polhem framework services in the DI container.
    /// </summary>
    public static partial class PolhemFrameworkServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Polhem framework services and decrypts security keys from
        /// <paramref name="configuration"/>, failing when the master key is missing. See the
        /// overload that takes <c>autoCreateMasterKey</c> for the details.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The backend configuration (from SystemSettings.xml).</param>
        /// <param name="pathOptions">Path configuration that locates definition files.</param>
        public static IServiceCollection AddPolhemFramework(
            this IServiceCollection services,
            BackendConfiguration configuration,
            PathOptions pathOptions)
            => services.AddPolhemFramework(configuration, pathOptions, autoCreateMasterKey: false);

        /// <summary>
        /// Registers Polhem framework services and decrypts security keys from
        /// <paramref name="configuration"/>. This call alone brings the framework up:
        /// <c>app.UsePolhemFramework()</c> carries no bootstrap work of its own — callers obtain
        /// <see cref="DbAccess"/> via <see cref="IDbAccessFactory"/> (ctor injected). It does
        /// still run host-side startup checks, so an ASP.NET Core host should keep calling it.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The backend configuration (from SystemSettings.xml).</param>
        /// <param name="pathOptions">
        /// Path configuration that locates definition files (SystemSettings.xml, FormSchema/, etc.).
        /// Registered as a singleton so framework services can ctor-inject it directly.
        /// </param>
        /// <param name="autoCreateMasterKey">Whether to auto-create the master key file if missing.</param>
        public static IServiceCollection AddPolhemFramework(
            this IServiceCollection services,
            BackendConfiguration configuration,
            PathOptions pathOptions,
            bool autoCreateMasterKey)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(pathOptions);

            // 1. Decrypt the security keys once and thread them through downstream ctors.
            //    Each key is byte[]; empty means "not configured" (callers gracefully no-op
            //    when the relevant crypto path runs without a key).
            var keys = DecryptSecurityKeys(configuration.SecurityKeySettings, pathOptions.DefinePath, autoCreateMasterKey);

            var components = configuration.Components;

            // 2. PathOptions — registered as a singleton so consumers can ctor-inject it.
            services.AddSingleton(pathOptions);

            // 3. Underlying cache provider (in-memory / Redis / ...). Idempotent — no-op
            //    when the configured type matches the current provider's runtime type.
            CacheInfo.Initialize(configuration);

            // 4. IDefineStorage / IDefineAccess / ICacheContainer — singletons.
            services.AddSingleton<IDefineStorage>(sp => CreateDefineStorage(
                components.DefineStorage, BackendDefaultTypes.DefineStorage, sp, sp.GetRequiredService<PathOptions>()));
            //    The data source is passed as a factory, not an instance. Resolving it here would
            //    close the cycle ICacheContainer → ICacheDataSourceProvider → repositories →
            //    IDefineAccess → ICacheContainer; deferring to the first cache miss breaks it.
            services.AddSingleton<ICacheContainer>(sp =>
                new CacheContainerService(
                    sp.GetRequiredService<IDefineStorage>(),
                    sp.GetRequiredService<PathOptions>(),
                    string.Empty,
                    sp.GetRequiredService<ICacheDataSourceProvider>));

            // 4b. Tenant customization-override layer: per-customizeId cache provider + reader.
            //     Both honour PathOptions.CustomizePath — when it is empty (the standard,
            //     non-customized deployment) the reader short-circuits every lookup to null, so
            //     all consumers degrade to pure base, bit-for-bit identical to before. Always
            //     registered; behaviour is gated entirely by CustomizePath, not by presence.
            services.AddSingleton<ICacheContainerProvider>(sp =>
                new CacheContainerProvider(sp.GetRequiredService<PathOptions>()));
            //     The reader follows the storage. A DB-backed IDefineStorage keeps base and
            //     customization rows in one table, told apart by customize_id, so it implements
            //     ICustomizeDefineReader itself and must be preferred — registering the file-based
            //     reader unconditionally would send every customization lookup to the filesystem and
            //     leave DB customizations permanently unreachable.
            services.AddSingleton<ICustomizeDefineReader>(sp =>
                sp.GetRequiredService<IDefineStorage>() as ICustomizeDefineReader
                ?? new CustomizeDefineReader(
                    sp.GetRequiredService<ICacheContainerProvider>(),
                    sp.GetRequiredService<PathOptions>()));
            //     The writer follows the storage for the same reason. It exists for exactly one
            //     artifact — the plugin bindings a deployment maintains through the framework's own
            //     local-only API — so the rest of the customization layer stays read-only.
            services.AddSingleton<ICustomizeDefineWriter>(sp =>
                sp.GetRequiredService<IDefineStorage>() as ICustomizeDefineWriter
                ?? new CustomizeDefineWriter(
                    sp.GetRequiredService<ICacheContainerProvider>(),
                    sp.GetRequiredService<PathOptions>()));

            services.AddSingleton<IDefineAccess>(sp =>
                ResolveDefineAccess(
                    components.DefineAccess,
                    sp.GetRequiredService<IDefineStorage>(),
                    sp.GetRequiredService<PathOptions>(),
                    sp.GetRequiredService<ICacheContainer>(),
                    keys.ConfigEncryptionKey,
                    sp.GetRequiredService<ICustomizeDefineReader>(),
                    sp.GetService<ILoggerFactory>()?.CreateLogger<CacheDefineAccess>()));

            // 5. Database settings provider (used by DbConnectionManager bootstrap).
            services.AddSingleton<IDatabaseSettingsProvider>(sp =>
                new DefineAccessDatabaseSettingsProvider(sp.GetRequiredService<IDefineAccess>()));

            // 6. IDbConnectionManager + IDbAccessFactory — DI-injectable singletons.
            //    Callers ctor-inject IDbAccessFactory.Create(databaseId) instead of using
            //    the removed static DbConnectionManager facade.
            services.AddSingleton<IDbConnectionManager>(sp =>
                new DbConnectionManagerService(sp.GetRequiredService<IDatabaseSettingsProvider>()));
            services.AddSingleton<IDbAccessFactory>(sp =>
            {
                // DB anomaly logging (opt-in). Lazy writer resolver breaks the construction cycle
                // IDbAccessFactory → IAnomalyLogWriter → AuditLogDbSink → IDbAccessFactory.
                //
                // IMPORTANT: keep the null when anomaly logging is off — do not simplify this to an
                // unconditional resolve on the grounds that a no-op writer is registered either way.
                // DbAccess short-circuits on a null writer; a non-null no-op would make every DB
                // command start a Stopwatch and take the try/catch path to produce a record that is
                // then thrown away.
                var audit = configuration.AuditLogOptions;
                Func<IAnomalyLogWriter?>? anomalyWriterFactory =
                    audit.AnomalyEnabled ? () => sp.GetService<IAnomalyLogWriter>() : null;
                var anomalyOptions = audit.AnomalyEnabled ? configuration.LogOptions.DbAccess : null;
                return new DbAccessFactory(
                    sp.GetRequiredService<IDbConnectionManager>(), 0, anomalyWriterFactory, anomalyOptions);
            });

            // 6b. Cache-notify bump primitive — stateless; builds dialect SQL per call and runs
            //     it on the caller's transaction. No consumer wired yet (poller / business
            //     repositories arrive in later stages); registered now so it is injectable.
            services.AddSingleton<ICacheNotifyService, CacheNotifyService>();
            services.AddSingleton<ICacheNotifyReader, CacheNotifyReader>();

            // 6b-2. Reserved progId self-registration. Registered ahead of every other hosted
            //       service on purpose: hosted services start in registration order (the default,
            //       HostOptions.ServicesStartConcurrently being false), and this one both writes the
            //       registry and refuses to start a host whose reserved progIds do not resolve — a
            //       check worth failing before anything else comes up.
            services.AddHostedService<Registry.ReservedProgIdRegistrationService>();

            // 6b-3. Startup warning naming the forms that declare no permission model, which every
            //       authenticated user of the company can read and write. Logs only; never fails.
            services.AddHostedService<Registry.UnguardedFormWarningService>();

            // 6b-4. Startup warning naming the methods that declare replay protection while the wire
            //       frame it depends on is off. Logs only; never fails.
            services.AddHostedService<Registry.ReplayProtectionWarningService>();

            // 6c. Cache-notify polling hosted service. The poller publishes observed versions to
            //     CacheInfo.NotifyVersions; each cache entry carrying a matching ChangeNotifyKey
            //     expires on its next read. The poller is only registered when enabled; hosts without
            //     an IHost (e.g. unit-test service providers) simply never start the hosted service.
            services.AddSingleton(configuration.CacheNotifyOptions);
            if (configuration.CacheNotifyOptions.Enabled)
            {
                services.AddHostedService<CacheNotifyPoller>();
            }

            // 6c-2. Expired session cleanup. Reads no longer delete expired rows on the way past,
            //       and every sign-in inserts one, so this is what bounds st_session. Same shape as
            //       the poller above: registered only when enabled, inert without an IHost.
            services.AddSingleton(configuration.SessionCleanupOptions);
            if (configuration.SessionCleanupOptions.Enabled)
            {
                services.AddHostedService<ExpiredSessionCleanupService>();
            }

            // 6d. Log writing. Opt-in; both writer interfaces stay injectable either way.
            RegisterAuditLogWriters(services, configuration.AuditLogOptions);

            // 5. Replaceable core services, registered as Singleton: no consumer requires
            //    per-request scope today, and registering as Scoped would block resolution
            //    through the singleton BusinessObjectFactory.
            services.AddSingleton<IAccessTokenValidator>(sp =>
                CreateConfigurableService<IAccessTokenValidator>(sp, nameof(BackendComponents.AccessTokenValidator),
                    components.AccessTokenValidator, BackendDefaultTypes.AccessTokenValidator));
            services.AddSingleton<ISessionInfoService>(sp =>
                CreateConfigurableService<ISessionInfoService>(sp, nameof(BackendComponents.SessionInfoService),
                    components.SessionInfoService, BackendDefaultTypes.SessionInfoService));
            // The deployment's own language resources win; the framework's shipped translations of
            // its own UI text and messages answer what they do not declare.
            services.AddSingleton<ILanguageService>(sp =>
                new FrameworkLanguageService(new LanguageService(
                    sp.GetRequiredService<IDefineAccess>(),
                    sp.GetRequiredService<ICustomizeDefineReader>())));
            services.AddSingleton<ICompanyInfoService>(sp =>
                CreateConfigurableService<ICompanyInfoService>(sp, nameof(BackendComponents.CompanyInfoService),
                    components.CompanyInfoService, BackendDefaultTypes.CompanyInfoService));
            services.AddSingleton<ICacheDataSourceProvider>(sp =>
                CreateConfigurableService<ICacheDataSourceProvider>(sp, nameof(BackendComponents.CacheDataSourceProvider),
                    components.CacheDataSourceProvider, BackendDefaultTypes.CacheDataSourceProvider));

            // Company binding shared by EnterCompany and session rebuild — both must land on the
            // same session state, so the derivation lives in one place.
            services.AddSingleton<SessionCompanyBinder>();

            // Expression engine + form rule processor. Both are stateless singletons; the evaluator
            // caches compiled expressions process-wide, so a single instance is preferred.
            services.AddSingleton<IExpressionEvaluator, DynamicExpressoEvaluator>();
            services.AddSingleton<IFormRuleProcessor, FormRuleProcessor>();

            // 6. IApiEncryptionKeyProvider — Static and Derived need key material from the
            //    settings; Dynamic needs ISessionInfoService.
            services.AddSingleton<IApiEncryptionKeyProvider>(sp =>
                CreateApiEncryptionKeyProvider(sp, components.ApiEncryptionKeyProvider, keys));

            // 7. Login attempt tracker — registered by default.
            //    WARNING: Login is the one credential-checking method reachable anonymously, so
            //    leaving this unregistered meant a stock deployment had no account lockout at all
            //    while a complete implementation sat unused in the box. Defaults are 5 consecutive
            //    failures and a 15-minute lockout (LoginAttemptTracker.Default* constants).
            //    Registration is TryAdd, so an app that wants a different policy — or none —
            //    registers its own ILoginAttemptTracker and that one wins. Tests inject per-call
            //    via TestOverrideServiceProvider.
            services.TryAddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();

            // 8. Business-object factory + progId → BO type resolver.
            //    ProgramSettingsBoTypeResolver looks up ProgramItem.BusinessObject in
            //    ProgramSettings.xml. A progId that declares none falls back to the framework
            //    default — FormBusinessObject, or the framework's own object for a reserved progId.
            //    A progId that declares a name the registry cannot honour fails the request rather
            //    than degrading (see ProgramSettingsBoTypeResolver).
            services.AddSingleton<IBoTypeResolver>(sp =>
                new ProgramSettingsBoTypeResolver(
                    sp.GetRequiredService<IDefineAccess>(),
                    sp.GetRequiredService<ICustomizeDefineReader>()));
            //    The factory is no longer replaceable through BackendComponents: what it builds is
            //    decided by the registry, one progId at a time, which is both finer-grained and
            //    per-tenant. Swapping the whole factory was the only way to change a system business
            //    object before, and it was process-wide.
            services.AddSingleton<IBusinessObjectFactory, BusinessObjectFactory>();

            //    Business plugin chain resolver. Reads PluginSettings, base plus the tenant
            //    customization, and caches the resolved chain per (customizationCode, progId).
            services.AddSingleton<IFormPluginResolver>(sp =>
                new PluginSettingsResolver(
                    sp.GetRequiredService<IDefineAccess>(),
                    sp.GetRequiredService<ICustomizeDefineReader>()));

            // 9. Repository factory — one registration for every repository, on both axes.
            services.AddSingleton<IRepositoryDatabaseRouter, RepositoryDatabaseRouter>();
            //    progId → repository type with the tenant customization overlay, the counterpart of
            //    IBoTypeResolver above. Both overlay services are resolved as required. Were either
            //    registration dropped, an optional lookup would leave every tenant's repository
            //    override ignored while every request still succeeded.
            services.AddSingleton<IRepositoryTypeResolver>(sp =>
                new ProgramSettingsRepositoryTypeResolver(
                    sp.GetRequiredService<IDefineAccess>(),
                    sp.GetRequiredService<ICustomizeDefineReader>(),
                    sp.GetRequiredService<ISessionInfoService>()));
            services.AddSingleton<IRepositoryFactory>(sp =>
                CreateConfigurableService<IRepositoryFactory>(sp, nameof(BackendComponents.RepositoryFactory),
                    components.RepositoryFactory, BackendDefaultTypes.RepositoryFactory));

            // NOTE: individual repositories are deliberately NOT registered here. Consumers
            // ctor-inject IRepositoryFactory and create what they need per call, the same way
            // FormBusinessObject obtains its form repository by progId. Registering them one by one
            // made every new system table a three-place edit (factory method, DI line, consumer
            // ctor); this keeps it to the one factory registration above.

            // Permission services: per-company role-permission snapshot cache + layer-1 Can check.
            services.AddSingleton<IRolePermissionService>(sp =>
                new RolePermissionService(sp.GetRequiredService<ICacheContainer>()));
            services.AddSingleton<ICompanyAuthorizationService>(sp =>
                new CompanyAuthorizationService(
                    sp.GetRequiredService<ISessionInfoService>(),
                    sp.GetRequiredService<IRolePermissionService>()));
            // Deployment-level authorization: a separate axis from the company-scoped service above.
            // The two never fall back to one another — see IDeploymentAuthorizationService.
            services.AddSingleton<IDeploymentAuthorizationService>(sp =>
                new DeploymentAuthorizationService(
                    sp.GetRequiredService<ISessionInfoService>(),
                    sp.GetRequiredService<IRepositoryFactory>()));

            // API key validation (application identity). Registered plainly rather than through the
            // configurable-component path: a host that wants different key storage replaces it with
            // services.AddSingleton<IApiKeyValidator, MyValidator>() after AddPolhemFramework.
            services.AddSingleton<IApiKeyValidator>(sp =>
                new ApiKeyValidator(sp.GetRequiredService<ICacheContainer>()));
            // The gate state as a question the API layer can ask without referencing the cache
            // implementation. UsePolhemFramework's startup check is the only caller today.
            services.AddSingleton<IApiKeyGateStateProvider>(sp =>
                new ApiKeyGateStateProvider(sp.GetRequiredService<ICacheContainer>()));

            // Audit rules: per-company snapshot of st_audit_rule, consulted by FormBusinessObject
            // before writing a change or access entry. Registered unconditionally — the audit
            // master switch is checked at the call site, not here, so toggling it needs no
            // re-registration.
            services.AddSingleton<IAuditRuleService>(sp =>
                new AuditRuleService(sp.GetRequiredService<ICacheContainer>()));

            // Organization: per-company department-tree snapshot cache (record-scope source).
            services.AddSingleton<IDepartmentTreeService>(sp =>
                new DepartmentTreeService(sp.GetRequiredService<ICacheContainer>()));
            // Record-scope identity: resolves the current user's employee/department (EnterCompany
            // snapshots the result onto SessionInfo for zero-DB scope filtering).
            services.AddSingleton<IEmployeeContextResolver>(sp =>
                new EmployeeContextResolver(sp.GetRequiredService<IRepositoryFactory>()));
            // Record-scope (layer-2): resolves (model, action) + session identity + grants + model
            // default + department tree into a read filter / per-row verdict.
            services.AddSingleton<IScopeResolver>(sp =>
                new ScopeResolver(
                    sp.GetRequiredService<ISessionInfoService>(),
                    sp.GetRequiredService<IRolePermissionService>(),
                    sp.GetRequiredService<IDepartmentTreeService>(),
                    sp.GetRequiredService<IDefineAccess>()));

            // 10. JsonRpcExecutor — transient (per request); its dependencies (factories,
            //     validators, key providers) are resolved from the container at construction.
            //     Built explicitly rather than by the activator, so the constructor that carries
            //     anomaly logging is the one used, not whichever overload the activator prefers.
            //     The logger is a property, so it is assigned here rather than by the activator.
            services.AddTransient(sp =>
            {
                var executor = new JsonRpcExecutor(
                    sp.GetRequiredService<IBusinessObjectFactory>(),
                    sp.GetRequiredService<IAccessTokenValidator>(),
                    sp.GetRequiredService<IApiEncryptionKeyProvider>(),
                    sp.GetService<IAnomalyLogWriter>(),
                    sp.GetService<AuditLogOptions>(),
                    sp.GetService<ISessionInfoService>());
                executor.Logger = sp.GetService<ILoggerFactory>()?.CreateLogger<JsonRpcExecutor>();
                executor.LanguageService = sp.GetService<ILanguageService>();
                return executor;
            });

            return services;
        }

        /// <summary>
        /// Registers <see cref="IAuditLogWriter"/> and <see cref="IAnomalyLogWriter"/> for the
        /// supplied options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The audit-log section of the backend configuration.</param>
        /// <remarks>
        /// <para>
        /// Opt-in: when disabled every consumer gets the no-op writer, so both interfaces are
        /// always injectable with zero behavioural change. When enabled, the background writer
        /// batches to the log database; hosts without an IHost (e.g. in-process local) set
        /// <c>UseBackgroundWriter=false</c> for synchronous writes.
        /// </para>
        /// <para>
        /// One instance serves both interfaces — the queue, the batch drain and the saturation
        /// fallback are identical for an audit record and an anomaly record. Registering the
        /// concrete type first is what makes the two resolutions share it. The anomaly half has
        /// its own switch: a deployment can keep the audit trail on while leaving anomaly
        /// recording off, which is why it is registered separately rather than aliased onto the
        /// audit registration.
        /// </para>
        /// </remarks>
        private static void RegisterAuditLogWriters(IServiceCollection services, AuditLogOptions options)
        {
            services.AddSingleton(options);

            bool anomalyEnabled = options is { Enabled: true, AnomalyEnabled: true };
            if (!anomalyEnabled) { services.AddSingleton<IAnomalyLogWriter>(NullLogWriter.Instance); }

            if (!options.Enabled)
            {
                services.AddSingleton<IAuditLogWriter>(NullLogWriter.Instance);
                return;
            }

            // Built through the factory, not by the container: every repository now takes
            // (IRepositoryContext, Guid, string), and the container can supply none of those
            // three. Registering the concrete type directly would resolve at first use, not
            // at registration — and only in a host that has audit logging on.
            services.AddSingleton<IAuditLogWriteRepository>(sp =>
                sp.GetRequiredService<IRepositoryFactory>().Create<IAuditLogWriteRepository>());
            // TryAdd: IAuditLogSink is the public seam for sending records somewhere other than the
            // log database, so a sink the host registered first must not be overridden here.
            services.TryAddSingleton<IAuditLogSink, AuditLogDbSink>();

            if (options.UseBackgroundWriter)
            {
                services.AddSingleton<AuditLogWriterService>();
                services.AddSingleton<IAuditLogWriter>(sp => sp.GetRequiredService<AuditLogWriterService>());
                services.AddHostedService(sp => sp.GetRequiredService<AuditLogWriterService>());
                if (anomalyEnabled)
                {
                    services.AddSingleton<IAnomalyLogWriter>(sp => sp.GetRequiredService<AuditLogWriterService>());
                }
                return;
            }

            services.AddSingleton<SynchronousAuditLogWriter>();
            services.AddSingleton<IAuditLogWriter>(sp => sp.GetRequiredService<SynchronousAuditLogWriter>());
            if (anomalyEnabled)
            {
                services.AddSingleton<IAnomalyLogWriter>(sp => sp.GetRequiredService<SynchronousAuditLogWriter>());
            }
        }
    }
}
