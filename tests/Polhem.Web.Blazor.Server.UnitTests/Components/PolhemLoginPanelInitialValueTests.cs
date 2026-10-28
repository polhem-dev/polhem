using System.ComponentModel;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers <see cref="PolhemLoginPanel.InitialUserId"/> and <see cref="PolhemLoginPanel.InitialPassword"/>.
    /// </summary>
    public sealed class PolhemLoginPanelInitialValueTests : BunitContext
    {
        public PolhemLoginPanelInitialValueTests()
        {
            Services.AddPolhemBlazor();
            Services.AddScoped(_ => new FakeApiServer().CreateClient());
        }

        [Fact]
        [DisplayName("The panel starts with the initial user id and password")]
        public void Render_InitialValues_FillInputs()
        {
            var cut = Render<PolhemLoginPanel>(p => p
                .Add(c => c.InitialUserId, "demo")
                .Add(c => c.InitialPassword, "secret"));

            Assert.Equal("demo", cut.Find("input[type=text]").GetAttribute("value"));
            Assert.Equal("secret", cut.Find("input[type=password]").GetAttribute("value"));
        }

        [Fact]
        [DisplayName("Without initial values the panel starts empty")]
        public void Render_NoInitialValues_InputsEmpty()
        {
            var cut = Render<PolhemLoginPanel>();

            Assert.Equal(string.Empty, cut.Find("input[type=text]").GetAttribute("value"));
            Assert.Equal(string.Empty, cut.Find("input[type=password]").GetAttribute("value"));
        }
    }
}
