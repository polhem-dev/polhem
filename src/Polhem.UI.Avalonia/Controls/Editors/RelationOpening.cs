using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// How a relation field asks the form around it to open the record it points at: the form marks its subtree with
    /// <see cref="IsEnabledProperty"/>, and an editor inside raises <see cref="RequestedEvent"/>, which bubbles up to it.
    /// </summary>
    /// <remarks>
    /// <see cref="Polhem.UI.Avalonia.Views.FormView"/> is the form that does both. The flag lets an editor offer the
    /// double-click, with its hint and cursor, only where something will answer it, as the Blazor head offers it only
    /// when its scope carries a handler.
    /// </remarks>
    internal static class RelationOpening
    {
        /// <summary>
        /// Identifies the inherited flag that says the controls below handle <see cref="RequestedEvent"/>.
        /// </summary>
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(RelationOpening), inherits: true);

        /// <summary>
        /// Identifies the bubbling event an editor raises to open the record its relation field points at.
        /// </summary>
        public static readonly RoutedEvent<RelationOpenRequestedEventArgs> RequestedEvent =
            RoutedEvent.Register<RelationOpenRequestedEventArgs>("RelationOpenRequested", RoutingStrategies.Bubble, typeof(RelationOpening));

        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);
    }
}
