using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Record-rendering half of <see cref="FormView"/>. The form body itself is drawn by a
    /// <see cref="FormLayoutPresenter"/>; the view hands it the layout, the data object and the responsive state.
    /// </summary>
    public partial class FormView
    {
        // ---- Record rendering (master sections + detail grids) ----

        private void Rebuild()
        {
            // Nothing renders until the record has both a layout and a data object.
            var ready = _formLayout is not null && _dataObject is not null;
            // Build against the current width so the first render (which may run before any Bounds
            // notification) already reflects the compact state; resize crossings re-enter via
            // ApplyResponsiveState.
            if (ready)
                _isCompact = IsCompactWidth(GetViewportWidth(), CompactWidthThreshold);
            _presenter.Update(p =>
            {
                p.DataObject = ready ? _dataObject : null;
                p.RoundingContext = _roundingContext;
                p.DetailEditMode = DetailEditMode;
                p.IsCompact = _isCompact;
                p.Layout = ready ? _formLayout : null;
            });
        }
    }
}
