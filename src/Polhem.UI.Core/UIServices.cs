namespace Polhem.UI.Core
{
    /// <summary>
    /// The service provider that controls created from markup resolve their services from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A control created by a XAML loader cannot take constructor arguments, so a single-user head (desktop,
    /// mobile, browser WebAssembly) sets <see cref="Provider"/> once at startup and the controls read their
    /// connector factory, definition source and user context from it. When it is unset, or a service is not
    /// registered, a control works without a backend.
    /// </para>
    /// <para>
    /// WARNING: this is process-wide state. A host that serves several users from one process must not set it;
    /// its components receive scoped services through their own injection instead.
    /// <c>UIServicesUsageGateTests</c> in <c>tests/Polhem.Web.Blazor.Server.UnitTests</c> fails when the Blazor
    /// head references this type.
    /// </para>
    /// </remarks>
    public static class UIServices
    {
        /// <summary>
        /// Gets or sets the provider controls resolve their services from; <c>null</c> runs them without a backend.
        /// </summary>
        public static IServiceProvider? Provider { get; set; }

        /// <summary>
        /// Resolves a service from <see cref="Provider"/>.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The service, or <c>null</c> when no provider is set or the service is not registered.</returns>
        public static T? Get<T>() where T : class
            => Provider?.GetService(typeof(T)) as T;
    }
}
