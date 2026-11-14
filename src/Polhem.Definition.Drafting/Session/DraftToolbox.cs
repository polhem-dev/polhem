using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Polhem.Core.Data;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Drafting.Session
{
    /// <summary>
    /// The tools a model drafts a form with. Every tool runs locally and changes only the draft; none writes a file.
    /// </summary>
    /// <remarks>
    /// A tool checks all of its input before it changes anything, so a refused call (a
    /// <see cref="DraftToolException"/>) leaves the draft as it was.
    /// </remarks>
    internal sealed partial class DraftToolbox
    {
        /// <summary>The name that always refers to the master table, whatever the form's ProgId is.</summary>
        public const string MasterTableKey = "master";

        private static readonly string[] s_reservedPrefixes = ["sys_", "ref_"];

        private static readonly JsonSerializerOptions s_serializerOptions = CreateSerializerOptions();

        private readonly FormDraft _draft;
        private readonly IFormDraftingHost _host;
        private readonly double _relationConfirmationThreshold;

        public DraftToolbox(FormDraft draft, IFormDraftingHost host, double relationConfirmationThreshold)
        {
            _draft = draft;
            _host = host;
            _relationConfirmationThreshold = relationConfirmationThreshold;
            Functions =
            [
                Create(SetFormInfo, "set_form_info",
                    "Sets the form's ProgId, display names and menu placement. Pass only what changes."),
                Create(UpsertTable, "upsert_table",
                    $"Creates or updates a table. '{MasterTableKey}' is the master table; any other name is a detail table. "
                    + "Do not add the link to the master record: it is added automatically."),
                Create(RemoveTable, "remove_table", "Removes a detail table, or the master table when it is the only one."),
                Create(UpsertField, "upsert_field",
                    "Creates or updates a field of a table. Pass only what changes; omitted values stay as they are."),
                Create(RemoveField, "remove_field", "Removes a field from a table."),
                Create(SearchProgIds, "search_progids",
                    "Searches the existing forms by keywords in English or Chinese, to find the form a field picks a record from."),
                Create(GetProgIdFields, "get_progid_fields", "Lists the fields of an existing form's master table."),
                Create(SetRelation, "set_relation",
                    "Makes a field pick a record from an existing form, with how sure you are and why. Pass an empty progId to remove the relation."),
                Create(GetDraft, "get_draft", "Returns the whole draft as it is now, including any edits the user made."),
                Create(ValidateDraft, "validate_draft", "Converts the draft into a form definition and returns the problems found."),
                Create(MarkReady, "mark_ready",
                    "Marks the draft ready to import. Succeeds only when validate_draft reports no errors."),
            ];
        }

        /// <summary>Gets the tools, to be invoked by the session and declared to the model.</summary>
        public IReadOnlyList<AIFunction> Functions { get; }

        /// <summary>Gets or sets whether the draft passed <c>mark_ready</c> and has not changed since.</summary>
        public bool IsReady { get; set; }

        /// <summary>Gets or sets whether the last tool call changed the draft. The session resets it before each call.</summary>
        public bool DraftModified { get; set; }

        private string SetFormInfo(
            [Description("The ProgId in PascalCase English, such as LeaveRequest.")] string? progId = null,
            [Description("The English display name.")] string? displayName = null,
            [Description("The Traditional Chinese (zh-TW) display name.")] string? displayNameZhTw = null,
            [Description("The id of the menu folder that holds the form, such as Hr.")] string? menuFolderId = null,
            [Description("The English caption of the menu folder, used when the folder does not exist yet.")] string? menuFolderCaption = null,
            [Description("The zh-TW caption of the menu folder.")] string? menuFolderCaptionZhTw = null,
            [Description("The English caption of the menu entry; empty uses the display name.")] string? menuEntryCaption = null,
            [Description("The zh-TW caption of the menu entry.")] string? menuEntryCaptionZhTw = null)
        {
            string? normalizedProgId = null;
            if (progId is not null)
            {
                normalizedProgId = DraftNaming.ToPascalCase(progId);
                if (normalizedProgId.Length == 0)
                    throw new DraftToolException($"'{progId}' has no English letters or digits to make a ProgId from.");
            }
            if (new[] { progId, displayName, displayNameZhTw, menuFolderId, menuFolderCaption, menuFolderCaptionZhTw,
                    menuEntryCaption, menuEntryCaptionZhTw }.All(value => value is null))
                throw new DraftToolException("Pass at least one value to set.");

            if (normalizedProgId is not null) _draft.ProgId = normalizedProgId;
            if (displayName is not null) _draft.DisplayName = displayName.Trim();
            if (displayNameZhTw is not null) _draft.DisplayNameZhTw = displayNameZhTw.Trim();
            if (menuFolderId is not null) _draft.Menu.FolderId = menuFolderId.Trim();
            if (menuFolderCaption is not null) _draft.Menu.FolderCaption = menuFolderCaption.Trim();
            if (menuFolderCaptionZhTw is not null) _draft.Menu.FolderCaptionZhTw = menuFolderCaptionZhTw.Trim();
            if (menuEntryCaption is not null) _draft.Menu.EntryCaption = menuEntryCaption.Trim();
            if (menuEntryCaptionZhTw is not null) _draft.Menu.EntryCaptionZhTw = menuEntryCaptionZhTw.Trim();
            DraftModified = true;

            var message = "Form info updated.";
            if (normalizedProgId is not null && FindCatalogEntry(normalizedProgId) is not null)
                message += $" Note: a form '{normalizedProgId}' already exists, and importing this draft would change it. "
                    + "Confirm with the user that this is intended, or choose another ProgId.";
            return message;
        }

        private string UpsertTable(
            [Description($"'{MasterTableKey}' for the master table, or the PascalCase name of a detail table, such as LeaveRequestDetail.")] string table,
            [Description("The English display name.")] string? displayName = null,
            [Description("The zh-TW display name.")] string? displayNameZhTw = null)
        {
            var existing = FindTable(table);
            string? detailName = null;
            if (existing is null && !IsMasterKey(table))
            {
                detailName = DraftNaming.ToPascalCase(table);
                if (detailName.Length == 0)
                    throw new DraftToolException($"'{table}' has no English letters or digits to make a table name from.");
            }

            var created = existing is null;
            var target = existing ?? (detailName is null ? AddMasterTable() : AddDetailTable(detailName));
            if (displayName is not null) target.DisplayName = displayName.Trim();
            if (displayNameZhTw is not null) target.DisplayNameZhTw = displayNameZhTw.Trim();
            DraftModified = true;
            return $"{(created ? "Created" : "Updated")} the table '{DescribeTable(target)}'.";
        }

        private string RemoveTable(
            [Description($"'{MasterTableKey}' or the name of a detail table.")] string table)
        {
            var target = FindTable(table) ?? throw new DraftToolException(NoSuchTable(table));
            if (ReferenceEquals(target, _draft.Tables[0]) && _draft.Tables.Count > 1)
                throw new DraftToolException("The master table cannot be removed while the form has detail tables.");
            _draft.Tables.Remove(target);
            DraftModified = true;
            return $"Removed the table '{DescribeTable(target)}'.";
        }

        private string UpsertField(
            [Description($"'{MasterTableKey}' or the name of a detail table.")] string table,
            [Description("The field name in snake_case English, such as leave_type.")] string fieldName,
            [Description("The English caption.")] string? caption = null,
            [Description("The zh-TW caption.")] string? captionZhTw = null,
            [Description("The database type.")] FieldDbType? dbType = null,
            [Description("The maximum length of a String field.")] int? maxLength = null,
            [Description("The editor; omit to let it follow the type.")] ControlType? controlType = null,
            [Description("The choices of a field the document shows as a set of options. An empty list removes them.")] DraftOption[]? options = null,
            [Description("An expression that computes the field from other fields, such as quantity * unit_price. Empty removes it.")] string? valueExpression = null,
            [Description("Whether the field must be filled in.")] bool? required = null,
            [Description("The text of the document the field was read from.")] string? sourceQuote = null,
            [Description("A new field name, to rename the field.")] string? renameTo = null)
        {
            var name = CheckFieldName(fieldName);
            var newName = renameTo is null ? null : CheckFieldName(renameTo);
            var target = FindTable(table);
            if (target is null && !IsMasterKey(table))
                throw new DraftToolException(NoSuchTable(table) + " Create it with upsert_table first.");
            var field = target?.Fields.FirstOrDefault(f => f.FieldName == name);
            if (newName is not null && newName != name && target is not null && target.Fields.Any(f => f.FieldName == newName))
                throw new DraftToolException($"The table already has a field '{newName}'.");
            if (dbType == FieldDbType.Unknown)
                throw new DraftToolException($"'{FieldDbType.Unknown}' is not a type a field can have.");
            if (maxLength is <= 0)
                throw new DraftToolException($"The maximum length must be a positive number, not {maxLength}.");
            var optionList = options is null ? null : CheckOptions(options);

            target ??= AddMasterTable();
            var created = field is null;
            if (field is null)
            {
                field = new DraftField { FieldName = name };
                target.Fields.Add(field);
            }
            if (newName is not null) field.FieldName = newName;
            if (caption is not null) field.Caption = caption.Trim();
            if (captionZhTw is not null) field.CaptionZhTw = captionZhTw.Trim();
            if (dbType is not null) field.DbType = dbType;
            if (maxLength is not null) field.MaxLength = maxLength;
            if (controlType is not null) field.ControlType = controlType;
            if (optionList is not null)
            {
                field.Options.Clear();
                foreach (var option in optionList)
                    field.Options.Add(option);
            }
            if (valueExpression is not null) field.ValueExpression = valueExpression.Trim();
            if (required is not null) field.Required = required.Value;
            if (sourceQuote is not null) field.SourceQuote = sourceQuote.Trim();
            DraftModified = true;
            return $"{(created ? "Created" : "Updated")} the field '{DescribeTable(target)}.{field.FieldName}'.";
        }

        private string RemoveField(
            [Description($"'{MasterTableKey}' or the name of a detail table.")] string table,
            [Description("The field name.")] string fieldName)
        {
            var (target, field) = FindField(table, fieldName);
            target.Fields.Remove(field);
            DraftModified = true;
            return $"Removed the field '{DescribeTable(target)}.{field.FieldName}'.";
        }

        private FormDraft GetDraft() => _draft;

        private DraftTable AddMasterTable()
        {
            var master = new DraftTable();
            _draft.Tables.Insert(0, master);
            return master;
        }

        private DraftTable AddDetailTable(string tableName)
        {
            if (_draft.Tables.Count == 0)
                AddMasterTable();
            var detail = new DraftTable { TableName = tableName };
            _draft.Tables.Add(detail);
            return detail;
        }

        private DraftTable? FindTable(string table)
        {
            if (_draft.Tables.Count == 0) return null;
            if (IsMasterKey(table)) return _draft.Tables[0];
            var name = DraftNaming.ToPascalCase(table);
            for (var index = 1; index < _draft.Tables.Count; index++)
            {
                if (string.Equals(DraftNaming.ToPascalCase(_draft.Tables[index].TableName), name, StringComparison.OrdinalIgnoreCase))
                    return _draft.Tables[index];
            }
            return null;
        }

        private (DraftTable Table, DraftField Field) FindField(string table, string fieldName)
        {
            var target = FindTable(table) ?? throw new DraftToolException(NoSuchTable(table));
            var name = DraftNaming.ToSnakeCase(fieldName);
            var field = target.Fields.FirstOrDefault(f => f.FieldName == name)
                ?? throw new DraftToolException($"The table '{DescribeTable(target)}' has no field '{name}'.");
            return (target, field);
        }

        private bool IsMasterKey(string table)
        {
            var trimmed = table.Trim();
            return string.Equals(trimmed, MasterTableKey, StringComparison.OrdinalIgnoreCase)
                || (_draft.ProgId.Length > 0 && string.Equals(DraftNaming.ToPascalCase(trimmed), _draft.ProgId, StringComparison.OrdinalIgnoreCase));
        }

        private string DescribeTable(DraftTable table)
            => _draft.Tables.Count > 0 && ReferenceEquals(table, _draft.Tables[0]) ? MasterTableKey : table.TableName;

        private string NoSuchTable(string table) => $"There is no table '{table}'. Tables: "
            + string.Join(", ", _draft.Tables.Select(DescribeTable).DefaultIfEmpty("(none)")) + ".";

        private static string CheckFieldName(string fieldName)
        {
            var name = DraftNaming.ToSnakeCase(fieldName);
            if (name.Length == 0)
                throw new DraftToolException($"'{fieldName}' has no English letters or digits to make a field name from.");
            if (Array.Exists(s_reservedPrefixes, prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                throw new DraftToolException($"'{name}' is not allowed: names starting with "
                    + $"{string.Join(" or ", s_reservedPrefixes)} are added by the form conventions.");
            return name;
        }

        private static List<DraftOption> CheckOptions(DraftOption[] options)
        {
            var list = new List<DraftOption>();
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
            {
                var value = option.Value.Trim();
                if (value.Length == 0)
                    throw new DraftToolException("Every option needs a value.");
                if (!values.Add(value))
                    throw new DraftToolException($"The option value '{value}' appears more than once.");
                list.Add(new DraftOption { Value = value, Text = option.Text.Trim() });
            }
            return list;
        }

        private static AIFunction Create(Delegate method, string name, string description)
            => AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                SerializerOptions = s_serializerOptions,
            });

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
            options.Converters.Add(new JsonStringEnumConverter());
            options.MakeReadOnly();
            return options;
        }
    }
}
