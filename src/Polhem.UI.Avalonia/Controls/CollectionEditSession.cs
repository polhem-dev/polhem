using System.Collections;
using System.Globalization;
using Polhem.Core.Collections;
using Polhem.Core.Serialization;
using Polhem.Definition.Attributes;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The UI-free state of one collection being edited in <see cref="CollectionEditDialog"/>: the working list of
    /// items, the edits made to it, the check before it is written back, and the write-back itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The working list holds copies of the items, made by an XML round trip through <see cref="XmlCodec"/>, so a
    /// cancelled edit leaves the collection and its items untouched. The copies are not in any collection, which lets a
    /// keyed collection hold the same key twice while the user is still editing; <see cref="Validate"/> catches that
    /// before <see cref="Commit"/>.
    /// </para>
    /// <para>
    /// When an item cannot be copied, the working list holds the items themselves: edits to their properties apply at
    /// once and <see cref="IsCancelable"/> is <c>false</c>, so the dialog offers no Cancel.
    /// </para>
    /// <para>
    /// <see cref="Commit"/> replaces the collection's items with the working list, so after it the collection holds the
    /// copies, not the objects it held before.
    /// </para>
    /// </remarks>
    internal sealed class CollectionEditSession
    {
        private readonly List<object> _items;

        /// <summary>
        /// Starts editing <paramref name="collection"/>.
        /// </summary>
        /// <param name="collection">The collection to edit.</param>
        /// <param name="itemType">The type of its items.</param>
        internal CollectionEditSession(IList collection, Type itemType)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(itemType);
            Collection = collection;
            ItemType = itemType;
            IsKeyed = collection is IKeyCollectionBase;
            var originals = collection.Cast<object>().ToList();
            var copies = originals.Select(TryCopy).ToList();
            IsCancelable = copies.TrueForAll(c => c is not null);
            _items = IsCancelable ? copies! : originals;
        }

        /// <summary>Gets the collection being edited.</summary>
        internal IList Collection { get; }

        /// <summary>Gets the type of the collection's items.</summary>
        internal Type ItemType { get; }

        /// <summary>Gets whether the items have keys that must be unique.</summary>
        internal bool IsKeyed { get; }

        /// <summary>Gets whether the edit works on copies, so it can be cancelled.</summary>
        internal bool IsCancelable { get; }

        /// <summary>Gets the working list of items, in order.</summary>
        internal IReadOnlyList<object> Items => _items;

        /// <summary>Gets whether a new item can be created: the item type is a concrete class with a public parameterless constructor.</summary>
        internal bool CanAdd => CanCreate(ItemType);

        /// <summary>
        /// Creates an item and inserts it after <paramref name="index"/>, or at the end when it is out of range.
        /// </summary>
        /// <returns>The index of the new item.</returns>
        internal int Add(int index)
        {
            if (!CanAdd) { throw new InvalidOperationException($"Items of type {ItemType.Name} cannot be created."); }
            var item = Activator.CreateInstance(ItemType)!;
            if (item is IKeyCollectionItem keyed && string.IsNullOrEmpty(keyed.Key))
                keyed.Key = NextKey(ItemType.Name, _items.OfType<IKeyCollectionItem>().Select(i => i.Key));
            var position = index >= 0 && index < _items.Count ? index + 1 : _items.Count;
            _items.Insert(position, item);
            return position;
        }

        /// <summary>
        /// Removes the items at <paramref name="indices"/>.
        /// </summary>
        internal void Remove(IEnumerable<int> indices)
        {
            ArgumentNullException.ThrowIfNull(indices);
            foreach (var index in indices.Where(i => i >= 0 && i < _items.Count).Distinct().OrderByDescending(i => i))
                _items.RemoveAt(index);
        }

        /// <summary>
        /// Moves the item at <paramref name="index"/> by <paramref name="offset"/> places.
        /// </summary>
        /// <returns>The item's new index, or <paramref name="index"/> when the move would leave the list.</returns>
        internal int Move(int index, int offset)
        {
            var target = index + offset;
            if (index < 0 || index >= _items.Count || target < 0 || target >= _items.Count) { return index; }
            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(target, item);
            return target;
        }

        /// <summary>
        /// Returns the indices of the items whose key is missing or used by another item. Keys compare the way
        /// <see cref="KeyCollectionBase{T}"/> compares them, ignoring case.
        /// </summary>
        internal IReadOnlySet<int> GetInvalidIndices()
        {
            var invalid = new HashSet<int>();
            if (!IsKeyed) { return invalid; }
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] is not IKeyCollectionItem { Key: { } key })
                {
                    invalid.Add(i);
                    continue;
                }
                if (seen.TryGetValue(key, out var first))
                {
                    invalid.Add(first);
                    invalid.Add(i);
                }
                else
                {
                    seen[key] = i;
                }
            }
            return invalid;
        }

        /// <summary>
        /// Returns why the working list cannot be written back, or <c>null</c> when it can.
        /// </summary>
        internal string? Validate()
        {
            var invalid = GetInvalidIndices();
            if (invalid.Count == 0) { return null; }
            var key = _items[invalid.Min()] is IKeyCollectionItem { Key: { } k } ? k : null;
            return key is null
                ? UIText.Get(PolhemUIText.CollectionKeyMissing)
                : string.Format(CultureInfo.CurrentCulture,
                    UIText.Get(PolhemUIText.CollectionKeyDuplicate), key);
        }

        /// <summary>
        /// Replaces the items of <see cref="Collection"/> with the working list.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Validate"/> reports a problem.</exception>
        /// <remarks>
        /// The items are removed one at a time from the end: <see cref="CollectionBase{T}"/> and
        /// <see cref="KeyCollectionBase{T}"/> unlink an item from its collection only when it is removed that way, and
        /// <c>Clear()</c> would leave each old item pointing at the collection.
        /// </remarks>
        internal void Commit()
        {
            if (Validate() is { } error) { throw new InvalidOperationException(error); }
            for (var i = Collection.Count - 1; i >= 0; i--)
                Collection.RemoveAt(i);
            foreach (var item in _items)
                Collection.Add(item);
        }

        /// <summary>
        /// Returns the text the list shows for the item at <paramref name="index"/>: its <see cref="TreeNodeAttribute"/>
        /// label or its own <see cref="object.ToString"/>, else its key, else the type name and position.
        /// </summary>
        /// <param name="index">The position of the item in the working list.</param>
        /// <param name="translator">
        /// The translator of <see cref="PropertyGridControl.LabelTranslator"/>, which receives the display format as
        /// <see cref="PropertyGridTextKind.ItemLabel"/>; <c>null</c> uses the format as written.
        /// </param>
        internal string GetLabel(int index, Func<PropertyGridText, string?>? translator)
        {
            var item = _items[index];
            var itemType = item.GetType();
            var label = TreeNodeAttribute.GetDisplayText(item, translator is null
                ? null
                : format => PropertyGridMetadata.Translate(translator, PropertyGridTextKind.ItemLabel, itemType, null, format));
            if (string.IsNullOrEmpty(label) || label == item.GetType().ToString())
            {
                label = item is IKeyCollectionItem { Key: { Length: > 0 } key }
                    ? key
                    : string.Format(CultureInfo.InvariantCulture, "{0} #{1}", item.GetType().Name, index + 1);
            }
            return label;
        }

        /// <summary>
        /// Returns whether an item of <paramref name="itemType"/> can be created with a public parameterless constructor.
        /// </summary>
        internal static bool CanCreate(Type itemType)
        {
            ArgumentNullException.ThrowIfNull(itemType);
            return PropertyGridMetadata.IsEditableItemType(itemType) && !itemType.IsAbstract
                && itemType.GetConstructor(Type.EmptyTypes) is not null;
        }

        /// <summary>
        /// Returns <paramref name="prefix"/> followed by the smallest number from 1 up that makes a key none of
        /// <paramref name="existing"/> uses, ignoring case.
        /// </summary>
        internal static string NextKey(string prefix, IEnumerable<string> existing)
        {
            ArgumentNullException.ThrowIfNull(prefix);
            ArgumentNullException.ThrowIfNull(existing);
            var used = new HashSet<string>(existing.Where(k => k is not null), StringComparer.OrdinalIgnoreCase);
            for (var n = 1; ; n++)
            {
                var key = prefix + n.ToString(CultureInfo.InvariantCulture);
                if (!used.Contains(key)) { return key; }
            }
        }

        /// <summary>
        /// Returns a copy of <paramref name="item"/> made by an XML round trip, or <c>null</c> when its type cannot be
        /// serialized that way.
        /// </summary>
        internal static object? TryCopy(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            try
            {
                return XmlCodec.Deserialize(XmlCodec.Serialize(item), item.GetType());
            }
            // NOTE: XmlSerializer reports a type it cannot handle, and data it cannot read back, as
            // InvalidOperationException.
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
