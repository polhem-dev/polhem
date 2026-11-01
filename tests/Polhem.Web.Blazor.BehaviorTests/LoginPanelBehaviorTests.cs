using System.ComponentModel;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's login panel must do.
    /// </summary>
    public abstract class LoginPanelBehaviorTests : BunitContext, IAsyncLifetime
    {
        private const string User = "input[autocomplete=username]";
        private const string Password = "input[type=password]";
        private LoginResponse _response = new() { AccessToken = Guid.NewGuid(), UserId = "demo", TimeZone = "Asia/Tokyo" };
        private PolhemApiClient? _client;

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        /// <summary>Gets the panel component, which derives from <see cref="LoginPanelBase"/>.</summary>
        protected abstract Type PanelType { get; }

        private IRenderedComponent<ContainerFragment> RenderPanel(string? initialUser = null, string? initialPassword = null,
            Action<LoginResponse>? loggedIn = null)
        {
            var server = new FakeApiServer().On<LoginRequest>($"{SysProgIds.System}.{SystemActions.Login}", _ => _response);
            _client = server.CreateClient();
            var client = _client;
            Services.AddScoped(_ => client);
            Services.AddScoped(_ => new BlazorUserContext(client, string.Empty));
            var type = PanelType;
            return Render(b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(LoginPanelBase.InitialUserId), initialUser);
                b.AddAttribute(2, nameof(LoginPanelBase.InitialPassword), initialPassword);
                if (loggedIn is not null)
                    b.AddAttribute(3, nameof(LoginPanelBase.OnLoggedIn), EventCallback.Factory.Create(this, loggedIn));
                b.CloseComponent();
            });
        }

        private static void Submit(IRenderedComponent<ContainerFragment> cut) => cut.Find("form.polhem-login-panel").Submit();

        [Fact]
        [DisplayName("The panel starts with the initial values, or empty without them")]
        public void Initial_Values_Fill()
        {
            var cut = RenderPanel("demo", "secret");

            Assert.Equal("demo", cut.Find(User).GetAttribute("value"));
            Assert.Equal("secret", cut.Find(Password).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("An empty access token is a failure in the user's language")]
        public void Login_EmptyToken_ShowsError()
        {
            using var culture = new CultureScope("zh-TW");
            _response = new LoginResponse { AccessToken = Guid.Empty };
            var cut = RenderPanel();

            Submit(cut);

            cut.WaitForAssertion(() => Assert.Contains("登入失敗", cut.Find(".polhem-login-panel__error").TextContent, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A successful login clears the password, signs the client in and raises the callback")]
        public void Login_Success_SignsIn()
        {
            LoginResponse? received = null;
            var cut = RenderPanel("demo", "secret", r => received = r);

            Submit(cut);

            cut.WaitForAssertion(() => Assert.NotNull(received));
            Assert.Equal(string.Empty, cut.Find(Password).GetAttribute("value") ?? string.Empty);
            Assert.Equal(_response.AccessToken, _client!.Session.Credentials.AccessToken);
            Assert.Equal("Asia/Tokyo", _client.Session.Credentials.UserTimeZoneId);
            Assert.Equal("demo", Services.GetRequiredService<BlazorUserContext>().User?.UserId);
        }

        [Fact]
        [DisplayName("A login without a callback neither throws nor reports an error")]
        public void Login_NoCallback_Succeeds()
        {
            var cut = RenderPanel("demo", "secret");

            Submit(cut);

            cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.Find(Password).GetAttribute("value") ?? string.Empty));
            Assert.Empty(cut.FindAll(".polhem-login-panel__error"));
        }
    }
}
