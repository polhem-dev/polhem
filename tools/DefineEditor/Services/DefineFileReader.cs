using Polhem.Core.Serialization;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Shared reading and labelling for the sidebar views that are built from the contents of define files.
/// </summary>
public static class DefineFileReader
{
    /// <summary>
    /// Deserializes <paramref name="path"/>, or returns <see langword="null"/> when the file is missing or cannot be
    /// read. A broken file leaves its view short of nodes; opening the file from the Explorer view reports why.
    /// </summary>
    public static T? TryRead<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return XmlCodec.DeserializeFromFile<T>(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The node text for an identifier and its display name: <c>"id - display name"</c>, or the identifier alone
    /// when the display name is empty or the same.
    /// </summary>
    public static string Label(string id, string? displayName)
        => string.IsNullOrWhiteSpace(displayName) || string.Equals(id, displayName, StringComparison.Ordinal)
            ? id
            : $"{id} - {displayName}";
}
