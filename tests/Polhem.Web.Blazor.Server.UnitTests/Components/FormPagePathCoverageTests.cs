using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers <see cref="FormPage.OnInitializedAsync"/> with a valid ProgId.
    /// When Factory is not injected (null), a NullReferenceException is thrown inside the try block,
    /// the catch sets _error and the finally resets _isInitializing,
    /// so all three branches of try-catch-finally are covered without a Blazor renderer.
    /// </summary>
    public class FormPagePathCoverageTests
    {
        private static FieldInfo GetErrorField() =>
            typeof(FormPage).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static FieldInfo GetIsInitializingField() =>
            typeof(FormPage).GetField("_isInitializing", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static async Task InvokeOnInitializedAsync(FormPage page)
        {
            var method = typeof(FormPage).GetMethod(
                "OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;
        }

        private static FormPage CreatePageWithProgId(string progId)
        {
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId",
                BindingFlags.Public | BindingFlags.Instance)!.SetValue(page, progId);
            return page;
        }

        [Fact]
        [DisplayName("OnInitializedAsync enters the catch and sets an error message for a valid ProgId when Factory is not injected")]
        public async Task OnInitializedAsync_ValidProgIdNullFactory_CatchesExceptionAndSetsError()
        {
            var page = CreatePageWithProgId("TestProg");
            await InvokeOnInitializedAsync(page);
            var error = GetErrorField().GetValue(page) as string;
            Assert.NotNull(error);
        }

        [Fact]
        [DisplayName("OnInitializedAsync sets _isInitializing to false in finally for a valid ProgId when Factory is not injected")]
        public async Task OnInitializedAsync_ValidProgIdNullFactory_FinallyResetsIsInitializing()
        {
            var page = CreatePageWithProgId("TestProg");
            await InvokeOnInitializedAsync(page);
            Assert.False((bool)GetIsInitializingField().GetValue(page)!);
        }
    }
}
