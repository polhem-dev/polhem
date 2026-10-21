namespace Polhem.UI.Avalonia.Shell.Navigation
{
    /// <summary>
    /// One row of the navigation menu: a non-selectable folder header (<see cref="IsHeader"/>) or a form link
    /// carrying the <see cref="ProgId"/> the forms screen opens.
    /// </summary>
    internal sealed class NavItem
    {
        /// <summary>Gets the text shown in the menu.</summary>
        public required string Title { get; init; }

        /// <summary>Gets the program id opened when the row is selected; empty for a header.</summary>
        public string ProgId { get; init; } = string.Empty;

        /// <summary>Gets whether the row is a folder header.</summary>
        public bool IsHeader { get; init; }

        /// <summary>Creates a header row.</summary>
        public static NavItem Header(string title) => new() { Title = title, IsHeader = true };

        /// <summary>Creates a form link.</summary>
        public static NavItem Form(string title, string progId) => new() { Title = title, ProgId = progId };
    }
}
