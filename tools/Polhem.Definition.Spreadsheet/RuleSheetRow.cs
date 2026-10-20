using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// One row of the Rules sheet.
/// </summary>
internal sealed class RuleSheetRow(FormRule rule)
{
    public FormRule Rule { get; } = rule;

    public string ZhMessage { get; set; } = string.Empty;
}
