namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Serializes the test classes that touch process-wide state.
    /// </summary>
    /// <remarks>
    /// This assembly used to have neither <c>[Collection]</c> nor <c>DisableTestParallelization</c>, yet it had three
    /// conflicting behaviors at the same time:
    /// <list type="number">
    /// <item><c>MasterKeyProviderTests</c> sets the <c>POLHEM_MASTER_KEY</c> environment variable to <c>null</c>
    /// and restores it in <c>finally</c>.</item>
    /// <item><c>PolhemTestFixtureSmokeTests</c> builds DI containers **inside the test body**. A container built inside
    /// that null window takes the <c>autoCreate</c> branch of <c>MasterKeyProvider</c>, generates a new key and writes it
    /// back to the environment variable, which <c>finally</c> then overwrites. That container ends up holding a master key
    /// that matches nothing else, which later shows up as a decryption or HMAC failure that does not point to the cause.</item>
    /// <item><c>GlobalEventsTests</c> asserts how many times a global event fires, and every container built fires it
    /// once through <c>DatabaseSettingsCache</c>.</item>
    /// </list>
    /// <para>
    /// WARNING: The criterion is **touching** the same process-wide state, not modifying it. A reader that falls inside a
    /// writer's try/finally window misbehaves just the same; this is exactly the shape this repository has hit before (see
    /// <c>rules/testing.md</c>).
    /// </para>
    /// <para>
    /// NOTE: Since 2026-09-04 this collection is **redundant**: the whole assembly is serialized by
    /// <c>DisableTestParallelization</c> in <c>AssemblyInfo.cs</c>, so **new test classes need not be added here**. It is
    /// kept as a record of which classes touch process-wide state and why (the same approach as
    /// <c>ApiServiceOptionsStateCollection</c> in <c>Polhem.Api.Core</c>). <b>Do not remove
    /// <c>DisableTestParallelization</c> and switch back to this collection because it is redundant</b>: that would trade a
    /// structural guarantee for "remember to add it", a requirement that is bound to be missed, with no signal when it is.
    /// </para>
    /// </remarks>
    [CollectionDefinition(Name)]
    public static class ProcessWideStateCollection
    {
        /// <summary>The collection name.</summary>
        public const string Name = "ProcessWideState";
    }
}
