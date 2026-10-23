using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Shared caption convention for field state, applied uniformly to master form field
    /// captions and detail grid headers. A required field's caption is blue. A read-only field
    /// is marked by parenthesising its caption (<see cref="FormatCaption"/>) — a theme-independent
    /// cue that reads on both light and dark variants, unlike a colour — while master form fields
    /// additionally show the editor's read-only underline. Read-only wins when a field is both:
    /// a read-only field cannot be filled in, so the required colour is suppressed.
    /// </summary>
    internal static class FieldCaptionStyle
    {
        // Required: blue. The dark variant needs a lighter blue: the light one falls below a
        // readable contrast on a dark background.
        private static readonly ImmutableSolidColorBrush s_requiredBrush =
            new(Color.FromRgb(0x25, 0x63, 0xEB));
        private static readonly ImmutableSolidColorBrush s_requiredDarkBrush =
            new(Color.FromRgb(0x60, 0xA5, 0xFA));

        /// <summary>
        /// Returns the caption foreground for the given field state, or <c>null</c> to use the
        /// theme default. Required fields are blue; read-only fields use the theme default (their
        /// cue is the parenthesised caption from <see cref="FormatCaption"/>, plus the editor's
        /// read-only underline on master fields). Read-only suppresses the required colour.
        /// </summary>
        /// <param name="readOnly">Whether the field is read-only.</param>
        /// <param name="required">Whether the field is required (mandatory input).</param>
        /// <param name="themeVariant">The caption's theme variant; the dark variant gets a lighter blue.</param>
        public static IBrush? GetCaptionForeground(bool readOnly, bool required, ThemeVariant? themeVariant = null)
        {
            if (!required || readOnly) return null;
            return themeVariant == ThemeVariant.Dark ? s_requiredDarkBrush : s_requiredBrush;
        }

        /// <summary>
        /// Colours <paramref name="caption"/> for the given field state and keeps the colour in step
        /// with the theme variant the caption ends up in, including a later light/dark switch.
        /// </summary>
        /// <param name="caption">The caption text block.</param>
        /// <param name="readOnly">Whether the field is read-only.</param>
        /// <param name="required">Whether the field is required (mandatory input).</param>
        public static void ApplyCaptionForeground(TextBlock caption, bool readOnly, bool required)
        {
            if (GetCaptionForeground(readOnly, required) is null) return;
            void Update() => caption.Foreground = GetCaptionForeground(readOnly, required, caption.ActualThemeVariant);
            Update();
            caption.ActualThemeVariantChanged += (_, _) => Update();
        }

        /// <summary>
        /// Marks a read-only field's caption by wrapping it in parentheses (e.g. <c>Amount</c> →
        /// <c>(Amount)</c>); returns the caption unchanged when the field is editable or empty.
        /// </summary>
        /// <param name="caption">The field caption.</param>
        /// <param name="readOnly">Whether the field is read-only.</param>
        public static string FormatCaption(string caption, bool readOnly)
            => readOnly && !string.IsNullOrEmpty(caption) ? $"({caption})" : caption;
    }
}
