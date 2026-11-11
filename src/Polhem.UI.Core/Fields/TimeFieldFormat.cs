using Polhem.Core;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Write-back rule for a time-of-day field, shared by the time editors of every UI head.
    /// </summary>
    /// <remarks>
    /// A time of day is stored as a fixed-width <c>HH:mm</c> string (ADR-033).
    /// </remarks>
    public static class TimeFieldFormat
    {
        /// <summary>
        /// Resolves the value a time editor writes back for the text the user left in it.
        /// </summary>
        /// <param name="text">The editor's text.</param>
        /// <param name="current">The value the editor holds now, in the stored <c>HH:mm</c> form.</param>
        /// <returns>
        /// An empty string for an emptied box; the normalised <c>HH:mm</c> form for text that parses; otherwise
        /// <paramref name="current"/>, unchanged.
        /// </returns>
        /// <remarks>
        /// An emptied box is an explicit "unset". The empty string is how an unfilled time of day is stored, because
        /// midnight is a legal value and cannot stand in for it. Text that does not parse is not passed on: the time
        /// column's coercion would throw a <see cref="FormatException"/> for it inside the change handler.
        /// </remarks>
        public static string ResolveWriteBack(string? text, string current)
        {
            if (string.IsNullOrWhiteSpace(text)) { return string.Empty; }
            string normalized = ValueUtilities.CTimeString(text);
            return StringUtilities.IsNotEmpty(normalized) ? normalized : current;
        }
    }
}
