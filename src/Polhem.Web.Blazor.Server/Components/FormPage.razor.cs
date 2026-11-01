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
    /// On a viewport narrower than <see cref="CompactWidthThreshold"/> the form switches to its compact layout
    /// (see <see cref="FormLayoutPresenterBase.IsCompact"/>), detected by a <see cref="CompactViewportObserver"/>.
    /// Setting <see cref="FormPageBase.IsCompact"/> fixes the layout instead, and the page then measures nothing.
    /// </remarks>
    public sealed partial class FormPage : FormPageBase
    {
        private ElementReference _cancelDeleteButton;

        /// <summary>
        /// Gets or sets the viewport width, in CSS pixels, below which the form renders in its compact layout
        /// while <see cref="FormPageBase.IsCompact"/> is <c>null</c>. Defaults to
        /// <see cref="CompactViewportObserver.DefaultCompactWidthThreshold"/>.
        /// </summary>
        [Parameter]
        public double CompactWidthThreshold { get; set; } = CompactViewportObserver.DefaultCompactWidthThreshold;

        /// <summary>
        /// Gets or sets the detail grids' editing model outside the compact layout, which always edits in
        /// <see cref="GridEditMode.EditForm"/>. Defaults to <see cref="GridEditMode.InCell"/>.
        /// </summary>
        [Parameter]
        public GridEditMode DetailEditMode { get; set; } = GridEditMode.InCell;

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
