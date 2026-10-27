using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Northwind.Blazor.Components.Layout;

/// <summary>
/// Turns the server's <see cref="MenuSettings"/> into the flat rows the navigation menu shows.
/// </summary>
/// <remarks>
/// The same rule the Avalonia shell applies: order and visibility come from
/// <see cref="MenuNodeCollection.GetDisplayNodes"/>, each folder becomes a header followed by its contents, depth
/// first, and a folder left with nothing to open is dropped. Captions follow the circuit's UI culture through the
/// menu's language resources when the definition loader is on.
/// </remarks>
public static class MenuRows
{
    /// <summary>
    /// Fetches the menu over the circuit's client and returns its rows.
    /// </summary>
    /// <param name="client">The circuit's client.</param>
    /// <param name="options">The package options, which decide whether captions are localized.</param>
    /// <param name="cancellationToken">A token that cancels the fetches.</param>
    public static async Task<IReadOnlyList<MenuRow>> LoadAsync(PolhemApiClient client, PolhemBlazorOptions options,
        CancellationToken cancellationToken = default)
    {
        var access = new ClientDefineAccess(client.System);
        var settings = await access.GetMenuSettingsAsync(cancellationToken).ConfigureAwait(true);
        string lang = CultureInfo.CurrentUICulture.Name;
        MenuLocalizer? localizer = options.UseDefinitionLoader
            ? await new FormDefinitionLoader(access, options.DefaultLanguage)
                .GetMenuLocalizerAsync(lang, cancellationToken).ConfigureAwait(true)
            : null;

        var rows = new List<MenuRow>();
        AddNodes(rows, settings.Items, node => localizer?.GetCaption(node, lang) ?? node.Caption);
        return rows;
    }

    private static void AddNodes(List<MenuRow> rows, MenuNodeCollection? nodes, Func<MenuNodeBase, string> caption)
    {
        if (nodes is null) return;

        foreach (var node in nodes.GetDisplayNodes())
        {
            switch (node)
            {
                case MenuFolder folder:
                    int headerIndex = rows.Count;
                    rows.Add(new MenuRow(caption(folder), null));
                    AddNodes(rows, folder.Items, caption);
                    if (rows.Count == headerIndex + 1)
                        rows.RemoveAt(headerIndex);
                    break;
                case MenuEntry entry when !string.IsNullOrWhiteSpace(entry.ProgId):
                    rows.Add(new MenuRow(caption(entry), entry.ProgId));
                    break;
            }
        }
    }
}
