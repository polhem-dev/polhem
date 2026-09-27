using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the success path of <c>PolhemLoginPanel.OnSubmitAsync</c> (private, so not a cref).
    /// A fake Factory and a fake IJsonRpcProvider make LoginAsync return a controllable <see cref="LoginResponse"/>
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

        private static readonly PropertyInfo s_factoryProp =
            typeof(PolhemLoginPanel).GetProperty("Factory", BindingFlags.NonPublic | BindingFlags.Instance)!;

        /// <summary>
        /// Answers every call with the given login response. The connector treats any provider other than the
        /// in-process one as a remote transport and encodes the request, so this encodes the response in the
        /// request's format and codec, the way a server answers, and the connector's decode path accepts it.
        /// </summary>
        private sealed class FakeLoginProvider : IJsonRpcProvider
        {
            private readonly LoginResponse _response;

            public FakeLoginProvider(LoginResponse response) => _response = response;

            public Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
            {
                var result = new JsonRpcResult { Value = _response, Codec = request.Params.Codec };
                ApiPayloadConverter.TransformTo(result, request.Params.Format);
                return Task.FromResult(new JsonRpcResponse(request) { Result = result });
            }
        }

        private sealed class FakeConnectorFactory : PolhemApiConnectorFactory
        {
            private readonly IJsonRpcProvider _provider;
            private readonly ApiSessionContext _session;

            public FakeConnectorFactory(IJsonRpcProvider provider, ApiSessionContext session)
                : base(new PolhemBlazorOptions(), session, Polhem.Tests.Shared.EmptyServiceProvider.Instance)
            {
                _provider = provider;
                _session = session;
            }

            public override SystemApiConnector CreateSystemConnector(Guid accessToken)
            {
                var connector = new SystemApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, accessToken, _session);
                typeof(ApiConnector)
                    .GetProperty(nameof(ApiConnector.Provider), BindingFlags.Public | BindingFlags.Instance)!
                    .SetValue(connector, _provider);
                return connector;
            }
        }

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

        private static PolhemLoginPanel CreatePanelWithFakeFactory(LoginResponse response, ApiSessionContext? session = null)
        {
            var panel = new PolhemLoginPanel();
            var factory = new FakeConnectorFactory(new FakeLoginProvider(response), session ?? new ApiSessionContext());
            s_factoryProp.SetValue(panel, factory);
            return panel;
        }

        [Fact]
        [DisplayName("OnSubmitAsync sets a login failure error message when LoginAsync returns an empty AccessToken")]
        public async Task OnSubmitAsync_EmptyAccessToken_SetsLoginFailedError()
        {
            using var culture = new CultureScope("en-US");
            var panel = CreatePanelWithFakeFactory(new LoginResponse { AccessToken = Guid.Empty });

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
            var panel = CreatePanelWithFakeFactory(new LoginResponse { AccessToken = Guid.Empty });

            await InvokeOnSubmitAsync(panel);

            Assert.Equal("登入失敗：伺服器未傳回存取權杖。", (string?)s_errorField.GetValue(panel));
        }

        [Fact]
        [DisplayName("OnSubmitAsync clears the password field to an empty string after a successful login")]
        public async Task OnSubmitAsync_SuccessfulLogin_ClearsPasswordField()
        {
            var panel = CreatePanelWithFakeFactory(new LoginResponse { AccessToken = Guid.NewGuid() });
            s_passwordField.SetValue(panel, "secret");

            await InvokeOnSubmitAsync(panel);

            Assert.Equal(string.Empty, (string?)s_passwordField.GetValue(panel));
        }

        [Fact]
        [DisplayName("A successful login sets the user's time zone on the circuit's own session, not on the ambient one")]
        public async Task OnSubmitAsync_SuccessfulLogin_SetsCircuitSessionTimeZone()
        {
            var circuitSession = new ApiSessionContext();
            var panel = CreatePanelWithFakeFactory(
                new LoginResponse { AccessToken = Guid.NewGuid(), TimeZone = "Asia/Tokyo" }, circuitSession);

            await InvokeOnSubmitAsync(panel);

            Assert.Equal("Asia/Tokyo", circuitSession.UserTimeZoneId);
            Assert.NotEqual("Asia/Tokyo", ApiSessionContext.Ambient.UserTimeZoneId);
        }

        [Fact]
        [DisplayName("OnSubmitAsync does not throw or set an error message after a successful login without an OnLoggedIn delegate")]
        public async Task OnSubmitAsync_SuccessfulLoginNoDelegate_NoErrorSet()
        {
            var panel = CreatePanelWithFakeFactory(new LoginResponse { AccessToken = Guid.NewGuid() });

            await InvokeOnSubmitAsync(panel);

            Assert.Null((string?)s_errorField.GetValue(panel));
        }

        [Fact]
        [DisplayName("OnSubmitAsync invokes the callback with the LoginResponse after a successful login with an OnLoggedIn delegate")]
        public async Task OnSubmitAsync_SuccessfulLoginWithDelegate_InvokesCallback()
        {
            var expectedToken = Guid.NewGuid();
            var panel = CreatePanelWithFakeFactory(new LoginResponse { AccessToken = expectedToken });

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
