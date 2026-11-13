using System.ComponentModel;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.UnitTests.Components
{
    /// <summary>
    /// The options a component falls back to when the host registered none.
    /// </summary>
    public class DefaultBlazorDefinitionOptionsTests
    {
        [Fact]
        [DisplayName("Without host options the definitions are assembled by the loader with no default language")]
        public void Instance_UsesTheLoaderWithoutDefaultLanguage()
        {
            IBlazorDefinitionOptions options = DefaultBlazorDefinitionOptions.Instance;

            Assert.True(options.UseDefinitionLoader);
            Assert.Equal(string.Empty, options.DefaultLanguage);
        }
    }
}
