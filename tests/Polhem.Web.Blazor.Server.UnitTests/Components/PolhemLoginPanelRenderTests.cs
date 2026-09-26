using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Server.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the Razor template <c>BuildRenderTree</c> of <see cref="PolhemLoginPanel"/>.
    /// The tests call <c>BuildRenderTree</c> directly without DI,
    /// covering the two conditional branches with and without an error message.
    /// </summary>
    public class PolhemLoginPanelRenderTests
    {
        private static readonly MethodInfo s_buildRenderTree =
            typeof(PolhemLoginPanel)
                .GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_errorField =
            typeof(PolhemLoginPanel).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static void Render(PolhemLoginPanel component)
        {
            var builder = new RenderTreeBuilder();
            s_buildRenderTree.Invoke(component, new object[] { builder });
        }

        [Fact]
        [DisplayName("BuildRenderTree does not throw when there is no error message")]
        public void BuildRenderTree_NoError_DoesNotThrow()
        {
            var component = new PolhemLoginPanel();
            var ex = Record.Exception(() => Render(component));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("BuildRenderTree renders the error block without throwing when there is an error message")]
        public void BuildRenderTree_WithError_DoesNotThrow()
        {
            var component = new PolhemLoginPanel();
            s_errorField.SetValue(component, "登入失敗：憑證無效");
            var ex = Record.Exception(() => Render(component));
            Assert.Null(ex);
        }
    }
}
