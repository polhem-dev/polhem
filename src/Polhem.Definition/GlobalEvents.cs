namespace Polhem.Definition
{
    /// <summary>
    /// Global events used for cross-project notifications.
    /// </summary>
    public static class GlobalEvents
    {
        /// <summary>
        /// Occurs when the database settings have changed.
        /// </summary>
        public static event EventHandler? DatabaseSettingsChanged;

        /// <summary>
        /// Raises the <see cref="DatabaseSettingsChanged"/> event.
        /// </summary>
        /// <remarks>
        /// Public because the raiser lives outside this assembly and is replaceable: the framework's
        /// <c>DatabaseSettingsCache</c> in <c>Polhem.ObjectCaching</c> raises it when an edited
        /// <c>DatabaseSettings.xml</c> is reloaded, and a host that plugs in its own
        /// <see cref="Storage.IDefineAccess"/> through <see cref="Settings.BackendComponents.DefineAccess"/> must raise it
        /// when its settings change, or the connection cache of <c>DbConnectionManagerService</c> keeps
        /// the old connections. Raising it is always safe: the only effect in the framework is that
        /// the connection cache is rebuilt on its next use.
        /// </remarks>
        public static void RaiseDatabaseSettingsChanged()
        {
            DatabaseSettingsChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
