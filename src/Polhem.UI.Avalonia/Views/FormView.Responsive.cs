using Avalonia;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Responsive-layout half of <see cref="FormView"/> (compact viewport detection, layout
    /// re-evaluation and error surface). Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class FormView
    {
        // ---- Responsive layout (compact = phone-sized viewport) ----

        /// <summary>
        /// Reacts to the inputs of the responsive layout decision. <see cref="Visual.Bounds"/> is
        /// a direct property whose change notifications are not delivered to static class handlers,
        /// so the width reaction is wired here (the same pattern OverlayDialogHost uses) rather than
        /// in the static constructor.
        /// </summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty || change.Property == CompactWidthThresholdProperty)
            {
                ApplyResponsiveState();
            }
            else if (change.Property == DetailEditModeProperty && !_isCompact)
            {
                // The preferred detail mode only takes effect on wide layouts; a compact layout
                // forces EditForm regardless, so only a wide form needs to re-render here.
                RebuildIfReady();
            }
        }

        /// <summary>
        /// Pure decision: a positive width below <paramref name="compactWidthThreshold"/> is a
        /// compact (phone-sized) viewport. A non-positive width means "not measured yet", so the
        /// layout stays in its wide form until the first layout pass.
        /// </summary>
        internal static bool IsCompactWidth(double viewportWidth, double compactWidthThreshold)
            => viewportWidth > 0 && viewportWidth < compactWidthThreshold;

        /// <summary>
        /// Gets the viewport width used for the compact-layout decision. Defaults to the
        /// control's own <see cref="Visual.Bounds"/> width; overridden in tests to drive the
        /// responsive switch without a real layout pass.
        /// </summary>
        protected virtual double GetViewportWidth() => Bounds.Width;

        /// <summary>
        /// Recomputes the compact state from the current viewport width and rebuilds the form only
        /// when the <see cref="CompactWidthThreshold"/> boundary is crossed, so the frequent
        /// within-band <see cref="Visual.Bounds"/> ticks during layout cost a single comparison.
        /// </summary>
        protected void ApplyResponsiveState()
        {
            var compact = IsCompactWidth(GetViewportWidth(), CompactWidthThreshold);
            if (compact == _isCompact) return;
            _isCompact = compact;
            RebuildIfReady();
        }

        private void RebuildIfReady()
        {
            if (_formLayout is not null && _dataObject is not null)
                Rebuild();
        }

        private void ReportError(Exception ex)
        {
            _errorLabel.Text = ex.Message;
            _errorLabel.IsVisible = true;
            ErrorOccurred?.Invoke(this, ex);
        }

        // A message for the user rather than a failure: unlike `ReportError` it raises no
        // `ErrorOccurred`, because nothing went wrong that a host needs to log.
        private void ShowMessage(string message)
        {
            _errorLabel.Text = message;
            _errorLabel.IsVisible = true;
        }

        private void ClearError()
        {
            _errorLabel.Text = string.Empty;
            _errorLabel.IsVisible = false;
        }
    }
}
