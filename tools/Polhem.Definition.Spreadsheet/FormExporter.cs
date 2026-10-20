using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Gathers what a form workbook shows from a definition folder: the FormSchema, its zh-TW texts and its
/// place in the menu.
/// </summary>
public static class FormExporter
{
    private const string ZhTw = "zh-TW";

    /// <summary>
    /// Loads the form <paramref name="progId"/> from <paramref name="definePath"/>.
    /// </summary>
    /// <param name="definePath">The definition folder (the folder that holds <c>FormSchema/</c>).</param>
    /// <param name="progId">The program id of the form.</param>
    /// <returns>The content to pass to <see cref="FormWorkbookWriter.Write"/>.</returns>
    /// <exception cref="FileNotFoundException">The form has no FormSchema file.</exception>
    public static FormWorkbookContent Load(string definePath, string progId)
    {
        ArgumentNullException.ThrowIfNull(definePath);
        ArgumentNullException.ThrowIfNull(progId);
        var paths = new PathOptions { DefinePath = Path.GetFullPath(definePath) };

        var schemaPath = paths.GetFormSchemaFilePath(progId);
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException($"No FormSchema file for '{progId}'.", schemaPath);
        var schema = XmlCodec.DeserializeFromFile<FormSchema>(schemaPath)
            ?? throw new InvalidOperationException($"The FormSchema file for '{progId}' is empty.");

        var content = new FormWorkbookContent(schema);
        var formTexts = LoadLanguage(paths.GetLanguageFilePath(ZhTw, schema.ProgId));
        if (formTexts is not null)
        {
            foreach (var item in formTexts.Items)
                content.ZhTw[item.Key] = item.Value;
        }

        LoadMenu(paths, schema.ProgId, content.Menu);
        return content;
    }

    private static void LoadMenu(PathOptions paths, string progId, MenuPlacement menu)
    {
        var menuPath = paths.GetMenuSettingsFilePath();
        if (!File.Exists(menuPath)) return;
        var settings = XmlCodec.DeserializeFromFile<MenuSettings>(menuPath);
        if (settings?.Items is null) return;

        var (entry, folder) = Find(settings.Items, null, progId);
        if (entry is null) return;

        var texts = LoadLanguage(paths.GetLanguageFilePath(ZhTw, MenuLocalizer.Namespace));
        menu.EntryCaption = entry.Caption;
        menu.EntryCaptionZhTw = Text(texts, MenuLocalizer.EntryCaptionKeyFormat, entry.Id);
        if (folder is not null)
        {
            menu.FolderId = folder.Id;
            menu.FolderCaption = folder.Caption;
            menu.FolderCaptionZhTw = Text(texts, MenuLocalizer.FolderCaptionKeyFormat, folder.Id);
        }
    }

    private static (MenuEntry? Entry, MenuFolder? Folder) Find(MenuNodeCollection nodes, MenuFolder? parent, string progId)
    {
        foreach (var node in nodes)
        {
            if (node is MenuEntry entry && string.Equals(entry.ProgId, progId, StringComparison.OrdinalIgnoreCase))
                return (entry, parent);
            if (node is MenuFolder { Items: not null } folder)
            {
                var found = Find(folder.Items, folder, progId);
                if (found.Entry is not null) return found;
            }
        }
        return (null, null);
    }

    private static LanguageResource? LoadLanguage(string path)
        => File.Exists(path) ? XmlCodec.DeserializeFromFile<LanguageResource>(path) : null;

    private static string Text(LanguageResource? resource, string format, string id)
    {
        var key = FormWorkbookWriter.Key(format, id);
        return resource is not null && resource.Items.Contains(key) ? resource.Items[key].Value : string.Empty;
    }
}
