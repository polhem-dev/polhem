using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.Messages.System;
using Polhem.Web.Blazor.Server.Components;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Structural smoke tests for <see cref="PolhemLoginPanel"/>: confirms the
    /// public parameter surface, the <see cref="Polhem.Api.Client.PolhemApiClient"/>
    /// injection, and the labels' defaults. The submit paths are driven through a
    /// <see cref="FakeApiServer"/> by <see cref="PolhemLoginPanelSuccessPathTests"/>; submitting
    /// against a real backend is not covered by this project.
    /// </summary>
    public class PolhemLoginPanelTests
    {
        private static PropertyInfo GetProperty(string name)
        {
            var property = typeof(PolhemLoginPanel).GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            return property!;
        }

        private static PropertyInfo GetNonPublicProperty(string name)
        {
            var property = typeof(PolhemLoginPanel).GetProperty(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(property);
            return property!;
        }

        [Fact]
        [DisplayName("PolhemLoginPanel is a subclass of Blazor ComponentBase")]
        public void Type_IsComponentBaseSubclass()
        {
            Assert.True(typeof(ComponentBase).IsAssignableFrom(typeof(PolhemLoginPanel)));
        }

        [Theory]
        [InlineData(nameof(PolhemLoginPanel.UserIdLabel))]
        [InlineData(nameof(PolhemLoginPanel.PasswordLabel))]
        [InlineData(nameof(PolhemLoginPanel.SubmitLabel))]
        [InlineData(nameof(PolhemLoginPanel.OnLoggedIn))]
        [DisplayName("Public properties are all marked with [Parameter]")]
        public void PublicProperties_AreMarkedAsParameters(string name)
        {
            var property = GetProperty(name);
            Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
        }

        [Fact]
        [DisplayName("The OnLoggedIn property is of type EventCallback<LoginResponse>")]
        public void OnLoggedIn_IsEventCallbackOfLoginResponse()
        {
            var property = GetProperty(nameof(PolhemLoginPanel.OnLoggedIn));
            Assert.Equal(typeof(EventCallback<LoginResponse>), property.PropertyType);
        }

        [Fact]
        [DisplayName("The Client property is injected with PolhemApiClient through [Inject]")]
        public void Client_IsInjected()
        {
            var property = GetNonPublicProperty("Client");
            Assert.NotNull(property.GetCustomAttribute<InjectAttribute>());
            Assert.Equal(typeof(Polhem.Api.Client.PolhemApiClient), property.PropertyType);
        }

        [Fact]
        [DisplayName("The label properties default to null, so the panel shows its localized text unless the host overrides it")]
        public void LabelProperties_DefaultToNull()
        {
            var panel = new PolhemLoginPanel();
            Assert.Null(panel.UserIdLabel);
            Assert.Null(panel.PasswordLabel);
            Assert.Null(panel.SubmitLabel);
        }

        [Fact]
        [DisplayName("OnLoggedIn has no delegate by default (HasDelegate is false)")]
        public void OnLoggedIn_Default_HasNoDelegate()
        {
            var panel = new PolhemLoginPanel();
            Assert.False(panel.OnLoggedIn.HasDelegate);
        }
    }
}
