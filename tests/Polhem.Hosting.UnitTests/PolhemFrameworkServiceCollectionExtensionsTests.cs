using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Hosting.UnitTests
{
    public class PolhemFrameworkServiceCollectionExtensionsTests
    {
        [Fact]
        [DisplayName("AddPolhemFramework 傳入 null services 應拋出 ArgumentNullException")]
        public void AddPolhemFramework_NullServices_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework(
                    null!, new BackendConfiguration(), new PathOptions()));
        }

        [Fact]
        [DisplayName("AddPolhemFramework 傳入 null configuration 應拋出 ArgumentNullException")]
        public void AddPolhemFramework_NullConfiguration_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentNullException>(() =>
                services.AddPolhemFramework(null!, new PathOptions()));
        }

        [Fact]
        [DisplayName("AddPolhemFramework 傳入 null pathOptions 應拋出 ArgumentNullException")]
        public void AddPolhemFramework_NullPathOptions_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentNullException>(() =>
                services.AddPolhemFramework(new BackendConfiguration(), null!));
        }
    }
}
