using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Opening a relation field's record from a viewed record: the permission check, and the read-only view of the
    /// related record in a dialog.
    /// </summary>
    /// <remarks>
    /// A relation field in <see cref="Polhem.Definition.Layouts.SingleFormMode.View"/> raises its request on a
    /// double-tap. The view loads the related form's schema through <see cref="ResolveSchemaAsync"/> and asks
    /// <see cref="RelationOpenRules.CanOpen"/> whether the user's capability snapshot grants <c>Read</c> on it before it
    /// calls anything else; without it the view names the form in its message bar. The related view loads the record
    /// through the related form's <c>GetData</c>, which checks the permission and the record scope again on the
    /// server. A relation field in that view opens the next record the same way, in a dialog of its own.
    /// </remarks>
    public partial class FormView
    {
        private void OnRelationOpenRequested(object? sender, RelationOpenRequestedEventArgs e)
        {
            e.Handled = true;
            _ = OpenRelatedRecordAsync(e.Target);
        }

        /// <summary>
        /// Opens <paramref name="target"/> read-only when the user may read its form, and otherwise names the form in
        /// the message bar.
        /// </summary>
        /// <returns>A task that completes when the related record's dialog has closed, or at once when it did not open.</returns>
        internal async Task OpenRelatedRecordAsync(RelationTarget target)
        {
            ClearError();
            FormSchema? schema;
            try
            {
                schema = await ResolveSchemaAsync(target.ProgId, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // UI boundary: the request comes from a gesture handler, so a failure is shown, not thrown.
                ReportError(ex);
                return;
            }
            if (schema is null)
            {
                ReportError(new InvalidOperationException($"No FormSchema definition found for '{target.ProgId}'."));
                return;
            }

            if (!RelationOpenRules.CanOpen(schema, ResolveUserContext()?.Capabilities))
            {
                ShowMessage(string.Format(CultureInfo.CurrentCulture,
                    UIText.Get(PolhemUIText.RelatedRecordDenied), schema.DisplayName));
                return;
            }

            var view = CreateRelatedView(schema);
            view.UseCloseCommand();
            // The related view shows its own load failure in its message bar, so the dialog opens either way.
            _ = view.ViewAsync(target.RowId);
            await ShowRelatedViewAsync(view, schema.DisplayName, CancellationToken.None).ConfigureAwait(true);
        }

        /// <summary>
        /// Creates the view that shows a related record read-only. The default is a <see cref="FormView"/> over
        /// <paramref name="schema"/> that fetches its definitions from the same source as this view.
        /// </summary>
        /// <param name="schema">The related form's schema, already loaded for the permission check.</param>
        /// <remarks>
        /// Override to resolve the related view's services another way, for example in a subclass that also overrides
        /// the <c>Resolve*</c> hooks; the returned view then opens its own relation fields through its own override.
        /// </remarks>
        protected virtual FormView CreateRelatedView(FormSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            return new FormView
            {
                ProgId = schema.ProgId,
                Schema = schema,
                DefinitionSource = DefinitionSource,
                DetailEditMode = DetailEditMode,
                CompactWidthThreshold = CompactWidthThreshold,
            };
        }

        /// <summary>
        /// Shows <paramref name="view"/>, which is loading a related record, as a modal dialog until the user closes
        /// it. The default opens a native window under the desktop classic-window lifetime and an overlay on the top
        /// level everywhere else (browser, iOS, Android), as the lookup dialog does.
        /// </summary>
        /// <param name="view">The view <see cref="CreateRelatedView"/> returned.</param>
        /// <param name="title">The dialog title: the related form's display name.</param>
        /// <param name="cancellationToken">A token that closes the dialog as if the user had closed it.</param>
        /// <returns>A task that completes when the dialog has closed.</returns>
        protected virtual async Task ShowRelatedViewAsync(FormView view, string title,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(view);

            if (DialogHosting.GetWindowOwner(TopLevel.GetTopLevel(this)) is { } owner)
            {
                var window = new Window
                {
                    Title = title,
                    Content = view,
                    Width = 720,
                    Height = 560,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                };
                view.Closed += (_, _) => window.Close();
                // The callback can run on any thread, and the window is a control, so it is posted to the UI thread.
                using var windowRegistration = cancellationToken.Register(() => Dispatcher.UIThread.Post(window.Close));
                await window.ShowDialog(owner).ConfigureAwait(true);
                return;
            }

            var closed = new TaskCompletionSource();
            view.Closed += (_, _) => closed.TrySetResult();
            using var overlayRegistration = cancellationToken.Register(() => closed.TrySetResult());
            await OverlayDialogHost.ShowAsync(this, view, title, closed.Task, OverlayDialogHost.MaxCardWidth)
                .ConfigureAwait(true);
        }

        // A related record is dismissed rather than navigated back from, so its view says Close instead of Back.
        internal void UseCloseCommand() => _backButton.Content = UIText.Get(PolhemUIText.Close);
    }
}
