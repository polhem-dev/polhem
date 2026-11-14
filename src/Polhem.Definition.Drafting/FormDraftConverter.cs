using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// Turns a <see cref="FormDraft"/> into a <see cref="FormSchema"/> by fixed rules, so the model never writes the
    /// definition itself: the names, the types and the editors are decided here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An incomplete draft still converts, so the form can be previewed while the conversation goes on. What is
    /// undecided is filled with a default and reported as a <see cref="DraftIssueSeverity.Warning"/>; what cannot be
    /// used (a duplicate name, a name the conventions reserve) is left out and reported as a
    /// <see cref="DraftIssueSeverity.Error"/>.
    /// </para>
    /// <para>
    /// The conversion adds nothing the draft does not describe: no system fields, database table names, category or
    /// relation mappings. Those come from the host's form conventions, which need only the
    /// <see cref="FormField.RelationProgId"/> of a relation field.
    /// </para>
    /// </remarks>
    public static class FormDraftConverter
    {
        /// <summary>The ProgId a draft is previewed under while its own ProgId is undecided.</summary>
        public const string PlaceholderProgId = "NewForm";

        private static readonly string[] s_reservedPrefixes = ["sys_", "ref_"];

        /// <summary>
        /// Converts a draft into a form definition.
        /// </summary>
        /// <param name="draft">The draft. It is read, not changed.</param>
        /// <returns>The form, its zh-TW texts, its menu placement and what the conversion found.</returns>
        public static FormDraftResult Convert(FormDraft draft)
        {
            ArgumentNullException.ThrowIfNull(draft);

            var progId = DraftNaming.ToPascalCase(draft.ProgId);
            var issues = new List<DraftIssue>();
            if (progId.Length == 0)
            {
                progId = PlaceholderProgId;
                issues.Add(Warning("Schema", $"The ProgId is not decided yet; previewed as '{PlaceholderProgId}'."));
            }
            var displayName = draft.DisplayName.Trim();
            if (displayName.Length == 0)
            {
                displayName = progId;
                issues.Add(Warning("Schema", "The display name is not decided yet."));
            }

            var result = new FormDraftResult(new FormSchema(progId, displayName));
            Translate(result, FormSchemaLocalizer.SchemaDisplayNameKey, draft.DisplayNameZhTw);
            CopyMenu(draft.Menu, result.Menu);

            if (draft.Tables.Count == 0)
                result.Schema.Tables!.Add(new FormTable(progId, displayName));
            for (var index = 0; index < draft.Tables.Count; index++)
                AddTable(result, draft.Tables[index], index, issues);

            foreach (var issue in issues)
                result.Issues.Add(issue);
            return result;
        }

        private static void AddTable(FormDraftResult result, DraftTable draftTable, int index, List<DraftIssue> issues)
        {
            var schema = result.Schema;
            string tableName;
            if (index == 0)
            {
                tableName = schema.ProgId;
            }
            else
            {
                tableName = DraftNaming.ToPascalCase(draftTable.TableName);
                if (tableName.Length == 0)
                {
                    tableName = $"{schema.ProgId}Detail{index}";
                    issues.Add(Warning($"Tables[{index}]", $"The table name is not decided yet; previewed as '{tableName}'."));
                }
            }
            if (schema.Tables!.Contains(tableName))
            {
                issues.Add(Error(tableName, $"The table '{tableName}' appears more than once; only the first is kept."));
                return;
            }

            var displayName = draftTable.DisplayName.Trim();
            if (displayName.Length == 0)
                displayName = index == 0 ? schema.DisplayName : tableName;
            var table = schema.Tables.Add(tableName, displayName);
            Translate(result, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                FormSchemaLocalizer.TableDisplayNameKeyFormat, tableName), draftTable.DisplayNameZhTw);

            for (var fieldIndex = 0; fieldIndex < draftTable.Fields.Count; fieldIndex++)
                AddField(result, table, draftTable.Fields[fieldIndex], fieldIndex, issues);
        }

        private static void AddField(FormDraftResult result, FormTable table, DraftField draftField, int index, List<DraftIssue> issues)
        {
            var fieldName = DraftNaming.ToSnakeCase(draftField.FieldName);
            if (fieldName.Length == 0)
            {
                fieldName = $"field_{index + 1}";
                issues.Add(Warning($"{table.TableName}.Fields[{index}]",
                    $"The field name of '{draftField.Caption}' is not decided yet; previewed as '{fieldName}'."));
            }
            var location = $"{table.TableName}.{fieldName}";
            if (Array.Exists(s_reservedPrefixes, prefix => fieldName.StartsWith(prefix, StringComparison.Ordinal)))
            {
                issues.Add(Error(location, $"The field '{fieldName}' is left out: names starting with "
                    + $"{string.Join(" or ", s_reservedPrefixes)} are added by the form conventions."));
                return;
            }
            if (table.Fields!.Contains(fieldName))
            {
                issues.Add(Error(location, $"The field '{fieldName}' appears more than once in '{table.TableName}'; only the first is kept."));
                return;
            }

            var caption = draftField.Caption.Trim();
            if (caption.Length == 0)
            {
                caption = fieldName;
                issues.Add(Warning(location, "The caption is not decided yet."));
            }
            var field = table.Fields.Add(fieldName, caption, ResolveDbType(draftField, location, issues));
            field.MaxLength = ResolveMaxLength(draftField, field.DbType, location, issues);
            field.ControlType = draftField.ControlType
                ?? (draftField.Options.Count > 0 ? ControlType.DropDownEdit : ControlType.Auto);
            field.Required = draftField.Required;
            field.ValueExpression = draftField.ValueExpression.Trim();
            field.ReadOnly = field.ValueExpression.Length > 0;
            AddOptions(field.ListItems!, draftField.Options, location, issues);
            ApplyRelation(field, draftField.Relation, location, issues);

            Translate(result, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                FormSchemaLocalizer.FieldCaptionKeyFormat, fieldName), draftField.CaptionZhTw);
            var quote = draftField.SourceQuote.Trim();
            if (quote.Length > 0)
                result.FieldSources[location] = quote;
        }

        private static FieldDbType ResolveDbType(DraftField draftField, string location, List<DraftIssue> issues)
        {
            if (draftField.DbType is { } declared)
            {
                if (declared != FieldDbType.Unknown && Enum.IsDefined(declared)) return declared;
                issues.Add(Error(location, $"'{declared}' is not a type a field can have; previewed as {FieldDbType.String}."));
                return FieldDbType.String;
            }

            var inferred = draftField.Options.Count > 0 ? FieldDbType.String : draftField.ControlType switch
            {
                ControlType.DateEdit => FieldDbType.Date,
                ControlType.DateTimeEdit => FieldDbType.DateTime,
                ControlType.TimeEdit => FieldDbType.Time,
                ControlType.CheckEdit => FieldDbType.Boolean,
                ControlType.NumericEdit => FieldDbType.Decimal,
                ControlType.MemoEdit => FieldDbType.Text,
                _ => FieldDbType.String,
            };
            issues.Add(Warning(location, $"The type is not decided yet; previewed as {inferred}."));
            return inferred;
        }

        private static int ResolveMaxLength(DraftField draftField, FieldDbType dbType, string location, List<DraftIssue> issues)
        {
            if (dbType != FieldDbType.String) return 0;
            switch (draftField.MaxLength)
            {
                case null:
                    issues.Add(Warning(location, "The maximum length is not decided yet."));
                    return 0;
                case <= 0:
                    issues.Add(Error(location, $"The maximum length {draftField.MaxLength} is not a positive number."));
                    return 0;
                default:
                    return draftField.MaxLength.Value;
            }
        }

        private static void AddOptions(ListItemCollection items, IList<DraftOption> options, string location, List<DraftIssue> issues)
        {
            for (var index = 0; index < options.Count; index++)
            {
                var value = options[index].Value.Trim();
                if (value.Length == 0)
                {
                    issues.Add(Error($"{location}.Options[{index}]", "The option has no value and is left out."));
                    continue;
                }
                if (items.Contains(value))
                {
                    issues.Add(Error($"{location}.Options[{index}]", $"The option value '{value}' appears more than once; only the first is kept."));
                    continue;
                }
                var text = options[index].Text.Trim();
                items.Add(value, text.Length == 0 ? value : text);
            }
        }

        private static void ApplyRelation(FormField field, DraftRelation? relation, string location, List<DraftIssue> issues)
        {
            if (relation is null) return;
            var progId = relation.ProgId.Trim();
            if (progId.Length == 0)
            {
                issues.Add(Warning(location, "The form this field picks a record from is not decided yet."));
                return;
            }
            field.RelationProgId = progId;
        }

        private static void CopyMenu(DraftMenuPlacement source, DraftMenuPlacement target)
        {
            target.FolderId = source.FolderId.Trim();
            target.FolderCaption = source.FolderCaption.Trim();
            target.FolderCaptionZhTw = source.FolderCaptionZhTw.Trim();
            target.EntryCaption = source.EntryCaption.Trim();
            target.EntryCaptionZhTw = source.EntryCaptionZhTw.Trim();
        }

        private static void Translate(FormDraftResult result, string key, string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length > 0)
                result.ZhTw.TryAdd(key, trimmed);
        }

        private static DraftIssue Warning(string location, string message) => new(DraftIssueSeverity.Warning, location, message);

        private static DraftIssue Error(string location, string message) => new(DraftIssueSeverity.Error, location, message);
    }
}
