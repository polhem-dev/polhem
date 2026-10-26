using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The steps of the relation dialog that need no window: listing the related form's master fields against a field's
/// current mappings, deciding what each row would do, and writing the result back to the field and its table.
/// </summary>
/// <remarks>
/// Destination names and captions follow the convention the form workbook import uses for <c>sys_id</c> and
/// <c>sys_name</c>: <c>ref_&lt;base&gt;_&lt;source&gt;</c>, where <c>&lt;base&gt;</c> is the foreign key's name without
/// its <c>_rowid</c> suffix.
/// </remarks>
public static class RelationSetup
{
    private const string RowIdSuffix = "_rowid";

    /// <summary>
    /// The rows the dialog lists for relating <paramref name="field"/> to the form whose master table is
    /// <paramref name="sourceMaster"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every master field but <c>sys_rowid</c>, which the foreign key itself receives, gets a row in table order. A row
    /// whose source an existing mapping reads takes that mapping's destination and lookup-only flag and is included; a
    /// source read by several mappings gets a row per mapping.
    /// </para>
    /// <para>
    /// A mapping whose source the master table does not have is listed after the others as missing. It stays included
    /// while the form is the field's current related form (<paramref name="isCurrentForm"/>), so nothing disappears
    /// unseen, and is left out when the user picked another form. When no row ends up included, <c>sys_id</c> and
    /// <c>sys_name</c> are.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<RelationSetupRow> CreateRows(FormField field, FormTable? sourceMaster, bool isCurrentForm)
    {
        ArgumentNullException.ThrowIfNull(field);
        var table = field.Table;
        var pending = field.RelationFieldMappings?.ToList() ?? [];
        var rows = new List<RelationSetupRow>();

        foreach (var source in sourceMaster?.Fields ?? [])
        {
            if (string.IsNullOrEmpty(source.FieldName) || IsName(source.FieldName, SysFields.RowId)) continue;
            var matches = pending.Where(m => IsName(m.SourceField, source.FieldName)).ToList();
            if (matches.Count == 0)
            {
                rows.Add(NewRow(field, table, source));
                continue;
            }
            foreach (var mapping in matches)
            {
                var row = NewRow(field, table, source);
                row.LookupOnly = mapping.LookupOnly;
                row.DestinationField = mapping.DestinationField;
                row.IsIncluded = true;
                rows.Add(row);
                pending.Remove(mapping);
            }
        }

        foreach (var mapping in pending)
        {
            var row = new RelationSetupRow(mapping.SourceField, mapping.DestinationField, mapping.DestinationField)
            {
                IsSourceMissing = true,
            };
            row.LookupOnly = mapping.LookupOnly;
            row.DestinationField = mapping.DestinationField;
            row.IsIncluded = isCurrentForm;
            rows.Add(row);
        }

        if (!rows.Exists(r => r.IsIncluded))
        {
            foreach (var row in rows.Where(r => !r.IsSourceMissing && (IsName(r.SourceField, SysFields.Id) || IsName(r.SourceField, SysFields.Name))))
                row.IsIncluded = true;
        }
        Evaluate(field, rows);
        return rows;
    }

