namespace Polhem.Northwind.Backend.UnitTests
{
    /// <summary>
    /// The test classes that seed the demo database share one <see cref="NorthwindSeedFixture"/> and run one after
    /// another: the definition and connection caches are process-wide, so a second fixture would still open the first
    /// one's database.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class NorthwindSeedCollection : ICollectionFixture<NorthwindSeedFixture>
    {
        /// <summary>The collection's name.</summary>
        public const string Name = "NorthwindSeed";
    }
}
