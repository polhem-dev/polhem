using Polhem.Core;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Turns a form read from a workbook into the definition files a runnable form needs: the FormSchema, a
/// TableSchema per table, the FormLayout, the database category, program and menu registrations, and the
/// zh-TW language resources.
/// </summary>
public static class FormImporter
{
    private const string ZhTw = "zh-TW";

    /// <summary>
    /// Applies <see cref="FormSchemaConventions"/> to <paramref name="content"/>, validates the form and works
    /// out every file to write, without writing anything.
    /// </summary>
    /// <param name="content">The form read from a workbook; the conventions change it in place.</param>
    /// <param name="options">Where to write and how.</param>
    /// <returns>The plan; it lists no files when there is any error.</returns>
    public static FormImportPlan Plan(FormWorkbookContent content, FormImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        var issues = new List<DefinitionIssue>(FormSchemaConventions.Apply(content));
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
            return new FormImportPlan(issues, []);

        var schema = content.Schema;
        var paths = new PathOptions { DefinePath = Path.GetFullPath(options.DefinePath) };
        var progIds = ExistingProgIds(paths).Append(schema.ProgId);
        issues.AddRange(FormSchemaValidator.Validate(schema, progIds).Select(i => Locate(i, content)));
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
            return new FormImportPlan(issues, []);

        var files = new List<PlannedFile>
        {
            PlanDefinition(paths.GetFormSchemaFilePath(schema.ProgId), schema, $"FormSchema {schema.ProgId}"),
        };

        var categoryId = TableSchemaGenerator.GetCategoryId(schema);
        foreach (var table in schema.Tables!)
        {
            files.Add(PlanTableSchema(paths.GetTableSchemaFilePath(categoryId, table.DbTableName),
                $"TableSchema/{categoryId}/{table.DbTableName}.TableSchema.xml",
                table.GenerateDbTable(), $"TableSchema {categoryId}/{table.DbTableName}", options.TableSchemaMode));
        }

        var layoutPath = paths.GetFormLayoutFilePath(schema.ProgId);
        files.Add(File.Exists(layoutPath) && !options.RegenerateLayout
            ? new PlannedFile(layoutPath, PlannedFileAction.Skip,
                $"FormLayout {schema.ProgId}: kept, it may have been adjusted by hand (regenerate it on request)", null)
            : PlanDefinition(layoutPath, FormLayoutGenerator.Generate(schema, schema.ProgId), $"FormLayout {schema.ProgId}"));

        files.Add(PlanSettings(paths.GetDbCategorySettingsFilePath(), "DbCategorySettings",
            $"DbCategorySettings: register the tables under '{categoryId}'",
            doc => schema.Tables.Aggregate(false, (changed, table) =>
                SettingsXmlEditor.AddTableItem(doc, categoryId, table.DbTableName, table.DisplayName) | changed)));

        var menuCaption = FirstNonEmpty(content.Menu.EntryCaption, schema.DisplayName, schema.ProgId);
        files.Add(PlanSettings(paths.GetProgramSettingsFilePath(), "ProgramSettings",
            $"ProgramSettings: register '{schema.ProgId}'",
            doc => SettingsXmlEditor.AddProgramItem(doc, schema.ProgId, menuCaption)));

        var entryId = MenuEntryId(schema.ProgId);
        var menuFile = PlanSettings(paths.GetMenuSettingsFilePath(), "MenuSettings",
            $"MenuSettings: add '{schema.ProgId}' to the menu",
            doc => SettingsXmlEditor.AddMenuEntry(doc, content.Menu.FolderId, content.Menu.FolderCaption,
                entryId, schema.ProgId, menuCaption));
        files.Add(menuFile);

        if (content.ZhTw.Count > 0)
        {
            files.Add(PlanSettings(paths.GetLanguageFilePath(ZhTw, schema.ProgId), "LanguageResource",
                $"Language {ZhTw}/{schema.ProgId}",
                doc => SettingsXmlEditor.MergeLanguageItems(doc, schema.ProgId, ZhTw, content.ZhTw.OrderBy(p => p.Key, StringComparer.Ordinal))));
        }

        var menuTexts = MenuTranslations(content, entryId, entryAdded: menuFile.Writes);
        if (menuTexts.Count > 0)
        {
            files.Add(PlanSettings(paths.GetLanguageFilePath(ZhTw, MenuLocalizer.Namespace), "LanguageResource",
                $"Language {ZhTw}/{MenuLocalizer.Namespace}",
                doc => SettingsXmlEditor.MergeLanguageItems(doc, MenuLocalizer.Namespace, ZhTw, menuTexts)));
        }

        return new FormImportPlan(issues, files);
    }

