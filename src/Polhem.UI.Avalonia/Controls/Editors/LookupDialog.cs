using System.Data;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Polhem.Api.Client.Connectors;
using Polhem.Definition.Forms;
using Polhem.UI.Core;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Modal presentation of <see cref="LookupPanel"/>: opens the lookup picker for
    /// a target program and returns the selected row, or <c>null</c> when the user
    /// cancels. Schema and connector default to the ambient <see cref="ClientInfo"/>
    /// wiring; pass them explicitly to bypass it (tests, custom hosts).
    /// </summary>
    /// <remarks>
    /// The default schema is localized through <see cref="ClientInfo.DefinitionLoader"/> unless the
    /// host turned off <see cref="ClientInfo.UseDefinitionLoader"/>, so the dialog title and columns
    /// read in the same language as the form that opened it.
    /// </remarks>
    public static class LookupDialog
    {
        /// <summary>
        /// Opens the lookup picker for <paramref name="progId"/> and returns the
        /// selected row, or <c>null</c> when the user cancels.
        /// </summary>
        /// <param name="host">A visual inside the owning window; used to resolve the dialog owner.</param>
        /// <param name="progId">The target program identifier (the lookup source form).</param>
        /// <param name="schema">
        /// The target form's schema; <c>null</c> loads it through <see cref="ClientInfo.DefinitionLoader"/>
        /// in the UI culture when one is in effect, else as stored through <see cref="ClientInfo.DefineAccess"/> (cached).
        /// </param>
        /// <param name="connector">The connector for the target form; <c>null</c> creates one through <see cref="ClientInfo.CreateFormApiConnector"/>.</param>
        /// <param name="cancellationToken">
        /// A token that cancels the schema fetch and the row load, and closes an open dialog as if the
        /// user had cancelled it. A cancelled call throws <see cref="OperationCanceledException"/>.
        /// </param>
        public static async Task<DataRow?> ShowAsync(
            Visual host,
            string progId,
            FormSchema? schema = null,
            FormApiConnector? connector = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);

            schema ??= ClientInfo.DefinitionLoader is { } loader
                ? await loader.GetLocalizedSchemaAsync(progId, CultureInfo.CurrentUICulture.Name, cancellationToken).ConfigureAwait(true)
                : await ClientInfo.DefineAccess.GetFormSchemaAsync(progId, cancellationToken).ConfigureAwait(true);
            if (schema is null)
                throw new InvalidOperationException($"FormSchema '{progId}' was not found for lookup.");
            connector ??= ClientInfo.CreateFormApiConnector(progId);
            cancellationToken.ThrowIfCancellationRequested();

            var panel = new LookupPanel();
            DataRow? selected = null;
            panel.Bind(schema, connector);
            // Fire-and-forget: load failures surface on the panel's error label, and the dialog stays
            // usable (retry via the search button). A cancellation ends the task as cancelled rather
            // than faulted, and the same cancellation closes the dialog and is rethrown below.
            _ = panel.ReloadAsync(cancellationToken);
            // The token closes the dialog the way the Cancel button does. The callback can run on
            // any thread, and the panel is a control, so it is posted to the UI thread.
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(panel.Cancel));

            // Single-view hosts (browser, iOS, Android) cannot open a native Window, so the panel goes on
            // the top level's OverlayLayer instead. Only the desktop classic-window lifetime keeps the
            // native modal window.
            var topLevel = TopLevel.GetTopLevel(host);
            if (DialogHosting.GetWindowOwner(topLevel) is not { } owner)
            {
                var completed = new TaskCompletionSource();
                panel.Committed += (_, row) => { selected = row; completed.TrySetResult(); };
                panel.Cancelled += (_, _) => completed.TrySetResult();
                await OverlayDialogHost.ShowAsync(host, panel, schema.DisplayName, completed.Task,
                    LookupPanel.PreferredMinWidth);
                cancellationToken.ThrowIfCancellationRequested();
                return selected;
            }

            var window = new Window
            {
                Title = schema.DisplayName,
                Content = panel,
                Width = 520,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            panel.Committed += (_, row) => { selected = row; window.Close(); };
            panel.Cancelled += (_, _) => window.Close();

            await window.ShowDialog(owner);
            cancellationToken.ThrowIfCancellationRequested();
            return selected;
        }
    }
}
