using System.ComponentModel;
using System.Reflection;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.UnitTests.Components
{
    /// <summary>
    /// Covers the guard every command of <see cref="FormPageBase"/> runs through. It is private and has no markup of its
    /// own, and a second call while one runs cannot be arranged from a rendered page without racing it.
    /// </summary>
    public class FormPageBaseGuardTests
    {
        private sealed class ProbePage : FormPageBase
        {
        }

        private static Task RunGuardedAsync(FormPageBase page, Func<Task> action)
            => (Task)typeof(FormPageBase).GetMethod("RunGuardedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(page, [action])!;

        private static object? Field(FormPageBase page, string name)
            => typeof(FormPageBase).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page);

        [Fact]
        [DisplayName("A guarded action runs, and the page stops being busy")]
        public async Task RunGuardedAsync_Success_ResetsBusy()
        {
            var page = new ProbePage();
            var executed = false;

            await RunGuardedAsync(page, () =>
            {
                executed = true;
                return Task.CompletedTask;
            });

            Assert.True(executed);
            Assert.False((bool)Field(page, "_isBusy")!);
        }

        [Fact]
        [DisplayName("A guarded action that throws becomes a notice, and the page stops being busy")]
        public async Task RunGuardedAsync_Throws_SetsNotice()
        {
            var page = new ProbePage();

            await RunGuardedAsync(page, () => throw new InvalidOperationException("test error"));

            Assert.Equal("test error", Field(page, "_notice"));
            Assert.False((bool)Field(page, "_isBusy")!);
        }

        [Fact]
        [DisplayName("A second command while one runs is skipped")]
        public async Task RunGuardedAsync_WhileBusy_Skips()
        {
            var page = new ProbePage();
            var release = new TaskCompletionSource();
            var first = RunGuardedAsync(page, () => release.Task);
            var second = false;

            await RunGuardedAsync(page, () =>
            {
                second = true;
                return Task.CompletedTask;
            });
            release.SetResult();
            await first;

            Assert.False(second);
        }
    }
}
