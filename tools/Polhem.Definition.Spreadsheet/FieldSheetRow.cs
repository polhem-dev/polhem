using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// One row of the Fields sheet. <see cref="TableName"/> says which table the field belongs to; empty means
/// the master table.
/// </summary>
internal sealed class FieldSheetRow(FormField field)
{
    public string TableName { get; set; } = string.Empty;

    public FormField Field { get; } = field;

    public string ZhCaption { get; set; } = string.Empty;
}
