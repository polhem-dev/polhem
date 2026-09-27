using Polhem.Definition.Collections;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Language
{
    /// <summary>
    /// Applies localized text from <see cref="LanguageResource"/> to a <see cref="FormSchema"/>
    /// instance using a fixed sub-key convention. Namespace is the schema's <see cref="FormSchema.ProgId"/>;
    /// sub-keys follow the table below.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="FormSchema.DisplayName"/> ← <c>Schema.DisplayName</c></description></item>
    /// <item><description><see cref="FormTable.DisplayName"/> ← <c>Table.{TableName}.DisplayName</c></description></item>
    /// <item><description><see cref="FormField.Caption"/> ← <c>Field.{FieldName}.Caption</c></description></item>
    /// <item><description><see cref="FormField.ListItems"/> ← the <see cref="LanguageEnum"/> named by <see cref="FormField.LangEnumName"/></description></item>
    /// <item><description><see cref="FormRule.Message"/> ← <c>Rule.{RuleId}.Message</c>, resolved on the server when the rule fails (see <see cref="RuleMessageKeyFormat"/>)</description></item>
    /// </list>
    /// <para>
    /// Every key resolves through the language fall-back chain (<see cref="LanguageFallback"/>): the
    /// requested culture, its parent cultures, then the default language (skipped for an English
    /// culture, whose language is the base text's). A key no culture declares
    /// leaves the existing string untouched, so the text written in the schema is the base text —
    /// schema authors who do not need i18n keep working with hard-coded labels with no behaviour
    /// change. Captions and option sets follow the same chain, so one culture never gets captions
    /// in one language next to options in another.
    /// </para>
    /// <para>
    /// The localizer mutates the schema in place. Callers that share schema instances
    /// (e.g. via the <see cref="FormSchema"/> Define cache) must clone first; this class deliberately
    /// does <b>not</b> clone, so the same helper composes cleanly with explicit cloning at
    /// the call site (typically the BO method right before returning to the API surface).
    /// </para>
    /// </remarks>
    public sealed class FormSchemaLocalizer
    {
        /// <summary>
        /// Sub-key for the schema-level display name.
        /// </summary>
        public const string SchemaDisplayNameKey = "Schema.DisplayName";

        /// <summary>
        /// Sub-key template for a table's display name. <c>{0}</c> is the table name.
        /// </summary>
        public const string TableDisplayNameKeyFormat = "Table.{0}.DisplayName";

        /// <summary>
        /// Sub-key template for a field's caption. <c>{0}</c> is the field name.
        /// </summary>
        public const string FieldCaptionKeyFormat = "Field.{0}.Caption";

        /// <summary>
        /// Sub-key template for a rule's message. <c>{0}</c> is the rule id.
        /// </summary>
        /// <remarks>
        /// Not applied by <see cref="Localize(FormSchema, string, string)"/>: a rule message is shown
        /// only when the rule fails on the server, so the server resolves it then, in the session's
        /// culture, and <see cref="FormRule.Message"/> stays the base text.
        /// </remarks>
        public const string RuleMessageKeyFormat = "Rule.{0}.Message";

        private readonly ILanguageService _languageService;

        /// <summary>
        /// Initializes a new <see cref="FormSchemaLocalizer"/>.
        /// </summary>
        /// <param name="languageService">The language resource service used to resolve sub-keys.</param>
        public FormSchemaLocalizer(ILanguageService languageService)
        {
            _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        }

        /// <summary>
        /// Applies localized text in <paramref name="lang"/> to the supplied <paramref name="schema"/>,
        /// resolving against the base language layer only.
        /// Properties whose keys are missing from the language resource are left as-is.
        /// </summary>
        /// <param name="schema">The schema to mutate. Must not be a shared cache instance — clone first.</param>
        /// <param name="lang">The BCP-47 language code (e.g. <c>"zh-TW"</c>); empty starts the chain at the default language.</param>
        public void Localize(FormSchema schema, string lang)
            => Localize(schema, string.Empty, lang);

        /// <summary>
        /// Tenant-customization-aware variant of <see cref="Localize(FormSchema, string)"/>: every
        /// sub-key is resolved through the customization overlay for <paramref name="customizeId"/>,
        /// so a tenant can rename individual captions and display names without copying the rest.
        /// </summary>
        /// <param name="schema">The schema to mutate. Must not be a shared cache instance — clone first.</param>
        /// <param name="customizeId">
        /// The tenant customization code, from <see cref="Polhem.Definition.Identity.SessionInfo.CustomizeId"/>. Empty resolves against
        /// the base layer only — identical to <see cref="Localize(FormSchema, string)"/>.
        /// </param>
        /// <param name="lang">The BCP-47 language code (e.g. <c>"zh-TW"</c>).</param>
        /// <remarks>
        /// The customization code must come from the server-side session, never from a client-supplied
        /// argument: it selects which tenant's customization files are read.
        /// </remarks>
        public void Localize(FormSchema schema, string customizeId, string lang)
        {
            ArgumentNullException.ThrowIfNull(schema);

            string @namespace = schema.ProgId;
            if (string.IsNullOrWhiteSpace(@namespace))
                return;

            // 1. Schema.DisplayName
            if (_languageService.TryResolveLangText(customizeId, lang, @namespace, SchemaDisplayNameKey, out string schemaName))
                schema.DisplayName = schemaName;

            // 2. Walk tables → fields.
            if (schema.Tables == null)
                return;

            foreach (var table in schema.Tables)
                LocalizeTable(table, customizeId, lang, @namespace);
        }

        private void LocalizeTable(FormTable table, string customizeId, string lang, string @namespace)
        {
            if (!string.IsNullOrWhiteSpace(table.TableName))
            {
                string tableKey = string.Format(System.Globalization.CultureInfo.InvariantCulture, TableDisplayNameKeyFormat, table.TableName);
                if (_languageService.TryResolveLangText(customizeId, lang, @namespace, tableKey, out string tableName))
                    table.DisplayName = tableName;
            }

            if (table.Fields == null)
                return;

            foreach (var field in table.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldName))
                    continue;
                string fieldKey = string.Format(System.Globalization.CultureInfo.InvariantCulture, FieldCaptionKeyFormat, field.FieldName);
                if (_languageService.TryResolveLangText(customizeId, lang, @namespace, fieldKey, out string caption))
                    field.Caption = caption;

                // Populate ListItems from the referenced LanguageEnum when LangEnumName is set.
                if (!string.IsNullOrWhiteSpace(field.LangEnumName))
                    ApplyLangEnum(field, customizeId, lang, @namespace);
            }
        }

        /// <summary>
        /// Resolves the field's <see cref="FormField.LangEnumName"/> against the language
        /// resource (through the fall-back chain) and replaces <see cref="FormField.ListItems"/>
        /// with the localized enum entries. Bare names (no dot) are resolved against
        /// the owning schema's <paramref name="schemaNamespace"/>; fully-qualified names
        /// (<c>"Common.Gender"</c>) target their own namespace.
        /// </summary>
        private void ApplyLangEnum(FormField field, string customizeId, string lang, string schemaNamespace)
        {
            string langEnumName = field.LangEnumName;
            string @namespace;
            string enumName;

            int dot = langEnumName.IndexOf('.');
            if (dot < 0)
            {
                // Bare name → use the owning schema's namespace.
                @namespace = schemaNamespace;
                enumName = langEnumName;
            }
            else
            {
                @namespace = langEnumName.Substring(0, dot);
                enumName = langEnumName.Substring(dot + 1);
            }

            var langEnum = _languageService.GetLangEnum(customizeId, lang, @namespace, enumName);
            if (langEnum is null)
                return; // missing translation → leave any existing ListItems untouched

            // Replace the field's ListItems with the resolved enum entries.
            field.ListItems!.Clear();
            foreach (var entry in langEnum.Entries)
                field.ListItems.Add(new ListItem(entry.Code, entry.Text));
        }
    }
}
