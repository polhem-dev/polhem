namespace Polhem.DefineEditor.ViewModels;

/// <summary>What draws the FormLayout editor's live preview.</summary>
/// <remarks>
/// The members are stored by name in <see cref="Services.UserSettings.FormLayoutPreviewEngine"/>. A setting written
/// before the web preview was split into its two packages says <c>Web</c>, which
/// <see cref="Services.DocumentViewModelFactory"/> reads as <see cref="Mud"/>.
/// </remarks>
public enum FormLayoutPreviewEngine
{
    /// <summary>The framework's Avalonia controls, drawn by the editor itself.</summary>
    Avalonia,

    /// <summary>
    /// The browser preview page drawn with the MudBlazor package (<c>Polhem.Web.Blazor.Mud</c>), shown in a web view
    /// inside the editor by the system's web engine.
    /// </summary>
    Mud,

    /// <summary>
    /// The browser preview page drawn with the Fluent UI package (<c>Polhem.Web.Blazor.Fluent</c>), shown in a web view
    /// inside the editor by the system's web engine.
    /// </summary>
    Fluent,
}
