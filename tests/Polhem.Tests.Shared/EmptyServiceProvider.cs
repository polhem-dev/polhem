namespace Polhem.Tests.Shared
{
    /// <summary>
    /// An <see cref="IServiceProvider"/> that resolves nothing, for tests that construct a local connector but never
    /// dispatch through it, or replace its provider before they do.
    /// </summary>
    /// <remarks>
    /// Using <see cref="TestProcessBootstrap.LocalServices"/> instead would bring up the whole process-wide backend for a
    /// test that only checks a constructor or an argument guard.
    /// </remarks>
    public sealed class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>
        /// The shared instance.
        /// </summary>
        public static readonly EmptyServiceProvider Instance = new();

        private EmptyServiceProvider()
        {
        }

        /// <inheritdoc/>
        public object? GetService(Type serviceType) => null;
    }
}
