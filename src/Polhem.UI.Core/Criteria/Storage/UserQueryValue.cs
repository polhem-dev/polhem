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

        /// <summary>Gets or sets the value, in an invariant form.</summary>
        [XmlText]
        public string Value { get; set; } = string.Empty;
    }
}
