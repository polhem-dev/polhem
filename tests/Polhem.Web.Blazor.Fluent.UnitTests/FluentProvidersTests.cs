using System.ComponentModel;
using Bunit;
using Polhem.Web.Blazor.Fluent.DependencyInjection;
using Polhem.Web.Blazor.Fluent.Theming;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// <see cref="PolhemFluentProviders.IsDarkMode"/> sets the Fluent UI package's theme mode only when it is given.
    /// </summary>
    public class FluentProvidersTests : BunitContext, IAsyncLifetime
    {
        private const string SetThemeMode = "setThemeMode";

        /// <summary>
        /// Initializes a new instance of <see cref="FluentProvidersTests"/>.
        /// </summary>
        public FluentProvidersTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        [Theory]
        [InlineData(true, "dark")]
        [InlineData(false, "light")]
        [DisplayName("A given dark mode sets the matching theme mode")]
        public void Render_DarkModeGiven_SetsThemeMode(bool isDarkMode, string expectedMode)
        {
            Render<PolhemFluentProviders>(p => p.Add(c => c.IsDarkMode, isDarkMode));

            var invocation = Assert.Single(ThemeModeInvocations());
            Assert.Equal(expectedMode, invocation.Arguments[0]?.ToString(), ignoreCase: true);
        }

        [Fact]
        [DisplayName("Without a dark mode the package keeps its own theme mode")]
        public void Render_DarkModeNotGiven_LeavesThemeMode()
        {
            Render<PolhemFluentProviders>();

            Assert.Empty(ThemeModeInvocations());
        }

        [Fact]
        [DisplayName("The theme mode is set again only when the dark mode changes")]
        public void Render_DarkModeChanged_SetsThemeModeOnce()
        {
            var cut = Render<PolhemFluentProviders>(p => p.Add(c => c.IsDarkMode, false));
            cut.Render(p => p.Add(c => c.IsDarkMode, false));
            cut.Render(p => p.Add(c => c.IsDarkMode, true));

            Assert.Equal(2, ThemeModeInvocations().Count);
        }

        private List<JSRuntimeInvocation> ThemeModeInvocations()
            => JSInterop.Invocations.Where(i => i.Identifier.EndsWith(SetThemeMode, StringComparison.Ordinal)).ToList();
    }
}
