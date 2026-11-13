namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// Clears the synchronization context for the length of a test, so the continuations of the dialogs' awaits run
    /// inline on the test's own thread when the test completes the dialog.
    /// </summary>
    /// <remarks>
    /// The dialogs await with <c>ConfigureAwait(true)</c>. Under xUnit's context those continuations are posted to
    /// another worker thread, and Avalonia controls refuse to be touched from a thread other than the one that created
    /// them. Without a context, completing the dialog runs the rest of it at once on the current thread, so a test
    /// stays synchronous: it starts the dialog, answers it, and reads the finished task.
    /// </remarks>
    internal sealed class InlineContinuationScope : IDisposable
    {
        private readonly SynchronizationContext? _previous = SynchronizationContext.Current;

        public InlineContinuationScope() => SynchronizationContext.SetSynchronizationContext(null);

        public void Dispose() => SynchronizationContext.SetSynchronizationContext(_previous);

        /// <summary>Returns the result of a dialog that the test has already answered.</summary>
        public static T Completed<T>(Task<T> task)
        {
            Assert.True(task.IsCompleted, "The dialog should have completed once it was answered.");
            return task.GetAwaiter().GetResult();
        }

        /// <summary>Checks that a dialog the test has already answered finished.</summary>
        public static void Completed(Task task)
        {
            Assert.True(task.IsCompleted, "The dialog should have completed once it was answered.");
            task.GetAwaiter().GetResult();
        }
    }
}
