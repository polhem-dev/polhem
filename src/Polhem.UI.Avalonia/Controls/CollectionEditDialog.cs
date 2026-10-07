using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// A modal dialog that edits a collection property: the items in a list with Add, Delete, Move up and Move down, and
    /// a <see cref="PropertyGridControl"/> for the selected item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog edits copies of the items, made by an XML round trip, and OK replaces the collection's items with
    /// them, so after OK the collection holds new item objects; a caller that keeps references to the old items must
    /// read them again. Cancel, or closing the dialog, leaves the collection untouched. When an item cannot be copied
    /// that way, the dialog edits the items themselves and has no Cancel.
    /// </para>
    /// <para>
    /// A new item is created with the item type's public parameterless constructor; Add is disabled for an abstract
    /// item type. In a keyed collection a new item gets the key "&lt;item type name&gt;&lt;n&gt;" with the first free
    /// number, and OK refuses a key that is missing or used twice, compared without regard to case.
    /// </para>
    /// <para>
    /// The dialog is a native window on the desktop and an overlay on the browser, iOS and Android, like
    /// <see cref="RowEditDialog"/>.
    /// </para>
    /// </remarks>
    public static class CollectionEditDialog
    {
        private const double WindowWidth = 760;
        private const double WindowHeight = 520;

        /// <summary>
        /// Opens the dialog for the collection <paramref name="context"/> describes and returns whether the collection
        /// changed.
        /// </summary>
        /// <param name="host">A visual inside the owning window; used to resolve the dialog owner.</param>
        /// <param name="context">The collection to edit.</param>
        /// <param name="cancellationToken">
        /// A token that closes an open dialog as if the user had cancelled it. A cancelled call throws
        /// <see cref="OperationCanceledException"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> when the user chose OK, or when the dialog edited the items themselves and so may have changed
        /// them; otherwise <c>false</c>.
        /// </returns>
        public static Task<bool> ShowAsync(Visual host, CollectionEditContext context, CancellationToken cancellationToken = default)
            => ShowAsync(host, context, null, cancellationToken);

        /// <summary>
        /// Opens the dialog with its title, item labels and item grid following the settings of
        /// <paramref name="source"/>: its <see cref="PropertyGridControl.LabelTranslator"/>,
        /// <see cref="PropertyGridControl.ValueSuggestionProvider"/> and
        /// <see cref="PropertyGridControl.CollectionEditorProvider"/>.
        /// </summary>
        internal static async Task<bool> ShowAsync(Visual host, CollectionEditContext context, PropertyGridControl? source,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            var session = new CollectionEditSession(context.Collection, context.ItemType);
            var title = PropertyGridMetadata.Translate(source?.LabelTranslator, PropertyGridTextKind.DisplayName,
                context.Component.GetType(), context.Property.Name, context.Property.DisplayName);
            var topLevel = TopLevel.GetTopLevel(host);
            var owner = DialogHosting.GetWindowOwner(topLevel);
            // The overlay card is narrower than the two columns need, so it always stacks them.
            var compact = owner is null || RowEditPanel.IsCompactWidth(topLevel?.Bounds.Width ?? 0);
            var panel = new CollectionEditPanel(session, source, compact);
            var committed = false;
            panel.Committed += (_, _) => committed = true;
            // The token closes the dialog the way Cancel does. The callback can run on any thread, and the panel is a
            // control, so it is posted to the UI thread.
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(panel.Cancel));

            if (owner is null)
            {
                var completed = new TaskCompletionSource();
                panel.Committed += (_, _) => completed.TrySetResult();
                panel.Cancelled += (_, _) => completed.TrySetResult();
                await OverlayDialogHost.ShowAsync(host, panel, title, completed.Task);
            }
            else
            {
                var window = new Window
                {
                    Title = title,
                    Content = panel,
                    Width = WindowWidth,
                    Height = WindowHeight,
                    MinWidth = RowEditPanel.PreferredMinWidth,
                    MinHeight = 320,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = true,
                    ShowInTaskbar = false,
                };
                panel.Committed += (_, _) => window.Close();
                panel.Cancelled += (_, _) => window.Close();
                await window.ShowDialog(owner);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return committed || !session.IsCancelable;
        }
    }
}
