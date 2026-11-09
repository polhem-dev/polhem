namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>
/// The Blazor UI package the browser preview page draws a layout with, carried in the page URL's <c>ui</c> query.
/// </summary>
public enum BrowserPreviewUi
{
    /// <summary>The MudBlazor package, <c>Polhem.Web.Blazor.Mud</c>; the page's default.</summary>
    Mud,

    /// <summary>The Fluent UI package, <c>Polhem.Web.Blazor.Fluent</c>.</summary>
    Fluent,
}
