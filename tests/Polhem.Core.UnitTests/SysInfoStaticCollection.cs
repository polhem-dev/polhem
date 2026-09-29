namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// Serializes every test class that modifies the process-wide static state of <see cref="SysInfo"/>
    /// (<c>Version</c> / <c>IsDebugMode</c> / <c>AllowedTypeNamespaces</c>).
    /// </summary>
    /// <remarks>
    /// Users write <c>[Collection(SysInfoStaticCollection.Name)]</c>, never the string itself. A mistyped literal
    /// makes xUnit create an implicit collection that no other class shares: the class looks serialized but is not,
    /// and nothing fails at compile time. Referencing the constant turns a typo into a compile error.
    /// </remarks>
    [CollectionDefinition(Name)]
    public static class SysInfoStaticCollection
    {
        /// <summary>The collection name. Reference this constant instead of repeating the string.</summary>
        public const string Name = "SysInfoStatic";
    }
}
