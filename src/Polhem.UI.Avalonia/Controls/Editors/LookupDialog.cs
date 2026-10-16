using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Forms;
using Polhem.UI.Core;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Modal presentation of <see cref="LookupPanel"/>: opens the lookup picker for
    /// a target program and returns the selected row, or <c>null</c> when the user
    /// cancels. Schema and connector default to the services registered in
    /// <see cref="UIServices.Provider"/>; pass them explicitly to bypass it (tests, custom hosts).
    /// </summary>
    /// <remarks>
    /// The default schema comes from the registered <see cref="IFormDefinitionSource"/>, the same source the
    /// form that opened the dialog uses, so the dialog title and columns read in the same language.
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
        /// The target form's schema; <c>null</c> loads it through the registered <see cref="IFormDefinitionSource"/>.
        /// </param>
        /// <param name="connector">
        /// The connector for the target form; <c>null</c> creates one through the registered
        /// <see cref="IFormApiConnectorFactory"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that cancels the schema fetch and the row load, and closes an open dialog as if the
        /// user had cancelled it. A cancelled call throws <see cref="OperationCanceledException"/>.
        /// </param>
        public static async Task<DataRow?> ShowAsync(
            Visual host,
            string progId,
            FormSchema? schema = null,
            IFormApiConnector? connector = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);

            if (schema is null && UIServices.Get<IFormDefinitionSource>() is { } source)
                schema = await source.GetFormSchemaAsync(progId, cancellationToken).ConfigureAwait(true);
            if (schema is null)
                throw new InvalidOperationException($"FormSchema '{progId}' was not found for lookup.");
            connector ??= UIServices.Get<IFormApiConnectorFactory>()?.Create(progId)
                ?? throw new InvalidOperationException(
                    $"No connector for lookup '{progId}': pass one, or register an {nameof(IFormApiConnectorFactory)}.");
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
