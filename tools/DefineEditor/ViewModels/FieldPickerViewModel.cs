using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Picks and orders fields for a property that names them as a comma-separated list, such as
/// <see cref="Polhem.Definition.Forms.FormSchema.ListFields"/>.
/// </summary>
/// <remarks>
/// The list starts with the fields already named, in their order, then the other fields in table order. A name that
/// matches no field is kept, ticked, so opening the picker and confirming never drops part of the value.
/// </remarks>
public sealed partial class FieldPickerViewModel : ObservableObject
{
    /// <param name="propertyName">The property being edited, shown in the title.</param>
    /// <param name="fields">The fields to choose from, in table order.</param>
    /// <param name="currentValue">The property's value: field names separated by commas.</param>
    public FieldPickerViewModel(string propertyName, IEnumerable<(string Name, string Caption)> fields, string currentValue)
    {
        PropertyName = propertyName;
        // Keyed case-insensitively like the definitions, so a name written in other casing still finds its field.
        var byName = new Dictionary<string, (string Name, string Caption)>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var (name, caption) in fields)
        {
            if (!string.IsNullOrEmpty(name) && byName.TryAdd(name, (name, caption)))
                order.Add(name);
        }

        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in currentValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!named.Add(name)) continue;
            var (fieldName, caption) = byName.TryGetValue(name, out var field) ? field : (name, string.Empty);
            Items.Add(new FieldPickerItem(fieldName, caption) { IsChecked = true });
        }
        foreach (var name in order.Where(n => !named.Contains(n)))
            Items.Add(new FieldPickerItem(name, byName[name].Caption));
    }

    /// <summary>The property being edited.</summary>
    public string PropertyName { get; }

    /// <summary>The fields; the ticked ones are written in this order.</summary>
    public ObservableCollection<FieldPickerItem> Items { get; } = [];

    /// <summary>The field the move buttons act on.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand), nameof(MoveDownCommand))]
    private FieldPickerItem? _selectedItem;

    /// <summary>The new value: the ticked field names, in list order, separated by commas.</summary>
    public string Result => string.Join(",", Items.Where(i => i.IsChecked).Select(i => i.Name));

    /// <summary>Moves the selected field one place up.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    private bool CanMoveUp() => SelectedItem is { } item && Items.IndexOf(item) > 0;

    /// <summary>Moves the selected field one place down.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private bool CanMoveDown() => SelectedItem is { } item && Items.IndexOf(item) is var index && index >= 0 && index < Items.Count - 1;

    private void Move(int step)
    {
        if (SelectedItem is not { } item) return;
        var index = Items.IndexOf(item);
        Items.Move(index, index + step);
        // The list may clear its selection while the item moves; select it again.
        SelectedItem = item;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}
