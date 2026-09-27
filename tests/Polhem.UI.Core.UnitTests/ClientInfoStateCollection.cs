namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Serializes the test classes that read or replace the process-wide statics of <see cref="ClientInfo"/>.
    /// xUnit runs every class in one collection sequentially; snapshot and restore alone only hold under serial
    /// execution.
    /// </summary>
    [CollectionDefinition(Name)]
    public static class ClientInfoStateCollection
    {
        /// <summary>The collection name. Reference this constant instead of repeating the string.</summary>
        public const string Name = "ClientInfoState";
    }
}
