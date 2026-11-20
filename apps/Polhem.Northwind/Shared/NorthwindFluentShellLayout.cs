using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polhem.Web.Blazor.Identity;
using Polhem.Web.Blazor.Fluent.Shell;

namespace Polhem.Northwind.Shell;

/// <summary>
/// The shell's layout on Fluent UI, with <see cref="NorthwindBuiltWith"/> under the sign-in screen.
/// </summary>
internal sealed class NorthwindFluentShellLayout : PolhemShellLayout
{
    [Inject]
    private BlazorUserContext SignInState { get; set; } = default!;

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        base.BuildRenderTree(builder);
        // The shell's layout shows its sign-in screen while no company has been entered; the same test keeps the line
        // with that screen.
        if (SignInState.Company is null)
        {
            builder.OpenComponent<NorthwindBuiltWith>(1000);
            builder.CloseComponent();
        }
    }
}
