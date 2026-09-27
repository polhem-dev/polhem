using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.Messages.System;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Structural smoke tests for <see cref="PolhemLoginPanel"/>: confirms the
    /// public parameter surface, the <see cref="PolhemApiConnectorFactory"/>
    /// injection, and the labels' defaults. Submitting against a real backend
    /// is exercised by the Phase 2 sample (BlazorHostApp), not by an in-memory
    /// unit test.
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
        [DisplayName("The Factory property is injected with PolhemApiConnectorFactory through [Inject]")]
        public void Factory_IsInjected()
        {
            var property = GetNonPublicProperty("Factory");
            Assert.NotNull(property.GetCustomAttribute<InjectAttribute>());
            Assert.Equal(typeof(PolhemApiConnectorFactory), property.PropertyType);
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
