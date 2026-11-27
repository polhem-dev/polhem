using System.Globalization;
using Avalonia;
using Polhem.Chat;
using Polhem.Chat.Avalonia;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.Views;

/// <summary>Keeps a chat view's own texts (send, stop, thinking, copy) in the editor's language.</summary>
/// <remarks>
/// The view's default texts follow the UI culture of the thread that created it, which is not the editor's language:
/// the editor keeps its language in <see cref="LocalizationService.Culture"/>, and Avalonia 12.1 resets the UI thread's
/// culture after each dispatcher operation. So the texts are set from the editor's language while the view is shown, and
/// again whenever the language changes.
/// </remarks>
internal static class ChatLanguage
{
    /// <summary>Sets the view's texts in the editor's language while it is shown.</summary>
    /// <param name="view">The chat view.</param>
    /// <param name="placeholderKey">The string resource of the prompt box's placeholder, or <see langword="null"/> for the view's own.</param>
    public static void Follow(ChatView view, string? placeholderKey = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        void Apply(object? sender, CultureInfo culture) => view.Strings = Create(culture, placeholderKey);
        view.AttachedToVisualTree += (_, _) =>
        {
            Apply(null, LocalizationService.Current.Culture);
            LocalizationService.Current.CultureChanged += Apply;
        };
        view.DetachedFromVisualTree += (_, _) => LocalizationService.Current.CultureChanged -= Apply;
    }

    /// <summary>The view's texts in a culture, with the placeholder of <paramref name="placeholderKey"/>.</summary>
    public static ChatStrings Create(CultureInfo culture, string? placeholderKey)
    {
        var strings = ChatStrings.ForCulture(culture);
        if (placeholderKey is not null)
            strings.PromptPlaceholder = LocalizationService.Current[placeholderKey];
        return strings;
    }
}
