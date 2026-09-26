using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the private methods of <see cref="FormPage"/>.
    /// Scope: the three paths of <c>RunGuardedAsync</c> (success, exception, busy guard),
    /// and the early return of the four action handlers when <c>_dataObject</c> is null.
    /// Lifecycle tests that need the Blazor renderer or an API connector are left to bUnit integration tests.
    /// </summary>
    public class FormPageInternalTests
    {
        private static MethodInfo GetRunGuardedAsync()
        {
            var method = typeof(FormPage).GetMethod(
                "RunGuardedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return method!;
        }

        private static FieldInfo GetErrorField()
        {
            var field = typeof(FormPage).GetField(
                "_error", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return field!;
        }

        private static FieldInfo GetBusyField()
        {
            var field = typeof(FormPage).GetField(
                "_isBusy", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return field!;
        }

        // ──────────────────────────────────────────────────────────────
        // RunGuardedAsync
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("RunGuardedAsync restores _isBusy to false after a successful action")]
        public async Task RunGuardedAsync_SuccessfulAction_ExecutesAndResetsBusy()
        {
            var page = new FormPage();
            var method = GetRunGuardedAsync();
            var busyField = GetBusyField();
            bool executed = false;
            Func<Task> action = () =>
            {
                executed = true;
                return Task.CompletedTask;
            };
            var task = (Task)method.Invoke(page, new object[] { action })!;
            await task;
            Assert.True(executed);
            Assert.False((bool)busyField.GetValue(page)!);
        }

        [Fact]
        [DisplayName("RunGuardedAsync sets _error and restores _isBusy to false after an action that throws")]
        public async Task RunGuardedAsync_ThrowingAction_SetsErrorAndResetsBusy()
        {
            var page = new FormPage();
            var method = GetRunGuardedAsync();
            var errorField = GetErrorField();
            var busyField = GetBusyField();
            Func<Task> action = () => throw new InvalidOperationException("test error");
            var task = (Task)method.Invoke(page, new object[] { action })!;
            await task;
            Assert.Equal("test error", errorField.GetValue(page) as string);
            Assert.False((bool)busyField.GetValue(page)!);
        }

        [Fact]
        [DisplayName("RunGuardedAsync skips the action when called with _isBusy=true")]
        public async Task RunGuardedAsync_WhenBusy_SkipsAction()
        {
            var page = new FormPage();
            var method = GetRunGuardedAsync();
            var busyField = GetBusyField();
            busyField.SetValue(page, true);
            bool executed = false;
            Func<Task> action = () =>
            {
                executed = true;
                return Task.CompletedTask;
            };
            try
            {
                var task = (Task)method.Invoke(page, new object[] { action })!;
                await task;
                Assert.False(executed);
            }
            finally
            {
                busyField.SetValue(page, false);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Null `_dataObject` guard (the private action handlers return early).
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("OnRowSelectedAsync returns early without throwing when _dataObject is null")]
        public async Task OnRowSelectedAsync_NullDataObject_ReturnsWithoutError()
        {
            var page = new FormPage();
            var method = typeof(FormPage).GetMethod(
                "OnRowSelectedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, new object[] { Guid.NewGuid() })!;
            var exception = await Record.ExceptionAsync(() => task);
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("OnNewAsync returns early without throwing when _dataObject is null")]
        public async Task OnNewAsync_NullDataObject_ReturnsWithoutError()
        {
            var page = new FormPage();
            var method = typeof(FormPage).GetMethod(
                "OnNewAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            var exception = await Record.ExceptionAsync(() => task);
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("OnSaveAsync returns early without throwing when _dataObject is null")]
        public async Task OnSaveAsync_NullDataObject_ReturnsWithoutError()
        {
            var page = new FormPage();
            var method = typeof(FormPage).GetMethod(
                "OnSaveAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            var exception = await Record.ExceptionAsync(() => task);
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("OnDeleteAsync returns early without throwing when _dataObject is null")]
        public async Task OnDeleteAsync_NullDataObject_ReturnsWithoutError()
        {
            var page = new FormPage();
            var method = typeof(FormPage).GetMethod(
                "OnDeleteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            var exception = await Record.ExceptionAsync(() => task);
            Assert.Null(exception);
        }
    }
}
