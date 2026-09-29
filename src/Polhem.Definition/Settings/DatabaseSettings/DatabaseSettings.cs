using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Attributes;
using Polhem.Core.Serialization;
using System.Text.Json.Serialization;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Database settings.
    /// </summary>
    [Description("Database settings.")]
    [TreeNode("Database Settings")]
    public sealed class DatabaseSettings : IObjectSerializeFile
    {
        private DatabaseServerCollection? _servers = null;
        private DatabaseItemCollection? _items = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="DatabaseSettings"/>.
        /// </summary>
        public DatabaseSettings()
        {
        }

        #endregion

        #region IObjectSerializeFile Interface

        /// <summary>
        /// Gets the file path bound to serialization.
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        [Browsable(false)]
        public string ObjectFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the file path bound to serialization.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void SetObjectFilePath(string filePath)
        {
            ObjectFilePath = filePath;
        }

        #endregion

        /// <summary>
        /// Gets the time at which this object was created.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public DateTime CreateTime { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the database server collection.
        /// </summary>
        [Description("Database server collection.")]
        [DefaultValue(null)]
        public DatabaseServerCollection? Servers
        {
            get
            {
                if (_servers == null) { _servers = []; }
                return _servers;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Servers"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ServersSpecified => _servers is { Count: > 0 };

        /// <summary>
        /// Gets the database connection settings collection.
        /// </summary>
        [Description("Database connection settings collection.")]
        [DefaultValue(null)]
        public DatabaseItemCollection? Items
        {
            get
            {
                if (_items == null) { _items = []; }
                return _items;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Items"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ItemsSpecified => _items is { Count: > 0 };

        /// <summary>
        /// Creates a copy of this instance.
        /// </summary>
        public DatabaseSettings Clone()
        {
            var copy = new DatabaseSettings();

            foreach (var server in Servers!)
                copy.Servers!.Add(server.Clone());

            foreach (var item in Items!)
                copy.Items!.Add(item.Clone());

            return copy;
        }

    }
}
