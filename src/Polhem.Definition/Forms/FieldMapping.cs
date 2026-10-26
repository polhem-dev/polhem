using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Collections;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// A field mapping that maps a source field to a destination field.
    /// </summary>
    [Description("Field mapping.")]
    public sealed class FieldMapping : CollectionItem
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="FieldMapping"/>.
        /// </summary>
        public FieldMapping()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="FieldMapping"/>.
        /// </summary>
        /// <param name="sourceField">The source field.</param>
        /// <param name="destinationField">The destination field.</param>
        public FieldMapping(string sourceField, string destinationField)
        {
            SourceField = sourceField;
            DestinationField = destinationField;
        }

        #endregion

        /// <summary>
        /// Gets or sets the source field.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Source field.")]
        public string SourceField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the destination field.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Destination field.")]
        public string DestinationField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this mapping only applies when a row is picked in a lookup.
        /// </summary>
        /// <remarks>
        /// A lookup-only mapping copies the picked row's value into <see cref="DestinationField"/> when the user
        /// selects it, but takes no part in the query join: <see cref="FormTable.RelationFieldReferences"/> and the
        /// select builder skip it. Use it to carry a value along with the selection, such as the employee's
        /// department into a leave request's own department field, without letting that field be read through
        /// the join. A relation field filled only by lookup-only mappings is empty again when the row is reloaded,
        /// so it still needs a regular mapping on its own foreign key.
        /// </remarks>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Applies only when a row is picked in a lookup; not used by the query join.")]
        [DefaultValue(false)]
        public bool LookupOnly { get; set; }

        /// <summary>
        /// Creates a deep copy of this instance. Result is unparented (no owning collection).
        /// </summary>
        public FieldMapping Clone() => new(SourceField, DestinationField) { LookupOnly = LookupOnly };

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return LookupOnly ? $"{SourceField} -> {DestinationField} (lookup)" : $"{SourceField} -> {DestinationField}";
        }
    }
}
