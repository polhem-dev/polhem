using Microsoft.FluentUI.AspNetCore.Components;

namespace Polhem.Web.Blazor.Fluent.Controls
{
    /// <summary>
    /// The icons the package draws, from the Fluent System Icons (regular, 20px).
    /// </summary>
    /// <remarks>
    /// NOTE: The paths are copied so the package does not reference the Fluent UI icon package, which carries every icon
    /// and is several megabytes for the handful used here. Each property returns a new instance because an
    /// <see cref="Icon"/> can be changed after it is created, for example by <see cref="Icon.WithColor(string)"/>.
    /// </remarks>
    internal static class PolhemIcons
    {
        public static Icon Search => Create(nameof(Search),
            "<path d=\"M13.73 14.44a6.5 6.5 0 1 1 .7-.7l3.42 3.4a.5.5 0 0 1-.63.77l-.07-.06zm-.71-.71A5.5 5.5 0 0 0 15 9.5a5.5 5.5 0 1 0-1.98 4.23\"/>");

        public static Icon Add => Create(nameof(Add),
            "<path d=\"M10 2.5c.28 0 .5.22.5.5v6.5H17a.5.5 0 0 1 0 1h-6.5V17a.5.5 0 0 1-1 0v-6.5H3a.5.5 0 0 1 0-1h6.5V3c0-.28.22-.5.5-.5\"/>");

        public static Icon Edit => Create(nameof(Edit),
            "<path d=\"M17.18 2.93a2.97 2.97 0 0 0-4.26-.06l-9.37 9.38q-.5.5-.66 1.2l-.88 3.94a.5.5 0 0 0 .6.6l3.93-.87c.46-.1.9-.34 1.23-.68l9.36-9.36a2.97 2.97 0 0 0 .05-4.15m-3.55.65a1.97 1.97 0 1 1 2.8 2.8l-.68.66-2.8-2.79zm-1.38 1.38 2.8 2.8-7.99 7.97q-.31.31-.74.41l-3.16.7.7-3.18q.1-.41.4-.7z\"/>");

        public static Icon Delete => Create(nameof(Delete),
            "<path d=\"M8.5 4h3a1.5 1.5 0 0 0-3 0m-1 0a2.5 2.5 0 0 1 5 0h5a.5.5 0 0 1 0 1h-1.05l-1.2 10.34A3 3 0 0 1 12.27 18H7.73a3 3 0 0 1-2.98-2.66L3.55 5H2.5a.5.5 0 0 1 0-1zM5.74 15.23A2 2 0 0 0 7.73 17h4.54a2 2 0 0 0 1.99-1.77L15.44 5H4.56zM8.5 7.5c.28 0 .5.22.5.5v6a.5.5 0 0 1-1 0V8c0-.28.22-.5.5-.5M12 8a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0z\"/>");

        private static Icon Create(string name, string content) => new(name, IconVariant.Regular, IconSize.Size20, content);
    }
}