    /// <summary>
    /// Writes the files of a plan.
    /// </summary>
    /// <param name="plan">A plan from <see cref="Plan"/>.</param>
    /// <exception cref="InvalidOperationException">The plan has errors.</exception>
    public static void Apply(FormImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.HasErrors)
            throw new InvalidOperationException("The import plan has errors; nothing was written.");
        foreach (var file in plan.Files.Where(f => f.Writes))
            FileUtilities.FileWriteText(file.Path, file.Content!);
    }

    /// <summary>The menu entry id for a ProgId: <c>AuditRule</c> becomes <c>audit-rule</c>.</summary>
    /// <param name="progId">The program id.</param>
    /// <returns>The menu entry id.</returns>
    public static string MenuEntryId(string progId)
        => FormSchemaConventions.ToSnakeCase(progId).Replace('_', '-');

    private static IEnumerable<string> ExistingProgIds(PathOptions paths)
    {
        var folder = Path.GetDirectoryName(paths.GetFormSchemaFilePath("x"))!;
        if (!Directory.Exists(folder)) return [];
        const string suffix = ".FormSchema.xml";
        return Directory.EnumerateFiles(folder, "*" + suffix)
            .Select(f => Path.GetFileName(f)[..^suffix.Length]);
    }

    /// <summary>
    /// Plans a file fully generated from a definition object. When the existing file already describes the
    /// same definition it stays untouched, so re-importing an unchanged workbook keeps its comments.
    /// </summary>
    private static PlannedFile PlanDefinition<T>(string path, T definition, string description) where T : class
    {
        var xml = XmlCodec.Serialize(definition);
        if (!File.Exists(path))
            return new PlannedFile(path, PlannedFileAction.Create, description, xml);

        var existing = XmlCodec.DeserializeFromFile<T>(path);
        return existing is not null && XmlCodec.Serialize(existing) == xml
            ? new PlannedFile(path, PlannedFileAction.Unchanged, description, null)
            : new PlannedFile(path, PlannedFileAction.Overwrite, description, xml);
    }

    /// <summary>
    /// Plans one TableSchema file.
    /// </summary>
    /// <remarks>
    /// A framework table such as <c>st_employee</c> has columns the framework reads that are not on its form
    /// (<c>user_rowid</c>, which sign-in uses). When the file is created or overwritten, the framework's own
    /// TableSchema for that table (<see cref="Defaults"/>) is the base, and the form's columns are merged into it;
    /// generating such a table from the form alone breaks sign-in.
    /// </remarks>
    private static PlannedFile PlanTableSchema(
        string path, string relativePath, TableSchema generated, string description, TableSchemaImportMode mode)
    {
        bool exists = File.Exists(path);
        if (!exists || mode == TableSchemaImportMode.Overwrite)
        {
            if (!Defaults.ListEmbedded().Contains(relativePath, StringComparer.Ordinal))
                return PlanDefinition(path, generated, description);

            System.Xml.Linq.XDocument document;
            using (var stream = Defaults.OpenEmbedded(relativePath))
                document = SettingsXmlEditor.Load(stream);
            SettingsXmlEditor.MergeTableSchema(document, XmlCodec.Serialize(generated));
            var xml = SettingsXmlEditor.Serialize(document);
            description += ": the framework's TableSchema with the form's columns";
            if (!exists)
                return new PlannedFile(path, PlannedFileAction.Create, description, xml);
            return File.ReadAllText(path) == xml
                ? new PlannedFile(path, PlannedFileAction.Unchanged, description, null)
                : new PlannedFile(path, PlannedFileAction.Overwrite, description, xml);
        }

        if (mode == TableSchemaImportMode.Keep)
        {
            var unchanged = PlanDefinition(path, generated, description);
            return unchanged.Action == PlannedFileAction.Unchanged
                ? unchanged
                : new PlannedFile(path, PlannedFileAction.Skip, $"{description}: kept as it is", null);
        }

        return PlanSettings(path, "TableSchema", $"{description}: add the columns and indexes the form brings",
            document => SettingsXmlEditor.MergeTableSchema(document, XmlCodec.Serialize(generated)));
    }

    private static PlannedFile PlanSettings(string path, string rootName, string description, Func<System.Xml.Linq.XDocument, bool> edit)
    {
        bool exists = File.Exists(path);
        var document = SettingsXmlEditor.LoadOrCreate(path, rootName);
        if (!edit(document))
            return new PlannedFile(path, PlannedFileAction.Unchanged, description, null);
        return new PlannedFile(path, exists ? PlannedFileAction.Update : PlannedFileAction.Create, description,
            SettingsXmlEditor.Serialize(document));
    }

    /// <summary>
    /// The zh-TW menu texts to set. The form's zh-TW display name stands in for a missing entry caption only when
    /// this import adds the entry; an entry that already exists keeps whatever text it has.
    /// </summary>
    private static List<KeyValuePair<string, string>> MenuTranslations(FormWorkbookContent content, string entryId, bool entryAdded)
    {
        var texts = new List<KeyValuePair<string, string>>();
        var fallback = entryAdded && content.ZhTw.TryGetValue(FormSchemaLocalizer.SchemaDisplayNameKey, out var name)
            ? name
            : string.Empty;
        var entryText = FirstNonEmpty(content.Menu.EntryCaptionZhTw, fallback);
        if (entryText.Length > 0)
            texts.Add(new(FormWorkbookWriter.Key(MenuLocalizer.EntryCaptionKeyFormat, entryId), entryText));
        if (content.Menu.FolderId.Length > 0 && content.Menu.FolderCaptionZhTw.Length > 0)
            texts.Add(new(FormWorkbookWriter.Key(MenuLocalizer.FolderCaptionKeyFormat, content.Menu.FolderId), content.Menu.FolderCaptionZhTw));
        return texts;
    }

    /// <summary>
    /// Points a validator issue at the workbook row the field came from: <c>Order.customer_rowid.Relation</c>
    /// becomes <c>Fields!12 (Order.customer_rowid.Relation)</c>.
    /// </summary>
    private static DefinitionIssue Locate(DefinitionIssue issue, FormWorkbookContent content)
    {
        var parts = issue.Location.Split('.');
        return parts.Length >= 2 && content.FieldSources.TryGetValue($"{parts[0]}.{parts[1]}", out var source)
            ? issue with { Location = $"{source} ({issue.Location})" }
            : issue;
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
