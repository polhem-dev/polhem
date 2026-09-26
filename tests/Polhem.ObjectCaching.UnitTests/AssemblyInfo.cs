// `CacheInfoTests.Initialize_DifferentProviderType_ReplacesProvider` has to swap the process-wide static
// `CacheInfo.Provider` temporarily to exercise the type comparison in `Initialize`. The other cache tests
// (`KeyObjectCache`, `ObjectCache`, `FormLayoutCache`, `SessionInfoService`, the settings caches and so on)
// read and write the same provider, so running in parallel races. In CI,
// `KeyObjectCacheTests.Set_WithIKeyObject_UsesGetKey` occasionally lost the value it had just set because
// `FakeCacheProvider` was installed (issue: 2026-05-14 build #25838584823).
// The tests in this assembly are few and light, so disabling parallelization for the whole assembly is the
// simplest fix and the hardest to get wrong.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
