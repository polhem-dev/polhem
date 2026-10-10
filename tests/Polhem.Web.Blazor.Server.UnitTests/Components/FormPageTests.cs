using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Structural smoke tests for <see cref="FormPage"/>.
    /// They confirm the compile-time declarations: public parameter properties, CascadingParameter, [Inject] injection and default values.
    /// The rendered behavior (load, row selection, New / Save / Delete, error display) is covered with bUnit by
    /// <see cref="FormPageBunitTests"/>.
    /// </summary>
    public class FormPageTests
    {
        private static PropertyInfo GetPublicProperty(string name)
        {
            var property = typeof(FormPage).GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            return property!;
        }

        private static PropertyInfo GetNonPublicProperty(string name)
        {
            var property = typeof(FormPage).GetProperty(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(property);
            return property!;
        }

        [Fact]
        [DisplayName("FormPage is a subclass of Blazor ComponentBase")]
        public void Type_IsComponentBaseSubclass()
        {
            Assert.True(typeof(ComponentBase).IsAssignableFrom(typeof(FormPage)));
        }

        [Fact]
        [DisplayName("The ProgId property is marked with both [Parameter] and [EditorRequired]")]
        public void ProgId_HasParameterAndEditorRequiredAttributes()
        {
            var property = GetPublicProperty(nameof(FormPage.ProgId));
            Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
            Assert.NotNull(property.GetCustomAttribute<EditorRequiredAttribute>());
        }

        [Fact]
        [DisplayName("The private Client property is marked with [Inject] and is of type PolhemApiClient")]
        public void Client_IsInjectedPolhemApiClient()
        {
            var property = GetNonPublicProperty("Client");
            Assert.NotNull(property.GetCustomAttribute<InjectAttribute>());
            Assert.Equal(typeof(Polhem.Api.Client.PolhemApiClient), property.PropertyType);
        }

        [Fact]
        [DisplayName("The private Options property is marked with [Inject] and is of type PolhemBlazorOptions")]
        public void Options_IsInjectedPolhemBlazorOptions()
        {
            var property = GetNonPublicProperty("Options");
            Assert.NotNull(property.GetCustomAttribute<InjectAttribute>());
            Assert.Equal(typeof(PolhemBlazorOptions), property.PropertyType);
        }

        [Fact]
        [DisplayName("FormPage ProgId defaults to an empty string")]
        public void ProgId_Default_IsEmptyString()
        {
            var page = new FormPage();
            Assert.Equal(string.Empty, page.ProgId);
        }
    }
}
