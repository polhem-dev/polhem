using System.ComponentModel;

namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// A UI-independent tree node that stands for an object, or for a collection shown as a folder.
    /// </summary>
    /// <remarks>
    /// Nodes are usually created by <see cref="ObjectTreeBuilder"/>. A UI head binds <see cref="Label"/> and
    /// <see cref="Children"/> to its own tree control; both raise change notifications.
    /// </remarks>
    public sealed class ObjectTreeNode : INotifyPropertyChanged
    {
        private readonly Func<object, string>? _labelProvider;
        private string _label;

        /// <summary>
        /// Initializes a new instance of <see cref="ObjectTreeNode"/> with a fixed label.
        /// </summary>
        /// <param name="value">The object the node stands for.</param>
        /// <param name="label">The text the node shows; <see cref="Refresh"/> keeps it.</param>
        /// <param name="isFolder">Whether the node groups the items of a collection rather than standing for an object.</param>
        public ObjectTreeNode(object value, string label, bool isFolder)
            : this(value, label, isFolder, null)
        { }

        internal ObjectTreeNode(object value, string label, bool isFolder, Func<object, string>? labelProvider)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(label);
            Value = value;
            IsFolder = isFolder;
            _label = label;
            _labelProvider = labelProvider;
            Children = new ObjectTreeNodeCollection(this);
        }

        /// <summary>
        /// Gets the object the node stands for. For a folder node it is the collection whose items are the children.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets whether the node groups the items of a collection rather than standing for an object.
        /// </summary>
        public bool IsFolder { get; }

        /// <summary>
        /// Gets the text the node shows.
        /// </summary>
        public string Label
        {
            get { return _label; }
            private set
            {
                if (string.Equals(_label, value, StringComparison.Ordinal)) { return; }
                _label = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }

        /// <summary>
        /// Gets the node whose <see cref="Children"/> contain this node, or <c>null</c> for a root.
        /// </summary>
        public ObjectTreeNode? Parent { get; internal set; }

        /// <summary>
        /// Gets the child nodes. Adding a node sets its <see cref="Parent"/>, and removing it clears it.
        /// </summary>
        public ObjectTreeNodeCollection Children { get; }

        /// <summary>
        /// Occurs when <see cref="Label"/> changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Recomputes <see cref="Label"/> from <see cref="Value"/>, for example after the object was edited.
        /// </summary>
        /// <remarks>
        /// A node created by <see cref="ObjectTreeBuilder"/> recomputes its label the way the builder did. A node
        /// created with a fixed label keeps it. The children are not refreshed.
        /// </remarks>
        public void Refresh()
        {
            if (_labelProvider != null)
                Label = _labelProvider(Value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Label;
        }
    }
}