    /// <summary>
    /// The destination a regular mapping of <paramref name="sourceField"/> on <paramref name="foreignKey"/> gets by
    /// default: <c>ref_&lt;base&gt;_&lt;suffix&gt;</c>, the suffix being the source name without a <c>sys_</c> or
    /// <c>ref_</c> prefix, so <c>sys_name</c> on <c>customer_rowid</c> goes to <c>ref_customer_name</c>.
    /// </summary>
    public static string DefaultDestination(string foreignKey, string sourceField)
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(sourceField);
        var baseName = foreignKey.EndsWith(RowIdSuffix, StringComparison.OrdinalIgnoreCase)
            ? foreignKey[..^RowIdSuffix.Length]
            : foreignKey;
        var suffix = sourceField;
        foreach (var prefix in new[] { "sys_", "ref_" })
        {
            if (suffix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && suffix.Length > prefix.Length)
            {
                suffix = suffix[prefix.Length..];
                break;
            }
        }
        return $"ref_{baseName}_{suffix}";
    }

    /// <summary>
    /// Sets the <see cref="RelationSetupRow.Status"/> of every row and returns whether any included row has an error
    /// that refuses applying.
    /// </summary>
    public static bool Evaluate(FormField field, IReadOnlyList<RelationSetupRow> rows)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(rows);
        var fields = field.Table?.Fields;
        var destinations = rows.Where(r => r.IsIncluded && !string.IsNullOrWhiteSpace(r.DestinationField))
            .GroupBy(r => r.DestinationField.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasErrors = false;
        foreach (var row in rows)
        {
            row.Status = StatusOf(row);
            hasErrors |= IsError(row.Status);
        }
        return hasErrors;

        RelationSetupRowStatus StatusOf(RelationSetupRow row)
        {
            if (!row.IsIncluded) return RelationSetupRowStatus.None;
            var destination = row.DestinationField.Trim();
            if (destination.Length == 0) return RelationSetupRowStatus.MissingDestination;
            if (IsName(destination, field.FieldName)) return RelationSetupRowStatus.DestinationIsForeignKey;
            if (destinations.Contains(destination)) return RelationSetupRowStatus.DuplicateDestination;
            if (fields?.GetOrDefault(destination) is not { } existing) return RelationSetupRowStatus.New;
            return row.SourceDbType is { } type && CreatedDbType(type) != existing.DbType
                ? RelationSetupRowStatus.TypeMismatch
                : RelationSetupRowStatus.Existing;
        }
    }

    /// <summary>Returns whether <paramref name="status"/> refuses applying.</summary>
    public static bool IsError(RelationSetupRowStatus status) => status is RelationSetupRowStatus.MissingDestination
        or RelationSetupRowStatus.DestinationIsForeignKey or RelationSetupRowStatus.DuplicateDestination;

    /// <summary>
    /// The relation fields that applying <paramref name="rows"/> would leave without any mapping: fields a current
    /// mapping of <paramref name="field"/> writes that no included row and no mapping of another field writes.
    /// </summary>
    /// <remarks>Only <see cref="FieldType.RelationField"/> fields count; a stored field keeps its data, so it is never offered for removal.</remarks>
    public static IReadOnlyList<string> OrphanedFields(FormField field, IReadOnlyList<RelationSetupRow> rows)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(rows);
        if (field.Table?.Fields is not { } fields) return [];
        var kept = rows.Where(r => r.IsIncluded).Select(r => r.DestinationField.Trim())
            .Concat(fields.Where(f => !ReferenceEquals(f, field))
                .SelectMany(f => f.RelationFieldMappings ?? [])
                .Select(m => m.DestinationField))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (field.RelationFieldMappings ?? [])
            .Select(m => m.DestinationField)
            .Where(name => !kept.Contains(name) && fields.GetOrDefault(name) is { Type: FieldType.RelationField })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The included lookup-only rows whose source field the related form's lookup query does not return, so a pick
    /// cannot write them back.
    /// </summary>
    public static IReadOnlyList<string> SourcesMissingFromLookup(FormSchema? sourceSchema, IReadOnlyList<RelationSetupRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (sourceSchema is null) return [];
        var lookup = sourceSchema.GetLookupFields().Select(f => f.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rows.Where(r => r.IsIncluded && r.LookupOnly && !r.IsSourceMissing && !lookup.Contains(r.SourceField))
            .Select(r => r.SourceField)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Relates <paramref name="field"/> to <paramref name="progId"/> with the included <paramref name="rows"/> as its
    /// mappings, adding each destination field the table lacks after the foreign key and its other relation fields.
    /// </summary>
    /// <remarks>
    /// An empty <paramref name="progId"/> removes the relation and every mapping. A foreign key that is not a
    /// <see cref="FieldDbType.Guid"/> becomes one, since it holds the picked row's <c>sys_rowid</c>. A new destination
    /// takes the source's data type and length; its caption follows the import convention for <c>sys_id</c> and
    /// <c>sys_name</c> (<c>Code</c> and <c>Name</c> after the foreign key's caption).
    /// </remarks>
    /// <param name="field">The foreign key field, which must belong to a table.</param>
    /// <param name="progId">The related form.</param>
    /// <param name="rows">The rows, as <see cref="CreateRows"/> built them and the user edited them.</param>
    /// <param name="removeOrphans">Whether to remove the fields <see cref="OrphanedFields"/> reports.</param>
    /// <exception cref="InvalidOperationException">The field belongs to no table, or a row has an error.</exception>
    public static RelationSetupChanges Apply(FormField field, string progId, IReadOnlyList<RelationSetupRow> rows,
        bool removeOrphans)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(rows);
        var fields = field.Table?.Fields ?? throw new InvalidOperationException("The field belongs to no table.");
        var relate = !string.IsNullOrWhiteSpace(progId);
        var included = relate ? rows.Where(r => r.IsIncluded).ToList() : [];
        if (relate && Evaluate(field, rows))
            throw new InvalidOperationException("A row of the relation has an error.");

        var removed = new List<FormField>();
        if (removeOrphans)
        {
            var orphans = OrphanedFields(field, included);
            foreach (var name in orphans)
            {
                if (fields.GetOrDefault(name) is not { } orphan) continue;
                fields.Remove(orphan);
                removed.Add(orphan);
            }
        }

        var mappings = field.RelationFieldMappings!;
        mappings.Clear();
        field.RelationProgId = relate ? progId.Trim() : string.Empty;
        if (!relate) return new RelationSetupChanges([], removed, false);

        var retyped = field.DbType != FieldDbType.Guid;
        field.DbType = FieldDbType.Guid;

        int index = fields.IndexOf(field) + 1;
        foreach (var row in included)
        {
            var destination = row.DestinationField.Trim();
            mappings.Add(new FieldMapping(row.SourceField, destination) { LookupOnly = row.LookupOnly });
            if (fields.GetOrDefault(destination) is { } existing && existing.Type == FieldType.RelationField)
                index = Math.Max(index, fields.IndexOf(existing) + 1);
        }

        var added = new List<FormField>();
        foreach (var row in included)
        {
            var destination = row.DestinationField.Trim();
            if (fields.Contains(destination)) continue;
            var created = new FormField(destination, Caption(field, row), CreatedDbType(row.SourceDbType ?? FieldDbType.String),
                row.NewFieldType)
            {
                MaxLength = row.SourceMaxLength,
            };
            fields.Insert(index++, created);
            added.Add(created);
        }
        return new RelationSetupChanges(added, removed, retyped);
    }

    /// <summary>
    /// The validation issues applying <paramref name="rows"/> would add to <paramref name="schema"/>, such as a relation
    /// field only lookup-only mappings write (POLHEM1006), as <c>location: message</c> lines. The schema is not changed:
    /// the rows are applied to a copy and the copy's issues that the schema does not already have are returned.
    /// </summary>
    public static IReadOnlyList<string> NewIssues(FormSchema schema, FormField field, string progId,
        IReadOnlyList<RelationSetupRow> rows, IEnumerable<string> availableProgIds)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(rows);
        var progIds = availableProgIds.ToList();
        var copy = schema.Clone();
        var copiedField = field.Table is { } table
            ? copy.Tables?.GetOrDefault(table.TableName)?.Fields?.GetOrDefault(field.FieldName)
            : null;
        if (copiedField is null || Evaluate(field, rows)) return [];

        // Status is part of what the rows show, so the copy gets rows of its own rather than the dialog's.
        var copiedRows = rows.Select(r =>
        {
            var copiedRow = new RelationSetupRow(r.SourceField, r.JoinDestination, r.LookupDestination)
            {
                SourceCaption = r.SourceCaption,
                SourceDbType = r.SourceDbType,
                SourceMaxLength = r.SourceMaxLength,
                IsSourceMissing = r.IsSourceMissing,
            };
            copiedRow.LookupOnly = r.LookupOnly;
            copiedRow.NewFieldType = r.NewFieldType;
            copiedRow.DestinationField = r.DestinationField;
            copiedRow.IsIncluded = r.IsIncluded;
            return copiedRow;
        }).ToList();
        Apply(copiedField, progId, copiedRows, removeOrphans: false);

        static string Line(DefinitionIssue issue) => $"{issue.Location}: {issue.Message}";
        var before = FormSchemaValidator.Validate(schema, progIds).Select(Line).ToHashSet(StringComparer.Ordinal);
        return FormSchemaValidator.Validate(copy, progIds).Select(Line).Where(line => !before.Contains(line)).Distinct().ToList();
    }

    private static RelationSetupRow NewRow(FormField field, FormTable? table, FormField source)
    {
        var join = DefaultDestination(field.FieldName, source.FieldName);
        var lookup = table?.Fields?.Contains(source.FieldName) == true && !IsName(source.FieldName, field.FieldName)
            ? source.FieldName
            : join;
        return new RelationSetupRow(source.FieldName, join, lookup)
        {
            SourceCaption = source.Caption,
            SourceDbType = source.DbType,
            SourceMaxLength = source.MaxLength,
        };
    }

    private static string Caption(FormField foreignKey, RelationSetupRow row)
    {
        var source = row.SourceCaption.Length > 0 ? row.SourceCaption : row.DestinationField.Trim();
        if (row.LookupOnly) return source;
        if (string.IsNullOrEmpty(foreignKey.Caption)) return row.DestinationField.Trim();
        if (IsName(row.SourceField, SysFields.Id)) return $"{foreignKey.Caption} Code";
        if (IsName(row.SourceField, SysFields.Name)) return $"{foreignKey.Caption} Name";
        return $"{foreignKey.Caption} {source}";
    }

    // An auto-increment source is a plain number once copied; the destination must not number its own rows.
    private static FieldDbType CreatedDbType(FieldDbType source) =>
        source == FieldDbType.AutoIncrement ? FieldDbType.Integer : source;

    private static bool IsName(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
