using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the two paths of <c>PolhemLoginPanel.OnSubmitAsync</c> (private, so not a cref) that can run without DI:
    /// 1. The _isBusy guard (returns directly without touching Factory).
    /// 2. The try-catch-finally path when Factory is not injected (the NRE is caught).
    /// </summary>
    public class PolhemLoginPanelInternalTests
    {
        private static FieldInfo GetBusyField() =>
            typeof(LoginPanelBase).GetField("_isBusy", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static FieldInfo GetErrorField() =>
            typeof(LoginPanelBase).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static async Task InvokeOnSubmitAsync(PolhemLoginPanel panel)
        {
            var method = typeof(PolhemLoginPanel).GetMethod(
                "OnSubmitAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(panel, [CancellationToken.None])!;
            await task;
        }

        [Fact]
        [DisplayName("OnSubmitAsync returns directly without calling Factory or throwing when _isBusy is true")]
        public async Task OnSubmitAsync_WhenBusy_ReturnsImmediatelyWithoutError()
        {
            var panel = new PolhemLoginPanel();
            var busyField = GetBusyField();
            busyField.SetValue(panel, true);
            var exception = await Record.ExceptionAsync(() => InvokeOnSubmitAsync(panel));
            Assert.Null(exception);
            Assert.True((bool)busyField.GetValue(panel)!);
        }

        [Fact]
        [DisplayName("OnSubmitAsync enters the catch, sets an error message and restores _isBusy to false when Factory is not injected")]
        public async Task OnSubmitAsync_NullFactory_CatchesExceptionAndResetsBusy()
        {
            var panel = new PolhemLoginPanel();
            await InvokeOnSubmitAsync(panel);
            Assert.False((bool)GetBusyField().GetValue(panel)!);
            Assert.NotNull(GetErrorField().GetValue(panel) as string);
        }
    }
}
