using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// The values on the Form sheet: the schema's own attributes, its zh-TW display name and its menu placement.
/// </summary>
internal sealed class FormSheetRow(FormSchema schema, MenuPlacement menu)
{
    public FormSchema Schema { get; } = schema;

    public MenuPlacement Menu { get; } = menu;

    public string ZhDisplayName { get; set; } = string.Empty;
}
