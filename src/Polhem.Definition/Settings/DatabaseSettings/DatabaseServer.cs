using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Definition.Attributes;
using Polhem.Core.Collections;
using Polhem.Definition.Database;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A database server configuration.
    /// </summary>
    [Description("Database server.")]
    [TreeNode("{0} - {1}", "Id,DisplayName")]
    public sealed class DatabaseServer : KeyCollectionItem
    {
        /// <summary>
        /// Gets or sets the server ID.
        /// </summary>
        [XmlAttribute]
        [Description("Server ID.")]
        [Category(PropertyCategories.Data)]
        public string Id
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [XmlAttribute]
        [Description("Display name.")]
        [Category(PropertyCategories.Data)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the database type.
        /// </summary>
        [XmlAttribute]
        [Description("Database type.")]
        [Category("Connection")]
        public DatabaseType DatabaseType { get; set; } = DatabaseType.SQLServer;

        /// <summary>
        /// Gets or sets the database connection string.
        /// </summary>
        [XmlAttribute]
        [Description("Database connection string.")]
        [DefaultValue("")]
        [Category("Connection")]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the login user ID, which replaces the {@UserId} placeholder in the connection string.
        /// </summary>
        [XmlAttribute]
        [Description("Login user ID, which replaces the {@UserId} placeholder in the connection string.")]
        [DefaultValue("")]
        [Category("Connection")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the login password, which replaces the {@Password} placeholder in the connection string.
        /// </summary>
        [XmlAttribute]
        [Description("Login password, which replaces the {@Password} placeholder in the connection string.")]
        [PasswordPropertyText(true)]
        [DefaultValue("")]
        [Category("Connection")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Creates a copy of this instance.
        /// </summary>
        public DatabaseServer Clone()
        {
            return new DatabaseServer()
            {
                Id = Id,
                DisplayName = DisplayName,
                DatabaseType = DatabaseType,
                ConnectionString = ConnectionString,
                UserId = UserId,
                Password = Password
            };
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{Id} - {DisplayName}";
        }
    }
}
