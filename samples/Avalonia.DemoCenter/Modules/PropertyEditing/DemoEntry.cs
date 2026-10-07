using System.ComponentModel;

namespace Avalonia.DemoCenter.Modules.PropertyEditing
{
    /// <summary>
    /// An entry of a <see cref="DemoGroup"/>.
    /// </summary>
    public sealed class DemoEntry
    {
        [Category("Data")]
        [Description("The entry's name.")]
        public string Name { get; set; } = string.Empty;

        [Category("Data")]
        [Description("The entry's value.")]
        public string Value { get; set; } = string.Empty;

        /// <inheritdoc/>
        public override string ToString() => $"{Name} = {Value}";
    }
}
