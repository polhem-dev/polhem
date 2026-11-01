using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Exceptions;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Identity;
using Polhem.Web.Blazor.Shell;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// Stands in for the backend of the shell: one company, one menu, and a language switch that records what it is
    /// asked for, for the shared shell tests and the browser tests.
    /// </summary>
    public sealed class ShellTestBackend
    {
        public const string Company = "ACME";

        /// <summary>
        /// Initializes a new instance of <see cref="ShellTestBackend"/>.
        /// </summary>
        public ShellTestBackend()
        {
            Server = new FakeApiServer()
                .On<LoginRequest>($"{SysProgIds.System}.{SystemActions.Login}", request =>
                {
                    LastLogin = (request.UserId, request.Password);
                    return new LoginResponse { AccessToken = Guid.NewGuid(), UserId = request.UserId, UserName = "Demo User" };
                })
                .On<EnterCompanyRequest>($"{SysProgIds.System}.{SystemActions.EnterCompany}", request =>
                {
                    EnterCompanyCount++;
                    return request.CompanyId == Company
                        ? new EnterCompanyResponse { Company = new CompanyInfo { CompanyId = Company, CompanyName = "Acme Corp" } }
                        : throw new UserMessageException("Company access denied");
                })
                .On<GetDefineRequest>($"{SysProgIds.System}.{SystemActions.GetDefine}",
                    _ => new GetDefineResponse { Xml = XmlCodec.Serialize(BuildMenu()) });
        }

        public FakeApiServer Server { get; }

        public (string UserId, string Password)? LastLogin { get; private set; }

        public int EnterCompanyCount { get; private set; }

        public List<string> SwitchedTo { get; } = [];

        /// <summary>Registers what the shell needs, with its client calling this backend.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Changes the options.</param>
        /// <param name="withSwitcher">Whether the host offers a language switch.</param>
        public void Register(IServiceCollection services, Action<PolhemShellOptions>? configure = null, bool withSwitcher = true)
        {
            var options = new PolhemShellOptions
            {
                AppTitle = "Acme App",
                SignInHint = "Use demo / demo.",
                DefaultCompanyId = Company,
                DefaultUserId = "demo",
            };
            configure?.Invoke(options);
            services.AddSingleton(options);
            services.AddSingleton<IBlazorDefinitionOptions>(new DefinitionOptions());
            services.AddScoped(_ => Server.CreateClient());
            services.AddScoped(sp => new BlazorUserContext(sp.GetRequiredService<Polhem.Api.Client.PolhemApiClient>(), string.Empty));
            services.AddScoped<IUserContext>(sp => sp.GetRequiredService<BlazorUserContext>());
            if (withSwitcher) services.AddSingleton<IShellLanguageSwitcher>(new Switcher(SwitchedTo));
        }

        private static MenuSettings BuildMenu()
        {
            var settings = new MenuSettings();
            var folder = new MenuFolder("sales", "Sales");
            folder.Items!.Add(new MenuEntry("order", "Order", "Orders"));
            settings.Items!.Add(folder);
            return settings;
        }

        private sealed class DefinitionOptions : IBlazorDefinitionOptions
        {
            public string DefaultLanguage => string.Empty;

            public bool UseDefinitionLoader => false;
        }

        private sealed class Switcher(List<string> switchedTo) : IShellLanguageSwitcher
        {
            public void Switch(PolhemShellLanguage language) => switchedTo.Add(language.Culture);
        }
    }
}
