using Avalonia.Interactivity;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// The arguments of <see cref="RelationOpening.RequestedEvent"/>: the record a relation field asks to open.
    /// </summary>
    internal sealed class RelationOpenRequestedEventArgs : RoutedEventArgs
    {
        public RelationOpenRequestedEventArgs(RelationTarget target)
            : base(RelationOpening.RequestedEvent)
        {
            Target = target;
        }

        public RelationTarget Target { get; }
    }
}
