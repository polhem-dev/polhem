namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Serializes every test class that changes the process-wide static components of
    /// <see cref="ApiServiceOptions"/> (<c>PayloadSerializer</c> / <c>PayloadCompressor</c> / <c>PayloadEncryptor</c>).
    /// </summary>
    /// <remarks>
    /// These tests snapshot and restore the static values with try/finally, but that only holds when they **run
    /// serially**. xUnit's default collection-per-class runs different test classes in parallel, and
    /// <c>ApiPayloadTransformer</c> reads these static values directly on the path every <c>JsonRpcExecutor</c>
    /// payload takes to be encrypted or encoded. Loose scheduling on a many-core local machine does not always
    /// trigger it; it goes red on 2-core CI, and the failure message is
    /// <c>NoEncryptionEncryptor is only permitted in debug/development mode</c>, which looks like a production
    /// security bug but is really tests contaminating each other.
    /// The root fix is to move these components into DI; until then this collection serializes them.
    /// <para>
    /// <b>Reinforced on 2026-08-07</b>: this collection only covers the **writers**, and the **readers** (about 19
    /// round-trip test classes going through the payload pipeline at the time) hit the same problem; CI build
    /// #31169045420 went red because of it. Readers keep growing with new tests, and adding <c>[Collection]</c> class
    /// by class is bound to miss some, so the whole assembly is serialized instead with
    /// <c>DisableTestParallelization</c> in <c>AssemblyInfo.cs</c>. This collection is kept as a marker of "which
    /// classes change the static components".
    /// </para>
    /// </remarks>
    [CollectionDefinition(Name)]
    public static class ApiServiceOptionsStateCollection
    {
        /// <summary>The collection name. Reference this constant instead of repeating the string.</summary>
        public const string Name = "ApiServiceOptionsState";
    }
}
