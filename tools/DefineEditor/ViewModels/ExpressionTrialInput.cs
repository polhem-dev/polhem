using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Data;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A sample value for one field the expression reads, typed in the try-out area.</summary>
public sealed partial class ExpressionTrialInput : ObservableObject
{
    /// <param name="fieldName">The field's declared name, as the expression names it.</param>
    /// <param name="typeName">The type the value is read as, shown beside it.</param>
    /// <param name="dbType">The field's database type, which decides how the text is read.</param>
    public ExpressionTrialInput(string fieldName, string typeName, FieldDbType dbType)
    {
        FieldName = fieldName;
        TypeName = typeName;
        DbType = dbType;
    }

    /// <summary>The field's declared name.</summary>
    public string FieldName { get; }

    /// <summary>The type the value is read as.</summary>
    public string TypeName { get; }

    /// <summary>The field's database type.</summary>
    public FieldDbType DbType { get; }

    /// <summary>The value as typed; empty means an empty cell.</summary>
    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>Why <see cref="Text"/> cannot be read as the field's type; empty when it can.</summary>
    [ObservableProperty]
    private string _error = string.Empty;

    /// <summary>Whether <see cref="Error"/> has a message to show.</summary>
    public bool HasError => Error.Length > 0;

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
}
