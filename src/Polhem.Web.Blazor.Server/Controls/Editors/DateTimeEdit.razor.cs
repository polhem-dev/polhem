using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DateTimeEdit"/>: an instant shown as a date with its time
    /// of day. Editing uses the browser's date-and-time input; the read-only state shows the value in the
    /// circuit's culture, the way the grid shows it.
    /// </summary>
    /// <remarks>
    /// The Avalonia head uses a culture-formatted text input instead, because a popup picker breaks inside
    /// its grid. A browser's <c>datetime-local</c> input has no such problem and exchanges the invariant
    /// <c>yyyy-MM-ddTHH:mm:ss</c> form in both directions.
    /// </remarks>
    public sealed partial class DateTimeEdit : DateTimeEditorBase
    {
    }
}
