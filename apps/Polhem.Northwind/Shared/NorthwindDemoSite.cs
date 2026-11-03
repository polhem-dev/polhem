namespace Polhem.Northwind.Demo;

/// <summary>
/// The front ends the public demo site serves, each under its own path, and the links between them that every
/// sign-in screen shows there.
/// </summary>
/// <remarks>
/// This file is linked into each head the demo site serves, so the paths and the list exist once. The
/// <c>index.html</c> of each WASM head still writes its own path, because it sets the base address before any of the
/// application's code runs: change it together with the path here.
/// </remarks>
internal static class NorthwindDemoSite
{
    /// <summary>Where the Blazor Server head on MudBlazor is served: the site's root.</summary>
    public const string BlazorServerPath = "/";

    /// <summary>Where the Blazor Server head on Fluent UI is served.</summary>
    public const string FluentServerPath = "/fluent";

    /// <summary>Where the Blazor WebAssembly head on MudBlazor is served.</summary>
    public const string BlazorWasmPath = "/wasm";

    /// <summary>Where the Blazor WebAssembly head on Fluent UI is served.</summary>
    public const string FluentWasmPath = "/fluent-wasm";

    /// <summary>Where the Avalonia browser head is served.</summary>
    public const string BrowserPath = "/web";

    /// <summary>The heading over the links.</summary>
    public const string LinksTitle = "Try the same application on another front end:";

    /// <summary>Gets every front end, as the text and the address of its link.</summary>
    public static IReadOnlyList<(string Text, string Href)> FrontEnds { get; } =
    [
        ("MudBlazor · Blazor Server", BlazorServerPath),
        ("MudBlazor · WebAssembly", BlazorWasmPath + "/"),
        ("Fluent UI · Blazor Server", FluentServerPath + "/"),
        ("Fluent UI · WebAssembly", FluentWasmPath + "/"),
        ("Avalonia · WebAssembly", BrowserPath + "/"),
    ];

    /// <summary>Returns whether <paramref name="pagePath"/> is under the front end served at <paramref name="path"/>.</summary>
    /// <param name="pagePath">The path of the page's address.</param>
    /// <param name="path">The front end's path, such as <see cref="BlazorWasmPath"/>.</param>
    /// <returns>Whether the page belongs to that front end.</returns>
    public static bool IsUnder(string pagePath, string path)
        => string.Equals(pagePath, path, StringComparison.OrdinalIgnoreCase)
            || pagePath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase);
}
