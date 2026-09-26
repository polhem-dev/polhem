namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Serializes every test class that modifies the process-wide static state of <see cref="SysInfo"/>
    /// (<c>Version</c> / <c>IsDebugMode</c> / <c>AllowedTypeNamespaces</c>).
    /// </summary>
    /// <remarks>
    /// This name was used by <c>[Collection("SysInfoStatic")]</c> before, but **had no matching definition**.
    /// xUnit's implicit grouping still made it work, but without a definition there is no compile-time protection:
    /// a typo in the name does not fail the build, serialization silently stops working, and the result is a race
    /// that only reproduces in CI.
    /// </remarks>
    [CollectionDefinition("SysInfoStatic")]
    public class SysInfoStaticCollection
    {
        // Marker only, no fixture.
    }
}
