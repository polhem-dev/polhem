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
    /// Methods such as OnInitializedAsync need the Blazor renderer and are left to bUnit integration tests.
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
        [DisplayName("The AccessToken property is marked with [CascadingParameter]")]
        public void AccessToken_HasCascadingParameterAttribute()
        {
            var property = GetPublicProperty(nameof(FormPage.AccessToken));
            Assert.NotNull(property.GetCustomAttribute<CascadingParameterAttribute>());
        }

        [Fact]
        [DisplayName("The private Factory property is marked with [Inject] and is of type PolhemApiConnectorFactory")]
        public void Factory_IsInjectedPolhemApiConnectorFactory()
        {
            var property = GetNonPublicProperty("Factory");
            Assert.NotNull(property.GetCustomAttribute<InjectAttribute>());
            Assert.Equal(typeof(PolhemApiConnectorFactory), property.PropertyType);
        }

        [Fact]
        [DisplayName("FormPage ProgId defaults to an empty string")]
        public void ProgId_Default_IsEmptyString()
        {
            var page = new FormPage();
            Assert.Equal(string.Empty, page.ProgId);
        }

        [Fact]
        [DisplayName("FormPage AccessToken defaults to Guid.Empty")]
        public void AccessToken_Default_IsGuidEmpty()
        {
            var page = new FormPage();
            Assert.Equal(Guid.Empty, page.AccessToken);
        }
    }
}
