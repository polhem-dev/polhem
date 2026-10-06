using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Definition.Attributes;
using Polhem.Core.Serialization;
using System.Text.Json.Serialization;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Database category settings.
    /// </summary>
    [Description("Database category settings.")]
    [TreeNode("Database Categories")]
    public sealed class DbCategorySettings : IObjectSerializeFile
    {
        private DbCategoryCollection? _categories = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="DbCategorySettings"/>.
        /// </summary>
        public DbCategorySettings()
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
        /// Gets the database category collection.
        /// </summary>
        [Description("Database category collection.")]
        [DefaultValue(null)]
        public DbCategoryCollection? Categories
        {
            get
            {
                if (_categories == null) { _categories = new DbCategoryCollection(this); }
                return _categories;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Categories"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool CategoriesSpecified => _categories is { Count: > 0 };
    }
}
