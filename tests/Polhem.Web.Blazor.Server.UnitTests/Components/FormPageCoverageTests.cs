using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the early-return path of <see cref="FormPage.OnInitializedAsync"/>.
    /// An empty ProgId returns before Factory is accessed, so no Blazor renderer is needed.
    /// </summary>
    public class FormPageCoverageTests
    {
        private static FieldInfo GetErrorField() =>
            typeof(FormPage).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static FieldInfo GetIsInitializingField() =>
            typeof(FormPage).GetField("_isInitializing", BindingFlags.NonPublic | BindingFlags.Instance)!;

        [Fact]
        [DisplayName("OnInitializedAsync sets an error message when ProgId is an empty string")]
        public async Task OnInitializedAsync_EmptyProgId_SetsErrorMessage()
        {
            var page = new FormPage();
            var method = typeof(FormPage).GetMethod(
                "OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;
            var error = GetErrorField().GetValue(page) as string;
            Assert.Equal("FormPage.ProgId must be set.", error);
        }

        [Fact]
        [DisplayName("OnInitializedAsync sets _isInitializing to false when ProgId is an empty string")]
        public async Task OnInitializedAsync_EmptyProgId_SetsIsInitializingFalse()
        {
            var page = new FormPage();
            var method = typeof(FormPage).GetMethod(
                "OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;
            Assert.False((bool)GetIsInitializingField().GetValue(page)!);
        }

        [Fact]
        [DisplayName("OnInitializedAsync sets an error message when ProgId is whitespace")]
        public async Task OnInitializedAsync_WhitespaceProgId_SetsErrorMessage()
        {
#pragma warning disable BL0005
            var page = new FormPage { ProgId = "   " };
#pragma warning restore BL0005
            var method = typeof(FormPage).GetMethod(
                "OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;
            var error = GetErrorField().GetValue(page) as string;
            Assert.Equal("FormPage.ProgId must be set.", error);
        }
    }
}
