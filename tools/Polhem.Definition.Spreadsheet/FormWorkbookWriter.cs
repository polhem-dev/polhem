using System.Globalization;
using MiniExcelLibs;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Writes a <see cref="FormWorkbookContent"/> into a copy of the form workbook template.
/// </summary>
/// <remarks>
/// The template carries the layout, the drop-downs and the instructions; this class only fills in the values.
/// Values are passed to MiniExcel as dictionaries, never as objects, because MiniExcel reads object
/// properties by reflection, which trimming and NativeAOT break.
/// </remarks>
public static class FormWorkbookWriter
{
    /// <summary>
    /// Writes the workbook for <paramref name="content"/>.
    /// </summary>
    /// <param name="content">The form to write.</param>
    /// <param name="output">The stream that receives the <c>.xlsx</c> bytes.</param>
    /// <exception cref="InvalidOperationException">A list item or mapping contains a character the cell format reserves.</exception>
    public static void Write(FormWorkbookContent content, Stream output)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(output);
        output.SaveAsByTemplate(FormWorkbookTemplate.GetBytes(), BuildValues(content));
    }

    /// <summary>
    /// Writes an empty workbook for describing a new form.
    /// </summary>
    /// <param name="output">The stream that receives the <c>.xlsx</c> bytes.</param>
    public static void WriteBlank(Stream output)
        => Write(new FormWorkbookContent(new FormSchema()), output);

    internal static Dictionary<string, object> BuildValues(FormWorkbookContent content)
    {
        var schema = content.Schema;
        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        var formRow = new FormSheetRow(schema, content.Menu)
        {
            ZhDisplayName = Translation(content, FormSchemaLocalizer.SchemaDisplayNameKey),
        };
        foreach (var item in FormWorkbookCatalog.FormItems)
            values[item.Key] = item.Export(formRow);

        var tables = new List<Dictionary<string, object>>();
        var fields = new List<Dictionary<string, object>>();
        foreach (var table in schema.Tables!)
        {
            tables.Add(Row(FormWorkbookCatalog.TableColumns, new TableSheetRow(table)
            {
                ZhDisplayName = Translation(content, Key(FormSchemaLocalizer.TableDisplayNameKeyFormat, table.TableName)),
            }));

            // Fields of the master table leave the table column empty, which is how a person would fill it in.
            var tableName = string.Equals(table.TableName, schema.ProgId, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : table.TableName;
            foreach (var field in table.Fields!)
            {
                fields.Add(Row(FormWorkbookCatalog.FieldColumns, new FieldSheetRow(field)
                {
                    TableName = tableName,
                    ZhCaption = Translation(content, Key(FormSchemaLocalizer.FieldCaptionKeyFormat, field.FieldName)),
                }));
            }
        }

        var rules = new List<Dictionary<string, object>>();
        foreach (var rule in schema.Rules!)
        {
            rules.Add(Row(FormWorkbookCatalog.RuleColumns, new RuleSheetRow(rule)
            {
                ZhMessage = Translation(content, Key(FormSchemaLocalizer.RuleMessageKeyFormat, rule.RuleId)),
            }));
        }

        values[FormWorkbookCatalog.TablesSheet] = tables;
        values[FormWorkbookCatalog.FieldsSheet] = fields;
        values[FormWorkbookCatalog.RulesSheet] = rules;
        return values;
    }

    internal static string Key(string format, string name)
        => string.Format(CultureInfo.InvariantCulture, format, name);

    private static Dictionary<string, object> Row<TRow>(IEnumerable<SheetColumn<TRow>> columns, TRow row)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var column in columns)
            values[column.Key] = column.Export(row);
        return values;
    }

    private static string Translation(FormWorkbookContent content, string key)
        => content.ZhTw.TryGetValue(key, out var text) ? text : string.Empty;
}
