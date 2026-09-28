using System.Data;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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
        public static async Task<DataRow?> ShowAsync(
            Visual host,
            string progId,
            FormSchema? schema = null,
            FormApiConnector? connector = null)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);

            schema ??= ClientInfo.DefinitionLoader is { } loader
                ? await loader.GetLocalizedSchemaAsync(progId, CultureInfo.CurrentUICulture.Name).ConfigureAwait(true)
                : await ClientInfo.DefineAccess.GetFormSchemaAsync(progId).ConfigureAwait(true);
            if (schema is null)
                throw new InvalidOperationException($"FormSchema '{progId}' was not found for lookup.");
            connector ??= ClientInfo.CreateFormApiConnector(progId);

            var panel = new LookupPanel();
            DataRow? selected = null;
            panel.Bind(schema, connector);
            // Fire-and-forget: load failures surface on the panel's error label,
            // and the dialog stays usable (retry via the search button).
            _ = panel.ReloadAsync();

            // Single-view hosts (browser, iOS, Android) cannot open a native Window, so the panel goes on
            // the top level's OverlayLayer instead. Only the desktop classic-window lifetime keeps the
            // native modal window.
            var topLevel = TopLevel.GetTopLevel(host);
            if (DialogHosting.GetWindowOwner(topLevel) is not { } owner)
            {
                var completed = new TaskCompletionSource();
                panel.Committed += (_, row) => { selected = row; completed.TrySetResult(); };
                panel.Cancelled += (_, _) => completed.TrySetResult();
                await OverlayDialogHost.ShowAsync(host, panel, schema.DisplayName, completed.Task);
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
            return selected;
        }
    }
}
