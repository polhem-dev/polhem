namespace Polhem.DefineEditor.ViewModels;

/// <summary>Shapes text a model is still writing for the places that show it while it arrives.</summary>
public static class StreamedText
{
    /// <summary>
    /// The end of <paramref name="text"/>, at most <paramref name="length"/> characters after an ellipsis, so text that
    /// keeps growing does not push the window open; the latest words are the ones worth reading.
    /// </summary>
    /// <param name="text">The text so far.</param>
    /// <param name="length">The most characters to keep.</param>
    /// <returns>The text trimmed, or its tail after "…".</returns>
    public static string Tail(string text, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return trimmed.Length <= length ? trimmed : "…" + trimmed[^length..].TrimStart();
    }
}
