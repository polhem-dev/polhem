namespace Polhem.Base
{
    /// <summary>
    /// Recognizes type and assembly names left over from Bee.NET, the framework Polhem continues
    /// under a new name, so an error about a name that cannot be resolved can say why.
    /// </summary>
    /// <remarks>
    /// Every <c>Bee.*</c> package, namespace and type was renamed to <c>Polhem.*</c>. A definition
    /// file, a settings file or a client carried over from Bee.NET still names the old ones, and the
    /// failure is otherwise a bare "type not found" that does not point at the rename.
    /// </remarks>
    public static class BeeNameHint
    {
        private const string BeePrefix = "Bee.";

        /// <summary>
        /// Returns whether a type name, an assembly-qualified type name or an assembly name refers to
        /// a Bee.NET type or assembly.
        /// </summary>
        /// <param name="name">The name to inspect.</param>
        public static bool Matches(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) { return false; }

            foreach (var part in name.Split([',', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith(BeePrefix, StringComparison.Ordinal)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Appends the rename hint to <paramref name="message"/> when <paramref name="name"/> refers to
        /// a Bee.NET type or assembly; otherwise returns <paramref name="message"/> unchanged.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="name">The type or assembly name the message is about.</param>
        public static string AppendTo(string message, string? name)
        {
            if (!Matches(name)) { return message; }

            return message + " '" + name + "' looks like a Bee.NET name: Bee.* packages, namespaces and types " +
                "were renamed to Polhem.*. See \"Migrating from Bee.NET\" in the Polhem README.";
        }
    }
}
