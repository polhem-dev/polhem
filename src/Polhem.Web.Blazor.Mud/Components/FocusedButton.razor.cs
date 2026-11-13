using System.ComponentModel;

namespace Polhem.Web.Blazor.Mud.Components
{
    /// <summary>
    /// A small MudBlazor button that takes the focus when it first renders. The content of an inline dialog renders in
    /// the dialog provider, after the component that placed the dialog, so only a component inside the content can move
    /// the focus there.
    /// </summary>
    /// <remarks>
    /// It belongs to this package's dialogs and is not meant for applications. It is public only because Razor places a
    /// component by its tag only when the type is public, and the class Razor generates for it is always public.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public partial class FocusedButton
    {
    }
}
