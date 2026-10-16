using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers <see cref="UIServices"/>. The only class in this assembly that sets the process-wide provider, and it
    /// restores the previous value, so it needs no serializing collection.
    /// </summary>
    public class UIServicesTests
    {
        private sealed class FakeUserContext : IUserContext
        {
            public Polhem.Definition.Identity.UserInfo? User => null;
            public IReadOnlyDictionary<string, Polhem.Definition.Settings.PermissionActions>? Capabilities => null;
            public Polhem.Definition.Identity.CompanyInfo? Company => null;
            public string DefaultLanguage => "en-US";
        }

        [Fact]
        [DisplayName("Get returns null when no provider is set, which runs the controls without a backend")]
        public void Get_NoProvider_ReturnsNull()
        {
            var original = UIServices.Provider;
            try
            {
                UIServices.Provider = null;
                Assert.Null(UIServices.Get<IUserContext>());
            }
            finally
            {
                UIServices.Provider = original;
            }
        }

        [Fact]
        [DisplayName("Get returns the service the provider registers and null for one it does not")]
        public void Get_WithProvider_ResolvesRegisteredServicesOnly()
        {
            var original = UIServices.Provider;
            var context = new FakeUserContext();
            using var provider = new ServiceCollection().AddSingleton<IUserContext>(context).BuildServiceProvider();
            try
            {
                UIServices.Provider = provider;
                Assert.Same(context, UIServices.Get<IUserContext>());
                Assert.Null(UIServices.Get<Polhem.UI.Core.Definitions.IFormDefinitionSource>());
            }
            finally
            {
                UIServices.Provider = original;
            }
        }
    }
}
