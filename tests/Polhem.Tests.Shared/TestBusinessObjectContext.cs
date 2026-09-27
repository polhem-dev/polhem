using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Test helper that builds an <see cref="IBusinessObjectContext"/> snapshot from a per-class
    /// <see cref="PolhemTestFixture"/>. Use in test fakes or direct-construction tests.
    /// </summary>
    public static class TestBusinessObjectContext
    {
        /// <summary>
        /// Creates a <see cref="BusinessObjectContext"/> from the supplied fixture's provider.
        /// Returns a fresh instance per call (cheap; just snapshotting refs).
        /// </summary>
        /// <param name="fixture">The per-class fixture supplying the service provider.</param>
        public static IBusinessObjectContext Create(PolhemTestFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            return CreateFrom(fixture.Provider);
        }

        /// <summary>
        /// Creates a <see cref="BusinessObjectContext"/> with one or more service overrides applied on top
        /// of the supplied fixture's provider. Handy for tests that need to inject a fake
        /// <c>ILoginAttemptTracker</c> etc. without rebuilding the container.
        /// </summary>
        public static IBusinessObjectContext CreateWithOverrides(PolhemTestFixture fixture, params (Type ServiceType, object? Instance)[] overrides)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            ArgumentNullException.ThrowIfNull(overrides);
            var sp = fixture.Provider;
            return new BusinessObjectContext
            {
                DefineAccess = sp.GetRequiredService<IDefineAccess>(),
                SessionInfoService = sp.GetRequiredService<ISessionInfoService>(),
                LanguageService = sp.GetRequiredService<ILanguageService>(),
                BoFactory = sp.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(sp, overrides),
            };
        }

        /// <summary>
        /// Creates a <see cref="BusinessObjectContext"/> with a custom <see cref="IDefineAccess"/> swapped in.
        /// Used by tests that need to redirect <c>Save*</c> writes to an isolated temp directory
        /// while keeping every other service resolved from the supplied fixture.
        /// </summary>
        public static IBusinessObjectContext CreateWithDefineAccess(PolhemTestFixture fixture, IDefineAccess defineAccess)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            ArgumentNullException.ThrowIfNull(defineAccess);
            var sp = fixture.Provider;
            return new BusinessObjectContext
            {
                DefineAccess = defineAccess,
                SessionInfoService = sp.GetRequiredService<ISessionInfoService>(),
                // Bind a per-call LanguageService to the swapped IDefineAccess so language
                // lookups in the test see the same temp-redirected store.
                LanguageService = new LanguageService(defineAccess, null),
                BoFactory = sp.GetRequiredService<IBusinessObjectFactory>(),
                Services = sp,
            };
        }

        private static BusinessObjectContext CreateFrom(IServiceProvider sp)
        {
            return new BusinessObjectContext
            {
                DefineAccess = sp.GetRequiredService<IDefineAccess>(),
                SessionInfoService = sp.GetRequiredService<ISessionInfoService>(),
                LanguageService = sp.GetRequiredService<ILanguageService>(),
                BoFactory = sp.GetRequiredService<IBusinessObjectFactory>(),
                Services = sp,
            };
        }
    }
}
