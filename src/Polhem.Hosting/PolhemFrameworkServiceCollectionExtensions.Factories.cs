using Polhem.Base;
using Polhem.Business.Providers;
using Polhem.Definition;
using Polhem.ObjectCaching;
using Polhem.Definition.Identity;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting
{
    /// <summary>
    /// Constructing the pluggable services `AddPolhemFramework` registers, from the type names the
    /// backend configuration names.
    /// </summary>
    /// <remarks>
    /// Kept apart from the registration list so that reading "what gets registered" does not mean
    /// scrolling through "how each one is built". `SecurityKeys` lives here — it exists only as the
    /// return shape of `DecryptSecurityKeys`.
    /// </remarks>
    public static partial class PolhemFrameworkServiceCollectionExtensions
    {
        /// <summary>
        /// Resolves the configured <see cref="IDefineAccess"/> implementation. The supported
        /// constructors, tried in this order, are
        /// <c>(IDefineStorage, PathOptions, ICacheContainer, byte[], ICustomizeDefineReader, ILogger)</c>
        /// (used by <see cref="CacheDefineAccess"/>),
        /// <c>(IDefineStorage, PathOptions, ICacheContainer, byte[], ICustomizeDefineReader)</c>,
        /// <c>(IDefineStorage, PathOptions, ICacheContainer, byte[])</c> and
        /// <c>(IDefineStorage, PathOptions)</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The type declares none of the supported constructors.</exception>
        private static IDefineAccess ResolveDefineAccess(string? typeName, IDefineStorage storage, PathOptions paths, ICacheContainer cache, byte[] configEncryptionKey, ICustomizeDefineReader customizeReader, ILogger? logger)
        {
            var resolvedName = string.IsNullOrWhiteSpace(typeName) ? BackendDefaultTypes.DefineAccess : typeName;
            var type = LoadComponentType(nameof(BackendComponents.DefineAccess), resolvedName, typeof(IDefineAccess));

            var ctorWithLogger = type.GetConstructor(new[] { typeof(IDefineStorage), typeof(PathOptions), typeof(ICacheContainer), typeof(byte[]), typeof(ICustomizeDefineReader), typeof(ILogger) });
            if (ctorWithLogger != null)
                return (IDefineAccess)ctorWithLogger.Invoke(new object?[] { storage, paths, cache, configEncryptionKey, customizeReader, logger });

            var ctorWithReader = type.GetConstructor(new[] { typeof(IDefineStorage), typeof(PathOptions), typeof(ICacheContainer), typeof(byte[]), typeof(ICustomizeDefineReader) });
            if (ctorWithReader != null)
                return (IDefineAccess)ctorWithReader.Invoke(new object[] { storage, paths, cache, configEncryptionKey, customizeReader });

            var ctorFull = type.GetConstructor(new[] { typeof(IDefineStorage), typeof(PathOptions), typeof(ICacheContainer), typeof(byte[]) });
            if (ctorFull != null)
                return (IDefineAccess)ctorFull.Invoke(new object[] { storage, paths, cache, configEncryptionKey });

            var ctorPaths = type.GetConstructor(new[] { typeof(IDefineStorage), typeof(PathOptions) });
            if (ctorPaths != null)
                return (IDefineAccess)ctorPaths.Invoke(new object[] { storage, paths });

            throw new InvalidOperationException(
                $"BackendComponents.{nameof(BackendComponents.DefineAccess)} names '{resolvedName}', which has no supported constructor. " +
                "It needs one of (IDefineStorage, PathOptions, ICacheContainer, byte[], ICustomizeDefineReader, ILogger), " +
                "(IDefineStorage, PathOptions, ICacheContainer, byte[], ICustomizeDefineReader), " +
                "(IDefineStorage, PathOptions, ICacheContainer, byte[]) or (IDefineStorage, PathOptions).");
        }

        /// <summary>
        /// Constructs the configured <see cref="IDefineStorage"/> implementation through its
        /// <c>(IServiceProvider)</c> constructor, or else its <c>(PathOptions)</c> constructor
        /// (used by <see cref="FileDefineStorage"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">The type declares neither constructor.</exception>
        private static IDefineStorage CreateDefineStorage(string? configured, string fallback, IServiceProvider sp, PathOptions paths)
        {
            var typeName = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
            var type = LoadComponentType(nameof(BackendComponents.DefineStorage), typeName, typeof(IDefineStorage));

            // Prefer an (IServiceProvider) ctor — used by DB-backed storage (e.g. DbDefineStorage),
            // which resolves its dependencies lazily to avoid a construction cycle through
            // IDbConnectionManager → IDatabaseSettingsProvider → IDefineAccess → IDefineStorage.
            var ctorWithServiceProvider = type.GetConstructor(new[] { typeof(IServiceProvider) });
            if (ctorWithServiceProvider != null)
                return (IDefineStorage)ctorWithServiceProvider.Invoke(new object[] { sp });

            var ctorWithPaths = type.GetConstructor(new[] { typeof(PathOptions) });
            if (ctorWithPaths != null)
                return (IDefineStorage)ctorWithPaths.Invoke(new object[] { paths });

            throw new InvalidOperationException(
                $"BackendComponents.{nameof(BackendComponents.DefineStorage)} names '{typeName}', which has no supported constructor. " +
                "It needs an (IServiceProvider) or a (PathOptions) constructor.");
        }

        /// <summary>
        /// Creates a configurable service whose implementation type is read from configuration.
        /// Constructor parameters are resolved from <paramref name="sp"/>.
        /// </summary>
        /// <param name="sp">The service provider.</param>
        /// <param name="settingName">The <see cref="BackendComponents"/> property the type name came from, for errors.</param>
        /// <param name="configured">The configured type name, or blank for the default.</param>
        /// <param name="fallback">The default type name.</param>
        /// <remarks>
        /// A constructor dependency that DI cannot resolve surfaces as the <see cref="ActivatorUtilities"/>
        /// exception, whose message names the missing service. That is covered by
        /// `CreateConfigurableService_UnregisteredCtorDependency_ExceptionNamesMissingService`.
        /// </remarks>
        private static T CreateConfigurableService<T>(IServiceProvider sp, string settingName, string? configured, string fallback)
            where T : class
        {
            var typeName = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
            var type = LoadComponentType(settingName, typeName, typeof(T));
            return (T)ActivatorUtilities.CreateInstance(sp, type);
        }

        /// <summary>
        /// Creates the configured <see cref="IApiEncryptionKeyProvider"/>. The static and derived
        /// providers receive the decrypted API key byte[] directly (as the shared key and as root
        /// key material respectively), the derived one falling back to the master key when no API
        /// key is configured; the dynamic provider relies on <see cref="ISessionInfoService"/>
        /// resolved through DI.
        /// </summary>
        private static IApiEncryptionKeyProvider CreateApiEncryptionKeyProvider(IServiceProvider sp, string? configured, SecurityKeys keys)
        {
            var typeName = string.IsNullOrWhiteSpace(configured) ? BackendDefaultTypes.ApiEncryptionKeyProvider : configured;
            var type = LoadComponentType(nameof(BackendComponents.ApiEncryptionKeyProvider), typeName, typeof(IApiEncryptionKeyProvider));

            if (type == typeof(StaticApiEncryptionKeyProvider))
                return new StaticApiEncryptionKeyProvider(keys.ApiEncryptionKey);
            if (type == typeof(DerivedApiEncryptionKeyProvider))
            {
                // This is the default provider, so a deployment that never configured
                // ApiEncryptionKey lands here. Falling back to a root key derived from the master
                // key keeps it working out of the box; an explicitly configured key wins.
                return keys.ApiEncryptionKey.Length > 0
                    ? new DerivedApiEncryptionKeyProvider(keys.ApiEncryptionKey)
                    : DerivedApiEncryptionKeyProvider.FromMasterKey(keys.MasterKey);
            }
            return (IApiEncryptionKeyProvider)ActivatorUtilities.CreateInstance(sp, type);
        }

        /// <summary>
        /// Loads the implementation type a <see cref="BackendComponents"/> setting names, and fails
        /// with an error that names the setting and the type when it cannot be used.
        /// </summary>
        /// <param name="settingName">The <see cref="BackendComponents"/> property the type name came from.</param>
        /// <param name="typeName">The assembly-qualified type name.</param>
        /// <param name="contract">The service type the implementation must be assignable to.</param>
        /// <exception cref="InvalidOperationException">
        /// The assembly cannot be loaded, the type is not in it, or the type does not implement <paramref name="contract"/>.
        /// </exception>
        /// <remarks>
        /// Without this a missing assembly surfaced as a bare <see cref="FileNotFoundException"/> from
        /// the loader, which says what file was missing but not which of the settings named it.
        /// </remarks>
        private static Type LoadComponentType(string settingName, string typeName, Type contract)
        {
            Type? type;
            try
            {
                type = AssemblyLoader.GetType(typeName);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                throw new InvalidOperationException(
                    BeeNameHint.AppendTo($"BackendComponents.{settingName} names '{typeName}', whose assembly could not be loaded.", typeName), ex);
            }

            if (type == null)
            {
                throw new InvalidOperationException(
                    BeeNameHint.AppendTo($"BackendComponents.{settingName} names '{typeName}', which was not found in its assembly.", typeName));
            }
            if (!contract.IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"BackendComponents.{settingName} names '{typeName}', which does not implement {contract.Name}.");
            }
            return type;
        }

        /// <summary>
        /// Decrypts the security keys the framework consumes from <paramref name="settings"/> in one
        /// pass using the master key. Empty entries map to empty byte arrays so downstream
        /// crypto paths see a consistent "no key configured" sentinel.
        /// </summary>
        /// <remarks>
        /// NOTE: <see cref="SecurityKeySettings.CookieEncryptionKey"/> and
        /// <see cref="SecurityKeySettings.DatabaseEncryptionKey"/> are deliberately not decrypted here.
        /// Nothing in the framework reads either one, so decrypting them produced two byte arrays
        /// that were dropped on the floor. Add them back when a consumer exists — not before, or
        /// the bundle grows fields again with nowhere to go.
        /// </remarks>
        private static SecurityKeys DecryptSecurityKeys(SecurityKeySettings settings, string definePath, bool autoCreateMasterKey)
        {
            byte[] masterKey = MasterKeyProvider.GetMasterKey(settings.MasterKeySource, definePath, autoCreateMasterKey);

            return new SecurityKeys(
                MasterKey: masterKey,
                ApiEncryptionKey: Decrypt(masterKey, settings.ApiEncryptionKey),
                ConfigEncryptionKey: Decrypt(masterKey, settings.ConfigEncryptionKey));

            static byte[] Decrypt(byte[] masterKey, string? encryptedKey)
                => StringUtilities.IsNotEmpty(encryptedKey)
                    ? EncryptionKeyProtector.DecryptEncryptedKey(masterKey, encryptedKey!)
                    : Array.Empty<byte>();
        }

        /// <summary>
        /// Decrypted security keys bundle. Each field is the 64-byte combined AES + HMAC
        /// key (or empty when not configured).
        /// </summary>
        private readonly record struct SecurityKeys(
            byte[] MasterKey,
            byte[] ApiEncryptionKey,
            byte[] ConfigEncryptionKey);
    }
}
