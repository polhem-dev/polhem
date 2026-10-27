using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Identity;

namespace Polhem.Web.Blazor.Server.UnitTests.Identity
{
    /// <summary>
    /// <see cref="CircuitUserContext"/> records a circuit's sign-in, company and capabilities, and each circuit has its
    /// own. A <see cref="FakeApiServer"/> answers the client's calls.
    /// </summary>
    public class CircuitUserContextTests
    {
        private static readonly Guid s_token = new("bbbbbbbb-0000-0000-0000-000000000001");

        private static FakeApiServer CreateServer() => new FakeApiServer()
            .On<LoginRequest>($"{SysProgIds.System}.{SystemActions.Login}", request => new LoginResponse
            {
                AccessToken = s_token,
                UserId = request.UserId,
                UserName = "Demo User",
                TimeZone = "Asia/Taipei",
                Culture = "zh-TW",
            })
            .On<EnterCompanyRequest>($"{SysProgIds.System}.{SystemActions.EnterCompany}", request => new EnterCompanyResponse
            {
                Company = new CompanyInfo { CompanyId = request.CompanyId },
                Capabilities = new Dictionary<string, PermissionActions>(StringComparer.Ordinal) { ["Orders"] = PermissionActions.Read },
            });

        private static (CircuitUserContext Context, PolhemApiClient Client) Create()
        {
            var client = CreateServer().CreateClient();
            return (new CircuitUserContext(client, new PolhemBlazorOptions { DefaultLanguage = "en-US" }), client);
        }

        [Fact]
        [DisplayName("ApplyLoginResult records the user and leaves the company and capabilities empty")]
        public async Task ApplyLoginResult_RecordsUserAndClearsCompany()
        {
            var (context, client) = Create();
            await context.EnterCompanyAsync("ACME");

            context.ApplyLoginResult(await client.System.LoginAsync("demo", "demo"));

            Assert.Equal("demo", context.User?.UserId);
            Assert.Equal("Asia/Taipei", context.User?.TimeZone);
            Assert.Equal("zh-TW", context.User?.Culture);
            Assert.Null(context.Company);
            Assert.Null(context.Capabilities);
        }

        [Fact]
        [DisplayName("EnterCompanyAsync records the company and the capabilities the server returned")]
        public async Task EnterCompanyAsync_RecordsCompanyAndCapabilities()
        {
            var (context, _) = Create();

            var response = await context.EnterCompanyAsync("ACME");

            Assert.Equal("ACME", response.Company.CompanyId);
            Assert.Equal("ACME", context.Company?.CompanyId);
            Assert.Equal(PermissionActions.Read, context.Capabilities!["Orders"]);
        }

        [Fact]
        [DisplayName("SignOut signs the client out and clears the user, the company and the capabilities")]
        public async Task SignOut_ClearsContextAndClient()
        {
            var (context, client) = Create();
            context.ApplyLoginResult(await client.System.LoginAsync("demo", "demo"));
            await context.EnterCompanyAsync("ACME");

            context.SignOut();

            Assert.Null(context.User);
            Assert.Null(context.Company);
            Assert.Null(context.Capabilities);
            Assert.Equal(Guid.Empty, client.Session.Credentials.AccessToken);
        }

        [Fact]
        [DisplayName("DefaultLanguage comes from the package options")]
        public void DefaultLanguage_ComesFromOptions()
        {
            var (context, _) = Create();

            Assert.Equal("en-US", context.DefaultLanguage);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor gives each scope its own context, also as its IUserContext")]
        public void AddPolhemBlazor_RegistersScopedContext()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(options => options.UseRemoteProvider("http://localhost:1/api", "key"));
            using var provider = services.BuildServiceProvider();
            using var first = provider.CreateScope();
            using var second = provider.CreateScope();

            var context = first.ServiceProvider.GetRequiredService<CircuitUserContext>();

            Assert.Same(context, first.ServiceProvider.GetRequiredService<IUserContext>());
            Assert.NotSame(context, second.ServiceProvider.GetRequiredService<CircuitUserContext>());
        }

        [Fact]
        [DisplayName("PolhemLoginPanel records a successful sign-in on the circuit's context")]
        public async Task LoginPanel_SignIn_AppliesLoginResult()
        {
            var (context, client) = Create();
            var services = new ServiceCollection().AddSingleton(context).BuildServiceProvider();
            var panel = new PolhemLoginPanel();
            SetMember(panel, "Client", client);
            SetMember(panel, "Services", services);
            typeof(PolhemLoginPanel).GetField("_userId", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(panel, "demo");

            await (Task)typeof(PolhemLoginPanel)
                .GetMethod("OnSubmitAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(panel, null)!;

            Assert.Equal("demo", context.User?.UserId);
        }

        private static void SetMember(PolhemLoginPanel panel, string name, object value)
            => typeof(PolhemLoginPanel).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(panel, value);
    }
}
