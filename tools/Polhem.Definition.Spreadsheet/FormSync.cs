using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Brings the files that derive from a saved FormSchema up to date with it: its TableSchemas, its FormLayout and its
/// table registrations. It is an import of the form as the definition folder already holds it, in
/// <see cref="TableSchemaImportMode.Merge"/> mode, so it only adds.
/// </summary>
public static class FormSync
{
    /// <summary>
    /// Plans the sync of the form <paramref name="progId"/> in <paramref name="definePath"/>, without writing anything.
    /// </summary>
    /// <remarks>
    /// Besides the issues of <see cref="FormImporter.Plan"/>, the plan warns about what a merge does not change: a
    /// column whose type or size on the form differs from its TableSchema, and a layout field the form no longer has.
    /// </remarks>
    /// <param name="definePath">The definition folder (the folder that holds <c>FormSchema/</c>).</param>
    /// <param name="progId">The program id of the form.</param>
    /// <param name="layoutMode">What happens to an existing FormLayout; by default the fields it lacks are added.</param>
    /// <returns>The plan; <see cref="FormImporter.Apply"/> writes it.</returns>
    /// <exception cref="FileNotFoundException">The form has no FormSchema file.</exception>
    public static FormImportPlan Plan(string definePath, string progId, FormLayoutImportMode layoutMode = FormLayoutImportMode.Merge)
    {
        var content = FormExporter.Load(definePath, progId);
        var plan = FormImporter.Plan(content, new FormImportOptions
        {
            DefinePath = definePath,
            TableSchemaMode = TableSchemaImportMode.Merge,
            LayoutMode = layoutMode,
        });
        if (plan.HasErrors) return plan;

        var paths = new PathOptions { DefinePath = Path.GetFullPath(definePath) };
        var schema = content.Schema;
        var warnings = ColumnDifferences(paths, schema).Concat(StaleLayoutFields(paths, schema));
        return new FormImportPlan([.. plan.Issues, .. warnings], plan.Files);
    }

    /// <summary>The database tables of <paramref name="schema"/>: the master table and every detail table.</summary>
    /// <remarks>
    /// The tables of the forms its relation fields point to are not included; they belong to those forms.
    /// </remarks>
    public static IReadOnlyList<string> TableNames(FormSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return (schema.Tables ?? [])
            .Select(t => t.DbTableName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<DefinitionIssue> ColumnDifferences(PathOptions paths, FormSchema schema)
    {
        var categoryId = TableSchemaGenerator.GetCategoryId(schema);
        foreach (var table in schema.Tables ?? [])
        {
            var path = paths.GetTableSchemaFilePath(categoryId, table.DbTableName);
            if (!File.Exists(path)) continue;
            var existing = XmlCodec.DeserializeFromFile<TableSchema>(path);
            if (existing?.Fields is null) continue;

            foreach (var generated in table.GenerateDbTable().Fields ?? [])
            {
                if (!existing.Fields.Contains(generated.FieldName)) continue;
                var column = existing.Fields[generated.FieldName];
                var kept = Describe(column);
                var derived = Describe(generated);
                if (kept == derived || (column.DbType == generated.DbType && generated.Length == 0 && column.Length > 0))
                    continue;
                yield return new DefinitionIssue(IssueSeverity.Warning, $"{table.TableName}.{generated.FieldName}",
                    $"The TableSchema keeps {kept}, the form gives {derived}; edit the TableSchema to change the column.");
            }
        }
    }

    private static string Describe(DbField field) => field.DbType switch
    {
        Polhem.Core.Data.FieldDbType.String => $"String({field.Length})",
        Polhem.Core.Data.FieldDbType.Decimal => $"Decimal({field.Precision},{field.Scale})",
        _ => field.DbType.ToString(),
    };

    private static IEnumerable<DefinitionIssue> StaleLayoutFields(PathOptions paths, FormSchema schema)
    {
        var path = paths.GetFormLayoutFilePath(schema.ProgId);
        if (!File.Exists(path)) yield break;
        var layout = XmlCodec.DeserializeFromFile<FormLayout>(path);
        if (layout is null) yield break;

        var master = schema.MasterTable;
        var placed = (layout.Sections ?? []).SelectMany(s => s.Fields ?? [])
            .Select(f => (Name: master?.TableName ?? schema.ProgId, Table: master, f.FieldName))
            .Concat((layout.Details ?? []).SelectMany(g => (g.Columns ?? [])
                .Select(c => (Name: g.TableName, Table: schema.Tables?.Contains(g.TableName) == true ? schema.Tables[g.TableName] : null, c.FieldName))));
        foreach (var (name, table, fieldName) in placed)
        {
            if (table?.Fields?.Contains(fieldName) == true) continue;
            yield return new DefinitionIssue(IssueSeverity.Warning, $"{name}.{fieldName}",
                "The FormLayout still shows this field, which the form no longer has; remove it from the FormLayout by hand.");
        }
    }
}
