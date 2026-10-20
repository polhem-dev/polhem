using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// One row of the Tables sheet.
/// </summary>
internal sealed class TableSheetRow(FormTable table)
{
    public FormTable Table { get; } = table;

    public string ZhDisplayName { get; set; } = string.Empty;
}
