namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Serializes every test class in this assembly that reads or writes <c>SysInfo.IsDebugMode</c>.
    /// </summary>
    /// <remarks>
    /// In debug mode <c>JsonRpcExecutor.MapException</c> passes through the original message of infrastructure
    /// exceptions, so tests that "assert the masked message" and tests that "switch to debug mode to verify the
    /// pass-through" share the same process-wide static. xUnit runs different test classes in parallel by default,
    /// and without serialization they contaminate each other; the symptom is that a test asserting the masked
    /// message intermittently gets the original exception message.
    ///
    /// A collection does not span assemblies, so the definition of the same name in <c>Polhem.Base.UnitTests</c>
    /// does not apply here; each assembly declares its own.
    /// </remarks>
    [CollectionDefinition("SysInfoStatic")]
    public class SysInfoStaticCollection
    {
        // A pure marker with no fixture.
    }
}
