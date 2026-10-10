using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Web.Blazor.Server.Components;
using Microsoft.AspNetCore.Components;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the success path of <c>PolhemLoginPanel.OnSubmitAsync</c> (private, so not a cref).
    /// A <see cref="FakeApiServer"/> makes LoginAsync return a controllable <see cref="LoginResponse"/>
    /// without a real API service, covering these paths:
    /// 1. Empty AccessToken: sets an error message and returns early.
    /// 2. Valid AccessToken: clears the password field.
    /// 3. Valid AccessToken without an OnLoggedIn delegate: completes normally.
    /// 4. Valid AccessToken with an OnLoggedIn delegate: invokes the callback.
    /// </summary>
    public class PolhemLoginPanelSuccessPathTests
    {
        private static readonly FieldInfo s_errorField =
            typeof(PolhemLoginPanel).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_passwordField =
            typeof(PolhemLoginPanel).GetField("_password", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly PropertyInfo s_clientProp =
            typeof(PolhemLoginPanel).GetProperty("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private sealed class SyncEventHandler : IHandleEvent
        {
            public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)
                => callback.InvokeAsync(arg);
        }

        private static async Task InvokeOnSubmitAsync(PolhemLoginPanel panel)
        {
            var method = typeof(PolhemLoginPanel).GetMethod(
                "OnSubmitAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)method.Invoke(panel, null)!;
        }

        private static PolhemLoginPanel CreatePanel(LoginResponse response, PolhemApiClient? client = null)
        {
            var panel = new PolhemLoginPanel();
            s_clientProp.SetValue(panel, client ?? LoginServer(response).CreateClient());
            return panel;
        }

        private static FakeApiServer LoginServer(LoginResponse response)
            => new FakeApiServer().On<LoginRequest>($"{SysProgIds.System}.{SystemActions.Login}", _ => response);

        [Fact]
        [DisplayName("OnSubmitAsync sets a login failure error message when LoginAsync returns an empty AccessToken")]
        public async Task OnSubmitAsync_EmptyAccessToken_SetsLoginFailedError()
        {
            using var culture = new CultureScope("en-US");
            var panel = CreatePanel(new LoginResponse { AccessToken = Guid.Empty });

            await InvokeOnSubmitAsync(panel);

            Assert.Equal(
                "Login failed: the server returned an empty access token.",
                (string?)s_errorField.GetValue(panel));
        }

        [Fact]
        [DisplayName("OnSubmitAsync shows the empty-token failure in the user's culture")]
        public async Task OnSubmitAsync_EmptyAccessTokenUnderZhTw_SetsLocalizedError()
        {
            using var culture = new CultureScope("zh-TW");
            var panel = CreatePanel(new LoginResponse { AccessToken = Guid.Empty });

            await InvokeOnSubmitAsync(panel);

            Assert.Equal("登入失敗：伺服器未傳回存取權杖。", (string?)s_errorField.GetValue(panel));
        }

        [Fact]
        [DisplayName("OnSubmitAsync clears the password field to an empty string after a successful login")]
        public async Task OnSubmitAsync_SuccessfulLogin_ClearsPasswordField()
        {
            var panel = CreatePanel(new LoginResponse { AccessToken = Guid.NewGuid() });
            s_passwordField.SetValue(panel, "secret");

            await InvokeOnSubmitAsync(panel);

            Assert.Equal(string.Empty, (string?)s_passwordField.GetValue(panel));
        }

        [Fact]
        [DisplayName("A successful login signs in the circuit's own client with the token and the user's time zone")]
        public async Task OnSubmitAsync_SuccessfulLogin_SignsInCircuitClient()
        {
            var token = Guid.NewGuid();
            var response = new LoginResponse { AccessToken = token, TimeZone = "Asia/Tokyo" };
            var circuitClient = LoginServer(response).CreateClient();
            var otherClient = LoginServer(response).CreateClient();
            var panel = CreatePanel(response, circuitClient);

            await InvokeOnSubmitAsync(panel);

            Assert.Equal(token, circuitClient.Session.Credentials.AccessToken);
            Assert.Equal("Asia/Tokyo", circuitClient.Session.Credentials.UserTimeZoneId);
            Assert.Same(ApiSessionCredentials.Anonymous, otherClient.Session.Credentials);
        }

        [Fact]
        [DisplayName("OnSubmitAsync does not throw or set an error message after a successful login without an OnLoggedIn delegate")]
        public async Task OnSubmitAsync_SuccessfulLoginNoDelegate_NoErrorSet()
        {
            var panel = CreatePanel(new LoginResponse { AccessToken = Guid.NewGuid() });

            await InvokeOnSubmitAsync(panel);

            Assert.Null((string?)s_errorField.GetValue(panel));
        }

        [Fact]
        [DisplayName("OnSubmitAsync invokes the callback with the LoginResponse after a successful login with an OnLoggedIn delegate")]
        public async Task OnSubmitAsync_SuccessfulLoginWithDelegate_InvokesCallback()
        {
            var expectedToken = Guid.NewGuid();
            var panel = CreatePanel(new LoginResponse { AccessToken = expectedToken });

            LoginResponse? captured = null;
            typeof(PolhemLoginPanel)
                .GetProperty("OnLoggedIn", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(panel, EventCallback.Factory.Create<LoginResponse>(
                    new SyncEventHandler(),
                    (LoginResponse r) => { captured = r; }));

            await InvokeOnSubmitAsync(panel);

            Assert.NotNull(captured);
            Assert.Equal(expectedToken, captured!.AccessToken);
        }
    }
}
