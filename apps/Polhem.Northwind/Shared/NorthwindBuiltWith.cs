using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polhem.Northwind.Demo;

namespace Polhem.Northwind.Shell;

/// <summary>
/// The line under the sign-in screen of the Blazor heads that says the application is built with Polhem, with the name
/// linked to the framework's repository.
/// </summary>
/// <remarks>
/// The sign-in screen belongs to the framework's shell, which has no place for it, so each head's layout draws this
/// under the shell's layout while nobody is signed in. The text follows the UI culture, which the sign-in screen's language
/// switch sets.
/// </remarks>
internal sealed class NorthwindBuiltWith : ComponentBase
{
    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var chinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "northwind-built-with");
        builder.AddAttribute(2, "style", "margin: -24px 16px 24px; text-align: center; font-size: 13px; opacity: 0.75;");
        builder.AddContent(3, chinese ? "由 " : "Built with ");
        builder.OpenElement(4, "a");
        builder.AddAttribute(5, "href", NorthwindDemoSite.SourceRepositoryUrl);
        builder.AddAttribute(6, "target", "_blank");
        builder.AddAttribute(7, "rel", "noopener");
        builder.AddContent(8, "Polhem");
        builder.CloseElement();
        builder.AddContent(9, chinese ? " 打造" : string.Empty);
        builder.CloseElement();
    }
}
