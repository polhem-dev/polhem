using System.Globalization;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Drafting;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Turns a form drafted from documents into what the workbook import takes, and draws the FormLayout that import
/// would write, so the drafting window previews the form the same way the files come out.
/// </summary>
public static class FormDraftImport
{
    /// <summary>
    /// Copies a converted draft into the content the workbook import plans from. The schema is copied, so applying the
    /// form conventions to the content leaves <paramref name="result"/> as it was.
    /// </summary>
    /// <param name="result">The converted draft.</param>
    /// <returns>The content; it has no field sources, which name workbook rows.</returns>
    public static FormWorkbookContent ToContent(FormDraftResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var content = new FormWorkbookContent(result.Schema.Clone());
        foreach (var (key, text) in result.ZhTw)
            content.ZhTw[key] = text;
        content.Menu.FolderId = result.Menu.FolderId;
        content.Menu.FolderCaption = result.Menu.FolderCaption;
        content.Menu.FolderCaptionZhTw = result.Menu.FolderCaptionZhTw;
        content.Menu.EntryCaption = result.Menu.EntryCaption;
        content.Menu.EntryCaptionZhTw = result.Menu.EntryCaptionZhTw;
        return content;
    }

    /// <summary>Converts the findings of the workbook import into the issues a drafting session reports.</summary>
    /// <param name="issues">The findings.</param>
    /// <returns>One draft issue per finding.</returns>
    public static IReadOnlyList<DraftIssue> ToDraftIssues(IEnumerable<DefinitionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return issues
            .Select(issue => new DraftIssue(
                issue.Severity == IssueSeverity.Error ? DraftIssueSeverity.Error : DraftIssueSeverity.Warning,
                issue.Location, issue.Message))
            .ToList();
    }

    /// <summary>
    /// Draws the FormLayout of a converted draft: the one the import generates for a new form, or, when the solution
    /// already has a FormLayout for the ProgId, a copy of it completed with the fields it lacks, as the import's merge
    /// mode writes it.
    /// </summary>
    /// <param name="result">The converted draft.</param>
    /// <param name="definePath">The definition folder of the solution; empty draws every form as new.</param>
    /// <param name="zhTw">Whether to show the zh-TW texts of the draft instead of the English ones.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="InvalidOperationException">The existing FormLayout cannot be read.</exception>
    public static FormLayout BuildPreviewLayout(FormDraftResult result, string definePath, bool zhTw)
    {
        ArgumentNullException.ThrowIfNull(result);
        var content = ToContent(result);
        // The conventions add the relation fields a lookup brings (ref_*), which the form shows. A draft they reject,
        // such as one whose ProgId does not survive conversion, is still drawn as the converter left it.
        FormSchemaConventions.Apply(content);
        var schema = content.Schema;
        if (zhTw)
            ApplyTexts(schema, content.ZhTw);

        if (definePath.Length > 0 && schema.ProgId != FormDraftConverter.PlaceholderProgId)
        {
            var layoutPath = new PathOptions { DefinePath = Path.GetFullPath(definePath) }.GetFormLayoutFilePath(schema.ProgId);
            if (File.Exists(layoutPath))
            {
                var layout = XmlCodec.DeserializeFromFile<FormLayout>(layoutPath)
                    ?? throw new InvalidOperationException($"The FormLayout file cannot be read: {layoutPath}");
                FormLayoutGenerator.Merge(layout, schema);
                return layout;
            }
        }
        return FormLayoutGenerator.Generate(schema, schema.ProgId);
    }

    private static void ApplyTexts(FormSchema schema, IDictionary<string, string> texts)
    {
        if (texts.TryGetValue(FormSchemaLocalizer.SchemaDisplayNameKey, out var formName))
            schema.DisplayName = formName;
        foreach (var table in schema.Tables!)
        {
            if (texts.TryGetValue(Key(FormSchemaLocalizer.TableDisplayNameKeyFormat, table.TableName), out var tableName))
                table.DisplayName = tableName;
            foreach (var field in table.Fields!)
            {
                if (texts.TryGetValue(Key(FormSchemaLocalizer.FieldCaptionKeyFormat, field.FieldName), out var caption))
                    field.Caption = caption;
            }
        }
    }

    private static string Key(string format, string name) => string.Format(CultureInfo.InvariantCulture, format, name);
}
