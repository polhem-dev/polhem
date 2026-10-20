namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// A field editor that exposes its <see cref="FieldEditorBinder"/> to the rest of this assembly, so a host
    /// can attach layout metadata without binding data.
    /// </summary>
    internal interface IFieldEditorBinderHost
    {
        /// <summary>Gets the editor's binder.</summary>
        FieldEditorBinder Binder { get; }
    }
}
