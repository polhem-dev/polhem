using System.ComponentModel;

namespace Avalonia.DemoCenter.Modules.PropertyEditing
{
    /// <summary>
    /// A group of <see cref="NestedCollectionsDemo"/>, holding entries of its own.
    /// </summary>
    public sealed class DemoGroup
    {
        [Category("Data")]
        [Description("The group's name.")]
        public string Name { get; set; } = string.Empty;

        [Category("Data")]
        [Description("The group's entries.")]
        public List<DemoEntry> Entries { get; } = [];

        /// <inheritdoc/>
        public override string ToString() => $"{Name} ({Entries.Count})";
    }
}
