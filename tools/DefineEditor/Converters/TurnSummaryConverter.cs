using System.Globalization;
using Avalonia.Data.Converters;
using Polhem.Chat;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.Converters;

/// <summary>
/// The line that ends a turn of an assistant's conversation: its seconds and tokens, with the part read from the
/// provider's cache, and whether it was stopped or failed.
/// </summary>
/// <remarks>
/// Replaces the chat view's own summary, which leaves out the cached tokens; the provider charges those less, and the
/// drafting prompts are written to be cached.
/// </remarks>
public sealed class TurnSummaryConverter : IValueConverter
{
    public static TurnSummaryConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TurnSummaryEntry summary ? Describe(summary) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>The line for a turn, in the editor's language.</summary>
    public static string Describe(TurnSummaryEntry summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var parts = new List<string> { Format("ChatSummary_Seconds", (int)Math.Round(summary.Elapsed.TotalSeconds)) };
        if (summary.Usage is { } usage && (usage.InputTokenCount is > 0 || usage.OutputTokenCount is > 0))
        {
            var input = Count(usage.InputTokenCount);
            var output = Count(usage.OutputTokenCount);
            parts.Add(usage.CachedInputTokenCount is > 0
                ? Format("ChatSummary_TokensCached", input, Count(usage.CachedInputTokenCount), output)
                : Format("ChatSummary_Tokens", input, output));
        }
        if (summary.Outcome == TurnOutcome.Stopped)
            parts.Add(LocalizationService.Current["ChatSummary_Stopped"]);
        else if (summary.Outcome == TurnOutcome.Failed)
            parts.Add(LocalizationService.Current["ChatSummary_Failed"]);
        return string.Join(LocalizationService.Current["ChatSummary_Separator"], parts);
    }

    private static string Count(long? count) => (count ?? 0).ToString("N0", CultureInfo.InvariantCulture);

    private static string Format(string key, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, LocalizationService.Current[key], args);
}
