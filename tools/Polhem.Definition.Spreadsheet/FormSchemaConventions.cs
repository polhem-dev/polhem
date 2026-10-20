using System.Text;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Fills in what a form described in a workbook leaves to convention, so the person describing it lists
/// only the business fields.
/// </summary>
/// <remarks>
/// Every step only adds what is missing, so applying the conventions to a form that already follows them
/// changes nothing; a form exported from the definition files and read back comes out the same.
/// </remarks>
public static class FormSchemaConventions
{
    /// <summary>The prefix of a business table name (<c>ft_order</c>).</summary>
    public const string BusinessTablePrefix = "ft_";

    /// <summary>
    /// Applies the conventions to <paramref name="content"/> in place.
    /// </summary>
    /// <param name="content">The form read from a workbook.</param>
    /// <returns>Problems that keep the conventions from applying.</returns>
    public static IReadOnlyList<DefinitionIssue> Apply(FormWorkbookContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var schema = content.Schema;
        var issues = new List<DefinitionIssue>();

        if (string.IsNullOrWhiteSpace(schema.CategoryId))
            schema.CategoryId = DbCategoryIds.Company;

        if (string.IsNullOrWhiteSpace(schema.ProgId))
        {
            issues.Add(new(IssueSeverity.Error, FormWorkbookCatalog.FormSheet, "The ProgId is empty."));
            return issues;
        }
        if (!schema.Tables!.Contains(schema.ProgId))
        {
            issues.Add(new(IssueSeverity.Error, FormWorkbookCatalog.TablesSheet,
                $"No table is named '{schema.ProgId}'. The master table must carry the ProgId as its table name."));
            return issues;
        }

        foreach (var table in schema.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.DbTableName))
                table.DbTableName = BusinessTablePrefix + ToSnakeCase(table.TableName);

            bool isMaster = string.Equals(table.TableName, schema.ProgId, StringComparison.OrdinalIgnoreCase);
            AddSystemFields(table, isMaster);
            AddRelationFields(table, content);
        }
        return issues;
    }

    /// <summary>
    /// Converts a PascalCase name to lower snake case: <c>OrderDetail</c> becomes <c>order_detail</c>.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The snake-case name.</returns>
    public static string ToSnakeCase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0 && name[i - 1] != '_'
                && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                    || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                builder.Append('_');
            }
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static void AddSystemFields(FormTable table, bool isMaster)
    {
        var fields = table.Fields!;
        int index = 0;
        Insert(SysFields.No, "Sequence", FieldDbType.AutoIncrement);
        Insert(SysFields.RowId, "Row ID", FieldDbType.Guid);
        if (!isMaster)
            Insert(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid);

        void Insert(string fieldName, string caption, FieldDbType dbType)
        {
            if (fields.Contains(fieldName))
            {
                index = Math.Max(index, fields.IndexOf(fields[fieldName]) + 1);
                return;
            }
            fields.Insert(index++, new FormField(fieldName, caption, dbType) { Visible = false });
        }
    }

    /// <summary>
    /// For a relation field without mappings, maps <c>sys_id</c> and <c>sys_name</c> of the related form to
    /// <c>ref_&lt;base&gt;_id</c> and <c>ref_&lt;base&gt;_name</c>, and adds those two fields after it when they
    /// are missing. <c>&lt;base&gt;</c> is the field name without its <c>_rowid</c> suffix.
    /// </summary>
    private static void AddRelationFields(FormTable table, FormWorkbookContent content)
    {
        var fields = table.Fields!;
        foreach (var field in fields.ToList())
        {
            if (field.Type != FieldType.DbField
                || string.IsNullOrWhiteSpace(field.RelationProgId)
                || field.RelationFieldMappings is { Count: > 0 })
            {
                continue;
            }

            field.DbType = FieldDbType.Guid;
            var baseName = field.FieldName.EndsWith("_rowid", StringComparison.OrdinalIgnoreCase)
                ? field.FieldName[..^"_rowid".Length]
                : field.FieldName;
            var zhCaption = Translation(content, field.FieldName);

            int index = fields.IndexOf(field) + 1;
            foreach (var (sourceField, suffix, captionSuffix, zhSuffix) in new[]
            {
                (SysFields.Id, "id", "Code", "代號"),
                (SysFields.Name, "name", "Name", "名稱"),
            })
            {
                var refName = $"ref_{baseName}_{suffix}";
                field.RelationFieldMappings!.Add(new FieldMapping(sourceField, refName));
                if (fields.Contains(refName))
                {
                    index = Math.Max(index, fields.IndexOf(fields[refName]) + 1);
                    continue;
                }
                var caption = string.IsNullOrEmpty(field.Caption) ? refName : $"{field.Caption} {captionSuffix}";
                fields.Insert(index++, new FormField(refName, caption, FieldDbType.String, FieldType.RelationField));
                var refKey = FormWorkbookWriter.Key(FormSchemaLocalizer.FieldCaptionKeyFormat, refName);
                if (zhCaption.Length > 0 && !content.ZhTw.ContainsKey(refKey))
                    content.ZhTw[refKey] = zhCaption + zhSuffix;
            }
        }
    }

    private static string Translation(FormWorkbookContent content, string fieldName)
        => content.ZhTw.TryGetValue(FormWorkbookWriter.Key(FormSchemaLocalizer.FieldCaptionKeyFormat, fieldName), out var text)
            ? text
            : string.Empty;
}
