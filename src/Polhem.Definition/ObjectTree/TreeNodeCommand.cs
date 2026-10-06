namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// A UI-independent command offered for a tree node, as returned by <see cref="ITreeNodeCommandProvider"/>.
    /// </summary>
    public sealed class TreeNodeCommand
    {
        private readonly Action _execute;

        /// <summary>
        /// Initializes a new instance of <see cref="TreeNodeCommand"/>.
        /// </summary>
        /// <param name="label">The text shown for the command, already localized.</param>
        /// <param name="execute">The action that runs when the command is chosen.</param>
        public TreeNodeCommand(string label, Action execute)
        {
            ArgumentNullException.ThrowIfNull(label);
            ArgumentNullException.ThrowIfNull(execute);
            Label = label;
            _execute = execute;
        }

        /// <summary>
        /// Gets the text shown for the command.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets or initializes the key of the icon shown with the command; the UI head resolves it, for example as a
        /// resource key. <c>null</c> shows no icon.
        /// </summary>
        public string? IconKey { get; init; }

        /// <summary>
        /// Gets or initializes whether the command can run now. A disabled command is shown but cannot be chosen.
        /// The default is <c>true</c>.
        /// </summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>
        /// Gets or initializes whether the command starts a new group, which a UI head shows as a separator before
        /// it. It has no effect on the first command. The default is <c>false</c>.
        /// </summary>
        public bool BeginsGroup { get; init; }

        /// <summary>
        /// Runs the command's action. It runs even when <see cref="IsEnabled"/> is <c>false</c>; respecting that flag
        /// is the UI head's job.
        /// </summary>
        public void Execute()
        {
            _execute();
        }
    }
}
