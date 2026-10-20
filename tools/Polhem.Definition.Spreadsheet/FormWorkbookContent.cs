using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Everything one form workbook describes: the form itself, its zh-TW captions and where it sits in the menu.
/// </summary>
public sealed class FormWorkbookContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormWorkbookContent"/> class.
    /// </summary>
    /// <param name="schema">The form definition.</param>
    public FormWorkbookContent(FormSchema schema)
    {
        Schema = schema;
    }

    /// <summary>Gets the form definition.</summary>
    public FormSchema Schema { get; }

    /// <summary>
    /// Gets the zh-TW texts, keyed by the sub-keys of <see cref="Polhem.Definition.Language.FormSchemaLocalizer"/>
    /// (<c>Schema.DisplayName</c>, <c>Table.Order.DisplayName</c>, <c>Field.order_date.Caption</c>,
    /// <c>Rule.customer_required.Message</c>).
    /// </summary>
    public IDictionary<string, string> ZhTw { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets where the form appears in the menu.</summary>
    public MenuPlacement Menu { get; } = new();

    /// <summary>
    /// Gets the workbook row each field was read from, keyed by <c>TableName.FieldName</c>
    /// (for example <c>Fields!12</c>). Empty for content that was not read from a workbook.
    /// </summary>
    public IDictionary<string, string> FieldSources { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
