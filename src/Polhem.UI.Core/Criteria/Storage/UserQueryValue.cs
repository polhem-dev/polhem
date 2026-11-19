using System.Xml.Serialization;

namespace Polhem.UI.Core.Criteria.Storage
{
    /// <summary>
    /// The stored form of one value of an <see cref="CriteriaOperator.In"/> row.
    /// </summary>
    public sealed class UserQueryValue
    {
        /// <summary>Gets or sets the text shown for the value when it was picked.</summary>
        [XmlAttribute("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the value, in an invariant form; empty for a relative value (<see cref="Relative"/>).</summary>
        [XmlText]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of a value that is resolved each time the query runs, such as <c>me</c> for the
        /// signed-in user (<see cref="CriteriaValue.CurrentUser"/>); <c>null</c> for a stored value.
        /// </summary>
        /// <remarks>
        /// <see cref="Value"/> is left empty beside it, which no relation field reads as one of its keys, so a version
        /// that does not know this attribute leaves the row out instead of reading the value as something else.
        /// </remarks>
        [XmlAttribute("relative")]
        public string? Relative { get; set; }
    }
}
