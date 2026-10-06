using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Serialization;

namespace Polhem.Core.Collections
{
    /// <summary>
    /// Base class for strongly-typed collection items.
    /// </summary>
    public abstract class CollectionItem : ICollectionItem, ITagProperty, IObjectSerializeBase
    {
        private ICollectionBase? _collection;

        #region ICollectionItem Interface

        /// <summary>
        /// Sets the collection that owns this item.
        /// </summary>
        /// <param name="collection">The owning collection.</param>
        public void SetCollection(ICollectionBase? collection)
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
        public ICollectionBase? Collection
        {
            get { return _collection; }
        }
    }
}
