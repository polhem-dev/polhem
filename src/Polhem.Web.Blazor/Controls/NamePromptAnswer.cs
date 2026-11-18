namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// What the user accepted in a name prompt: the name, trimmed, and whether the option under it was checked.
    /// </summary>
    public sealed class NamePromptAnswer
    {
        /// <summary>Initializes a new instance of <see cref="NamePromptAnswer"/>.</summary>
        /// <param name="name">The name, trimmed and not empty.</param>
        /// <param name="isOptionChecked">Whether the option was checked; <see langword="false"/> when the prompt offered none.</param>
        public NamePromptAnswer(string name, bool isOptionChecked)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            Name = name;
            IsOptionChecked = isOptionChecked;
        }

        /// <summary>Gets the name, trimmed.</summary>
        public string Name { get; }

        /// <summary>Gets a value indicating whether the option was checked.</summary>
        public bool IsOptionChecked { get; }
    }
}
