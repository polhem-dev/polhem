using Polhem.Base;
using Polhem.Business.Providers;
using Polhem.Definition;
using Polhem.ObjectCaching;
using Polhem.Definition.Identity;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.DependencyInjection;

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
        /// Creates the configured <see cref="IDefineAccess"/> implementation. Its constructor parameters
        /// are resolved from <paramref name="sp"/> by <see cref="ActivatorUtilities"/>, except a
        /// <see cref="byte"/>[] parameter, which receives the decrypted configuration encryption key.
        /// </summary>
        /// <remarks>
        /// The key is not a service, so it is handed only to a type that has a constructor asking for
        /// one; <see cref="ActivatorUtilities"/> rejects an argument no constructor takes. Among the
        /// constructors it can satisfy it uses the longest, so <see cref="CacheDefineAccess"/> gets its
        /// customization reader and logger. A constructor dependency that is not registered surfaces as
        /// the <see cref="ActivatorUtilities"/> exception, which names the missing service.
        /// </remarks>
        private static IDefineAccess CreateDefineAccess(IServiceProvider sp, string? typeName, byte[] configEncryptionKey)
        {
            var resolvedName = string.IsNullOrWhiteSpace(typeName) ? BackendDefaultTypes.DefineAccess : typeName;
            var type = LoadComponentType(nameof(BackendComponents.DefineAccess), resolvedName, typeof(IDefineAccess));

            bool takesKey = type.GetConstructors()
                .Any(ctor => ctor.GetParameters().Any(parameter => parameter.ParameterType == typeof(byte[])));
            object[] arguments = takesKey ? [configEncryptionKey] : [];
            return (IDefineAccess)ActivatorUtilities.CreateInstance(sp, type, arguments);
        }

        /// <summary>
        /// Creates the configured <see cref="IDefineStorage"/> implementation: through its
        /// <c>(IServiceProvider)</c> constructor when it has one, otherwise with its constructor
        /// parameters resolved from <paramref name="sp"/> by <see cref="ActivatorUtilities"/>, as
        /// <see cref="CreateConfigurableService{T}"/> does.
        /// </summary>
        /// <remarks>
        /// <see cref="FileDefineStorage"/> receives the registered <see cref="PathOptions"/>. The
        /// <c>(IServiceProvider)</c> constructor wins even over a longer one that DI could satisfy: a
        /// database-backed storage such as <c>DbDefineStorage</c> also has constructors taking
        /// <c>IDbConnectionManager</c>, and resolving it here would close the cycle
        /// <c>IDbConnectionManager</c> → <c>IDatabaseSettingsProvider</c> → <see cref="IDefineAccess"/> →
        /// <see cref="IDefineStorage"/>. That constructor defers resolution to the first read.
        /// </remarks>
        private static IDefineStorage CreateDefineStorage(IServiceProvider sp, string? configured, string fallback)
        {
            var typeName = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
            var type = LoadComponentType(nameof(BackendComponents.DefineStorage), typeName, typeof(IDefineStorage));

            if (type.GetConstructor([typeof(IServiceProvider)]) is { } deferred)
                return (IDefineStorage)deferred.Invoke([sp]);
            return (IDefineStorage)ActivatorUtilities.CreateInstance(sp, type);
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
