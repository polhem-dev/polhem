using Avalonia;
using Avalonia.Controls;
using Polhem.Definition.Settings;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Attached property carrying the <see cref="PermissionActions"/> a command control (typically a
    /// toolbar button) requires. The framework tags buttons at creation; the view then asks the
    /// element capability resolver whether the action is permitted and hides the control when it is
    /// not. The default <see cref="PermissionActions.None"/> means the control is not
    /// permission-controlled (opt-in), so untagged controls always render.
    /// </summary>
    public static class PermissionScope
    {
        /// <summary>Identifies the <c>Action</c> attached property.</summary>
        public static readonly AttachedProperty<PermissionActions> ActionProperty =
            AvaloniaProperty.RegisterAttached<Control, PermissionActions>(
                "Action", typeof(PermissionScope), PermissionActions.None);

        /// <summary>Sets the required permission action on the control.</summary>
        /// <param name="control">The target control.</param>
        /// <param name="value">The action(s) the control requires.</param>
        public static void SetAction(Control control, PermissionActions value) => control.SetValue(ActionProperty, value);

        /// <summary>Gets the required permission action on the control.</summary>
        /// <param name="control">The target control.</param>
        public static PermissionActions GetAction(Control control) => control.GetValue(ActionProperty);
    }
}
