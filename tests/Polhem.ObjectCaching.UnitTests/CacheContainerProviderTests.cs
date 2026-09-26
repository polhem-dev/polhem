using System.ComponentModel;
using Polhem.Definition;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Tests of the per-customizeId override containers of <see cref="CacheContainerProvider"/>:
    /// the same customizeId returns the same container, and different customizeIds are isolated.
    /// </summary>
    public class CacheContainerProviderTests
    {
        private static CacheContainerProvider CreateProvider()
            => new(new PathOptions { DefinePath = "/tmp/base", CustomizePath = "/tmp/customize" });

        [Fact]
        [DisplayName("For called repeatedly with the same customizeId returns the same container instance")]
        public void For_SameCustomizeId_ReturnsSameContainer()
        {
            var provider = CreateProvider();

            var first = provider.For("acme");
            var second = provider.For("acme");

            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("For with different customizeIds returns different container instances (tenant isolation)")]
        public void For_DifferentCustomizeId_ReturnsDifferentContainers()
        {
            var provider = CreateProvider();

            var acme = provider.For("acme");
            var globex = provider.For("globex");

            Assert.NotSame(acme, globex);
        }

        [Fact]
        [DisplayName("The CachePrefix of an override container is the customizeId (physical isolation)")]
        public void For_ContainerUsesCustomizeIdAsCachePrefix()
        {
            var provider = CreateProvider();

            var container = (CacheContainerService)provider.For("acme");

            Assert.Equal("acme", container.CachePrefix);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("For throws ArgumentException for an empty customizeId")]
        public void For_EmptyCustomizeId_ThrowsArgumentException(string customizeId)
        {
            Assert.Throws<ArgumentException>(() => CreateProvider().For(customizeId));
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for null paths")]
        public void Constructor_NullPaths_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CacheContainerProvider(null!));
        }
    }
}
