using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// Turns the server's <see cref="MenuSettings"/> into the flat rows the shell's menu shows.
    /// </summary>
    /// <remarks>
    /// The same rule the Avalonia shell applies: order and visibility come from
    /// <see cref="MenuNodeCollection.GetDisplayNodes"/>, each folder becomes a header followed by its contents, depth
    /// first, and a folder left with nothing to open is dropped. Captions follow the circuit's UI culture through the
    /// menu's language resources when the definition loader is on.
    /// </remarks>
    internal static class ShellMenuRows
    {
        /// <summary>
        /// Fetches the menu over the circuit's client and returns its rows.
        /// </summary>
        /// <param name="client">The circuit's client.</param>
        /// <param name="options">The package options, which decide whether captions are localized.</param>
        /// <param name="cancellationToken">A token that cancels the fetches.</param>
        public static async Task<IReadOnlyList<ShellMenuRow>> LoadAsync(PolhemApiClient client, PolhemBlazorOptions options,
            CancellationToken cancellationToken = default)
        {
            var access = new ClientDefineAccess(client.System);
            var settings = await access.GetMenuSettingsAsync(cancellationToken).ConfigureAwait(true);
            string lang = CultureInfo.CurrentUICulture.Name;
            MenuLocalizer? localizer = options.UseDefinitionLoader
                ? await new FormDefinitionLoader(access, options.DefaultLanguage)
                    .GetMenuLocalizerAsync(lang, cancellationToken).ConfigureAwait(true)
                : null;

            var rows = new List<ShellMenuRow>();
            AddNodes(rows, settings.Items, node => localizer?.GetCaption(node, lang) ?? node.Caption);
            return rows;
        }

        /// <summary>
        /// Loads the rows like <see cref="LoadAsync"/>, or returns the message of the failure instead of throwing it.
        /// </summary>
        /// <remarks>
        /// A component that lets the exception escape its initialization tears the circuit down, so the shell's
        /// components show the message instead.
        /// </remarks>
        public static async Task<(IReadOnlyList<ShellMenuRow>? Rows, string? Error)> TryLoadAsync(PolhemApiClient client,
            PolhemBlazorOptions options, CancellationToken cancellationToken = default)
        {
            try
            {
                return (await LoadAsync(client, options, cancellationToken).ConfigureAwait(true), null);
            }
            catch (Exception ex)
            {
                // A UI boundary, like the other components': the failure may be a user message, a refusal, a
                // JSON-RPC error or an unreachable server, and each is reported by its message.
                return (null, ex.Message);
            }
        }

        internal static void AddNodes(List<ShellMenuRow> rows, MenuNodeCollection? nodes, Func<MenuNodeBase, string> caption)
        {
            if (nodes is null) return;

            foreach (var node in nodes.GetDisplayNodes())
            {
                switch (node)
                {
                    case MenuFolder folder:
                        int headerIndex = rows.Count;
                        rows.Add(new ShellMenuRow(caption(folder), null));
                        AddNodes(rows, folder.Items, caption);
                        if (rows.Count == headerIndex + 1)
                            rows.RemoveAt(headerIndex);
                        break;
                    case MenuEntry entry when !string.IsNullOrWhiteSpace(entry.ProgId):
                        rows.Add(new ShellMenuRow(caption(entry), entry.ProgId));
                        break;
                }
            }
        }
    }
}
