using System.ComponentModel;
using Polhem.Base.Attributes;
using Polhem.Base.Data;
using Polhem.Base.Collections;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// A collection of form fields.
    /// </summary>
    [Description("Form field collection.")]
    [TreeNode("Fields", true)]
    public class FormFieldCollection : KeyCollectionBase<FormField>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FormFieldCollection"/>.
        /// </summary>
        /// <remarks>
        /// Required by XmlSerializer's reflection-only deserialization path (AOT targets such as iOS
        /// create the collection via the public parameterless constructor).
        /// </remarks>
        public FormFieldCollection() : base()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="FormFieldCollection"/>.
        /// </summary>
        /// <param name="formTable">The owning form table.</param>
        public FormFieldCollection(FormTable formTable) : base(formTable)
        { }
    }

    /// <summary>
    /// Convenience extension methods for <see cref="FormFieldCollection"/>.
    /// </summary>
    public static class FormFieldCollectionExtensions
    {
        /// <summary>
        /// Adds a field to the collection.
        /// </summary>
        /// <param name="collection">The collection to add to.</param>
        /// <param name="fieldName">The field name.</param>
        /// <param name="caption">The caption text.</param>
        /// <param name="dbType">The database field type.</param>
        public static FormField Add(this FormFieldCollection? collection, string fieldName, string caption, FieldDbType dbType)
        {
            ArgumentNullException.ThrowIfNull(collection);
            var field = new FormField(fieldName, caption, dbType);
            collection.Add(field);
            return field;
        }
    }
}
