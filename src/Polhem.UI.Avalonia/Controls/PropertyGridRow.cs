using System.ComponentModel;
using Avalonia.Controls;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// One property shown by <see cref="PropertyGridControl"/>: its label, its editor, and how to load the editor from
    /// the current value.
    /// </summary>
    internal sealed class PropertyGridRow
    {
        internal PropertyGridRow(object component, PropertyDescriptor property, PropertyGridEditorKind kind,
            bool isReadOnly, Border labelHost, TextBlock label, Control editor)
        {
            Component = component;
            Property = property;
            Kind = kind;
            IsReadOnly = isReadOnly;
            LabelHost = labelHost;
            Label = label;
            Editor = editor;
        }

        /// <summary>Gets the object that owns the property.</summary>
        internal object Component { get; }

        /// <summary>Gets the property shown on this row.</summary>
        internal PropertyDescriptor Property { get; }

        /// <summary>Gets the kind of editor the row has.</summary>
        internal PropertyGridEditorKind Kind { get; }

        /// <summary>Gets whether the row's editor refuses edits.</summary>
        internal bool IsReadOnly { get; }

        /// <summary>Gets the cell behind the label, which shows the selection and takes the clicks.</summary>
        internal Border LabelHost { get; }

        /// <summary>Gets the label text.</summary>
        internal TextBlock Label { get; }

        /// <summary>Gets the editor.</summary>
        internal Control Editor { get; }

        /// <summary>Gets or sets the action that shows the property's current value in the editor.</summary>
        internal Action Load { get; set; } = static () => { };

        /// <summary>Gets or sets the text the editor showed when it was last loaded, so an unchanged text is not written.</summary>
        internal string LoadedText { get; set; } = string.Empty;
    }
}
