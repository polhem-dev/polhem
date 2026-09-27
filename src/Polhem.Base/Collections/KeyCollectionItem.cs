using System.ComponentModel;
using System.Xml.Serialization;
using System.Text.Json.Serialization;
using Polhem.Base.Attributes;
using Polhem.Base.Serialization;

namespace Polhem.Base.Collections
{
    /// <summary>
    /// Base class for strongly-typed keyed collection items.
    /// </summary>
    public abstract class KeyCollectionItem : IKeyCollectionItem, ITagProperty, IObjectSerializeBase
    {
        private string _key = string.Empty;
        private IKeyCollectionBase? _collection;

        #region IKeyCollectionItem Interface

        /// <summary>
        /// Gets or sets the key of this item.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public virtual string Key
        {
            get
            {
                return _key;
            }
            set
            {
                if (_key != value)
                {
                    // Notify the owning collection of the key change
                    if (_collection != null && !StringUtilities.IsEquals(_key, value))
                        _collection.ChangeItemKey(value, this);
                    _key = value;
                }
            }
        }

        /// <summary>
        /// Sets the collection that owns this item.
        /// </summary>
        /// <param name="collection">The owning collection.</param>
        public void SetCollection(IKeyCollectionBase? collection)
        {
            _collection = collection;
        }

        /// <summary>
        /// Removes this item from its owning collection.
        /// </summary>
        public void Remove()
        {
            if (_collection != null)
                _collection.Remove(this);
        }

        #endregion

        #region ITagProperty Interface

        /// <summary>
        /// Gets or sets an arbitrary object for storing additional information.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public object? Tag { get; set; }

        #endregion

        /// <summary>
        /// Gets the collection that owns this item.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [TreeNodeIgnore]
        public IKeyCollectionBase? Collection
        {
            get { return _collection; }
        }
    }
}
