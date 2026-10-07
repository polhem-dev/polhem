namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The kind of editor <see cref="PropertyGridControl"/> builds for a property.
    /// </summary>
    internal enum PropertyGridEditorKind
    {
        /// <summary>A text box whose text goes through the property's type converter.</summary>
        Text,

        /// <summary>A check box for a <see cref="bool"/> property.</summary>
        Boolean,

        /// <summary>
        /// A drop-down whose text can also be typed, for a <see cref="string"/> property that
        /// <see cref="PropertyGridControl.ValueSuggestionProvider"/> offers values for.
        /// </summary>
        Suggestion,

        /// <summary>A drop-down of the enum members or of the converter's exclusive standard values.</summary>
        Choice,

        /// <summary>A numeric up-down for an integral or <see cref="decimal"/> property.</summary>
        Numeric,

        /// <summary>A date picker for a <see cref="DateTime"/> or <see cref="DateOnly"/> property.</summary>
        Date,

        /// <summary>A read-only summary of a collection's item count.</summary>
        Collection,

        /// <summary>A read-only summary of a value whose text cannot be converted back, such as a nested object.</summary>
        Summary,
    }
}
