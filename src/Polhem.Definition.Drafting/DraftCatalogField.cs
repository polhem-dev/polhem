using Polhem.Core.Data;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A field of an existing form, as <see cref="IFormDraftingHost"/> describes it.
    /// </summary>
    public sealed class DraftCatalogField
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftCatalogField"/> class.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="caption">The English caption.</param>
        /// <param name="dbType">The database type.</param>
        public DraftCatalogField(string fieldName, string caption, FieldDbType dbType)
        {
            FieldName = fieldName;
            Caption = caption;
            DbType = dbType;
        }

        /// <summary>Gets the field name.</summary>
        public string FieldName { get; }

        /// <summary>Gets the English caption.</summary>
        public string Caption { get; }

        /// <summary>Gets the database type.</summary>
        public FieldDbType DbType { get; }
    }
}
