using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using Polhem.Web.Blazor.Server.Controls;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// A <see cref="FormPageBase"/> drawn with HTML: the list is a <see cref="DynamicGrid"/> and the record a
    /// <see cref="FormLayoutPresenter"/>.
    /// </summary>
    /// <remarks>
    /// On a viewport narrower than <see cref="FormPageBase.CompactWidthThreshold"/> the form switches to its compact layout
    /// (see <see cref="FormLayoutPresenterBase.IsCompact"/>), detected by a <see cref="CompactViewportObserver"/>.
    /// Setting <see cref="FormPageBase.IsCompact"/> fixes the layout instead, and the page then measures nothing.
    /// </remarks>
    public sealed partial class FormPage : FormPageBase
    {
        private ElementReference _cancelDeleteButton;

        /// <inheritdoc/>
        protected override async Task FocusDeleteCancelAsync(CancellationToken cancellationToken = default)
            => await _cancelDeleteButton.FocusAsync().ConfigureAwait(true);

        private void OnConfirmDeleteKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Escape") CancelDelete();
        }

        private async Task OnPageSizeChangedAsync(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageSize))
                await SetPageSizeAsync(pageSize).ConfigureAwait(true);
        }
    }
}
