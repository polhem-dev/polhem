using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

// File → Export XML Schemas...: the schemas of the definition files, for XML editors other than this one.
public partial class MainWindowViewModel
{
    /// <summary>Writes the XML schemas of the definition files into <paramref name="folder"/> and reports it in the status bar.</summary>
    public void ExportXmlSchemas(string folder)
    {
        try
        {
            var written = XmlSchemaExport.Export(folder);
            StatusText = L("Status_XmlSchemasExported", written.Count(p => p.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase)),
                folder, XmlSchemaExport.AssociationsFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = L("Status_XmlSchemasExportFailed", ex.Message);
        }
    }
}
