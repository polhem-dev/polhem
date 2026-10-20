using MiniExcelLibs;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Reads a form workbook back into a <see cref="FormWorkbookContent"/>.
/// </summary>
/// <remarks>
/// Columns are found by the keys in the hidden key row, not by position, so a person may reorder columns,
/// reword the headers or add columns of their own (a column without a key is ignored). The reader applies
/// no naming conventions; <see cref="FormSchemaConventions"/> does that.
/// <para>
/// Rows are read through the non-generic <c>Query</c> and cast to dictionaries, never through
/// <c>Query&lt;T&gt;</c> or <c>dynamic</c>: both rely on reflection or runtime code generation that trimming
/// and NativeAOT break.
/// </para>
/// </remarks>
public static class FormWorkbookReader
{
    /// <summary>
    /// Reads a workbook.
    /// </summary>
    /// <param name="input">The <c>.xlsx</c> bytes.</param>
    /// <returns>The content, or the problems that kept it from being read.</returns>
    public static FormWorkbookReadResult Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);

        var issues = new List<DefinitionIssue>();
        var sheets = buffer.GetSheetNames();
        foreach (var required in new[] { FormWorkbookCatalog.FormSheet, FormWorkbookCatalog.FieldsSheet })
        {
            if (!sheets.Contains(required, StringComparer.Ordinal))
                issues.Add(Error(required, $"The workbook has no '{required}' sheet. Start from a workbook the tool exported."));
        }
        if (issues.Count > 0)
            return new FormWorkbookReadResult(null, issues);

        var schema = new FormSchema();
        var content = new FormWorkbookContent(schema);
        var zh = new Dictionary<string, string>(StringComparer.Ordinal);

        var formRow = new FormSheetRow(schema, content.Menu);
        ReadFormSheet(buffer, formRow, issues);
        Translate(zh, FormSchemaLocalizer.SchemaDisplayNameKey, formRow.ZhDisplayName);

        ReadTables(buffer, sheets, schema, zh, issues);
        ReadFields(buffer, content, zh, issues);
        ReadRules(buffer, sheets, schema, zh, issues);

        foreach (var (key, text) in zh)
            content.ZhTw[key] = text;
        return new FormWorkbookReadResult(content, issues);
    }

    private static void ReadFormSheet(MemoryStream buffer, FormSheetRow formRow, List<DefinitionIssue> issues)
    {
        var items = FormWorkbookCatalog.FormItems.ToDictionary(c => c.Key, StringComparer.Ordinal);
        int rowNumber = 0;
        foreach (var row in Rows(buffer, FormWorkbookCatalog.FormSheet, "A1"))
        {
            rowNumber++;
            var key = CellText.From(Cell(row, "B"));
            if (!items.TryGetValue(key, out var item)) continue;
            var error = item.Import(formRow, CellText.From(Cell(row, "C")));
            if (error is not null)
                issues.Add(Error($"{FormWorkbookCatalog.FormSheet}!C{rowNumber}", error));
        }
    }

    private static void ReadTables(
        MemoryStream buffer, IReadOnlyCollection<string> sheets, FormSchema schema,
        Dictionary<string, string> zh, List<DefinitionIssue> issues)
    {
        if (sheets.Contains(FormWorkbookCatalog.TablesSheet, StringComparer.Ordinal))
        {
            foreach (var (rowNumber, row) in TabularRows(buffer, FormWorkbookCatalog.TablesSheet, FormWorkbookCatalog.TableColumns, issues))
            {
                var tableRow = new TableSheetRow(new FormTable());
                Apply(FormWorkbookCatalog.TablesSheet, rowNumber, row, FormWorkbookCatalog.TableColumns, tableRow, issues);
                var table = tableRow.Table;
                if (table.TableName.Length == 0)
                {
                    if (schema.Tables!.Count > 0)
                    {
                        issues.Add(Error(RowLocation(FormWorkbookCatalog.TablesSheet, rowNumber),
                            "Only the first row (the master table) may leave the table name empty."));
                        continue;
                    }
                    table.TableName = schema.ProgId;
                }
                if (schema.Tables!.Contains(table.TableName))
                {
                    issues.Add(Error(RowLocation(FormWorkbookCatalog.TablesSheet, rowNumber),
                        $"The table '{table.TableName}' appears more than once."));
                    continue;
                }
                schema.Tables.Add(table);
                Translate(zh, FormWorkbookWriter.Key(FormSchemaLocalizer.TableDisplayNameKeyFormat, table.TableName), tableRow.ZhDisplayName);
            }
        }

        // A form needs a master table to hold its fields; a workbook that lists none means the obvious one.
        if (schema.Tables!.Count == 0)
            schema.Tables.Add(new FormTable(schema.ProgId, schema.DisplayName));
    }

    private static void ReadFields(
        MemoryStream buffer, FormWorkbookContent content, Dictionary<string, string> zh, List<DefinitionIssue> issues)
    {
        var schema = content.Schema;
        var masterName = schema.Tables![0].TableName;
        foreach (var (rowNumber, row) in TabularRows(buffer, FormWorkbookCatalog.FieldsSheet, FormWorkbookCatalog.FieldColumns, issues))
        {
            var location = RowLocation(FormWorkbookCatalog.FieldsSheet, rowNumber);
            var fieldRow = new FieldSheetRow(new FormField());
            Apply(FormWorkbookCatalog.FieldsSheet, rowNumber, row, FormWorkbookCatalog.FieldColumns, fieldRow, issues);
            var field = fieldRow.Field;

            var tableName = fieldRow.TableName.Length == 0 ? masterName : fieldRow.TableName;
            if (!schema.Tables.Contains(tableName))
            {
                issues.Add(Error(location, $"The table '{tableName}' is not listed on the {FormWorkbookCatalog.TablesSheet} sheet."));
                continue;
            }
            if (field.FieldName.Length == 0)
            {
                issues.Add(Error(location, "The field name is empty."));
                continue;
            }
            var table = schema.Tables[tableName];
            if (table.Fields!.Contains(field.FieldName))
            {
                issues.Add(Error(location, $"The field '{field.FieldName}' appears more than once in '{tableName}'."));
                continue;
            }
            table.Fields.Add(field);
            content.FieldSources[$"{table.TableName}.{field.FieldName}"] = location;
            Translate(zh, FormWorkbookWriter.Key(FormSchemaLocalizer.FieldCaptionKeyFormat, field.FieldName), fieldRow.ZhCaption);
        }
    }

    private static void ReadRules(
        MemoryStream buffer, IReadOnlyCollection<string> sheets, FormSchema schema,
        Dictionary<string, string> zh, List<DefinitionIssue> issues)
    {
        if (!sheets.Contains(FormWorkbookCatalog.RulesSheet, StringComparer.Ordinal)) return;
        foreach (var (rowNumber, row) in TabularRows(buffer, FormWorkbookCatalog.RulesSheet, FormWorkbookCatalog.RuleColumns, issues))
        {
            var location = RowLocation(FormWorkbookCatalog.RulesSheet, rowNumber);
            var ruleRow = new RuleSheetRow(new FormRule());
            Apply(FormWorkbookCatalog.RulesSheet, rowNumber, row, FormWorkbookCatalog.RuleColumns, ruleRow, issues);
            var rule = ruleRow.Rule;
            if (rule.RuleId.Length == 0)
            {
                issues.Add(Error(location, "The rule id is empty."));
                continue;
            }
            if (schema.Rules!.Contains(rule.RuleId))
            {
                issues.Add(Error(location, $"The rule '{rule.RuleId}' appears more than once."));
                continue;
            }
            schema.Rules.Add(rule);
            Translate(zh, FormWorkbookWriter.Key(FormSchemaLocalizer.RuleMessageKeyFormat, rule.RuleId), ruleRow.ZhMessage);
        }
    }

    private static void Apply<TRow>(
        string sheet, int rowNumber, Dictionary<string, (string Text, string Column)> row,
        IEnumerable<SheetColumn<TRow>> columns, TRow target, List<DefinitionIssue> issues)
    {
        foreach (var column in columns)
        {
            var (text, letter) = row.TryGetValue(column.Key, out var cell) ? cell : (string.Empty, string.Empty);
            var error = column.Import(target, text);
            if (error is not null)
                issues.Add(Error($"{sheet}!{letter}{rowNumber}", error));
        }
    }

    /// <summary>
    /// Yields the non-blank data rows of a tabular sheet, each as key → (text, column letter).
    /// </summary>
    private static IEnumerable<(int RowNumber, Dictionary<string, (string Text, string Column)> Row)> TabularRows<TRow>(
        MemoryStream buffer, string sheet, IReadOnlyList<SheetColumn<TRow>> columns, List<DefinitionIssue> issues)
    {
        var known = columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string>? keyByLetter = null;
        int rowNumber = FormWorkbookCatalog.KeyRow - 1;
        foreach (var row in Rows(buffer, sheet, $"A{FormWorkbookCatalog.KeyRow}"))
        {
            rowNumber++;
            if (keyByLetter is null)
            {
                keyByLetter = row
                    .Select(cell => (Letter: cell.Key, Key: CellText.From(cell.Value)))
                    .Where(cell => known.Contains(cell.Key))
                    .ToDictionary(cell => cell.Letter, cell => cell.Key, StringComparer.Ordinal);
                if (keyByLetter.Count == 0)
                {
                    issues.Add(Error(RowLocation(sheet, FormWorkbookCatalog.KeyRow),
                        "The hidden key row is missing, so no column can be recognized. Do not delete row 2."));
                    yield break;
                }
                continue;
            }

            var values = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            bool blank = true;
            foreach (var (letter, key) in keyByLetter)
            {
                var text = CellText.From(Cell(row, letter));
                blank &= text.Length == 0;
                values[key] = (text, letter);
            }
            if (!blank)
                yield return (rowNumber, values);
        }
    }

    private static IEnumerable<IDictionary<string, object>> Rows(MemoryStream buffer, string sheet, string startCell)
    {
        buffer.Position = 0;
        IEnumerable<object> rows = buffer.Query(useHeaderRow: false, sheetName: sheet, startCell: startCell);
        return rows.Cast<IDictionary<string, object>>();
    }

    private static object? Cell(IDictionary<string, object> row, string letter)
        => row.TryGetValue(letter, out var value) ? value : null;

    private static void Translate(Dictionary<string, string> zh, string key, string text)
    {
        if (text.Length > 0) zh[key] = text;
    }

    private static string RowLocation(string sheet, int rowNumber) => $"{sheet}!{rowNumber}";

    private static DefinitionIssue Error(string location, string message) => new(IssueSeverity.Error, location, message);
}
