namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// A <see cref="LoginPanelBase"/> drawn as an HTML form. Pair it with <see cref="PolhemAccessTokenProvider"/> to keep
    /// the access token cascading through the component tree.
    /// </summary>
    /// <remarks>
    /// The panel ships with a no-frills default look so it can drop in unstyled; hosts that want richer UI should
    /// provide their own login page and call <see cref="PolhemAccessTokenProvider.SetToken"/> directly.
    /// </remarks>
    public sealed partial class PolhemLoginPanel : LoginPanelBase
    {
    }
}
