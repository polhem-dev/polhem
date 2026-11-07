using CommunityToolkit.Mvvm.ComponentModel;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A field in <see cref="FieldPickerViewModel"/>, ticked when the value names it.</summary>
public sealed partial class FieldPickerItem(string name, string caption) : ObservableObject
{
    /// <summary>The field name, as the value writes it.</summary>
    public string Name { get; } = name;

    /// <summary>The field's caption; empty for a name that matches no field.</summary>
    public string Caption { get; } = caption;

    /// <summary>Whether the value names this field.</summary>
    [ObservableProperty]
    private bool _isChecked;
}
