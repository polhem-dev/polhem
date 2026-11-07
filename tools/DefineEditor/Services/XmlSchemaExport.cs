using System.Text.Json;
using Polhem.Core;
using Polhem.Definition;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Writes the XML schema of each kind of definition file the editor opens, so another XML editor (VS Code with the
/// Red Hat XML extension, Rider, Visual Studio) can check and complete the files too.
/// </summary>
/// <remarks>
/// The schemas are the ones the editor's own completion uses (<see cref="DefineXmlSchema.ExportXsd"/>). Next to them
/// goes <see cref="AssociationsFileName"/>: the <c>xml.fileAssociations</c> setting of VS Code's XML extension, which
/// ties each file name pattern to its schema.
/// </remarks>
public static class XmlSchemaExport
{
    /// <summary>The name of the file that ties the definition file names to their schemas.</summary>
    public const string AssociationsFileName = "xml.fileAssociations.json";

    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };

    /// <summary>Writes the schemas and the associations file into <paramref name="folder"/>, and returns their paths.</summary>
    /// <param name="folder">The folder to write into; it is created when missing.</param>
    public static IReadOnlyList<string> Export(string folder)
    {
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        var associations = new List<Dictionary<string, string>>();

        foreach (var group in DefinePathScanner.FilePatterns.GroupBy(p => p.Type))
        {
            var rootType = group.Key.ToClrType();
            var path = Path.Combine(folder, rootType.Name + ".xsd");
            FileUtilities.FileWriteText(path, DefineXmlSchema.ExportXsd(rootType));
            written.Add(path);

            foreach (var (_, fileName) in group)
            {
                associations.Add(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["pattern"] = "**/" + fileName,
                    ["systemId"] = path,
                });
            }
        }

        var associationsPath = Path.Combine(folder, AssociationsFileName);
        FileUtilities.FileWriteText(associationsPath, JsonSerializer.Serialize(associations, s_json));
        written.Add(associationsPath);
        return written;
    }
}
