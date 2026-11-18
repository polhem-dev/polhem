using System.ComponentModel;
using System.Xml.Serialization;

namespace Polhem.UI.Core.Criteria.Storage
{
    /// <summary>
    /// The stored form of one condition row of a <see cref="UserQueryDocument"/>.
    /// </summary>
    /// <remarks>
    /// The operator and the period are kept as names rather than enum values, so a document written by a newer
    /// version with a member this version does not know still loads: the row is skipped instead of the whole document
    /// failing. Values are written in an invariant form (dates as <c>yyyy-MM-dd</c>) and read back with the type of
    /// the field in the current definition.
    /// </remarks>
    public sealed class UserQueryCondition
    {
        /// <summary>Gets or sets the field name.</summary>
        [XmlAttribute("field")]
        public string Field { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the operator, a <see cref="CriteriaOperator"/> member.</summary>
        [XmlAttribute("operator")]
        public string Operator { get; set; } = string.Empty;

        /// <summary>Gets or sets the value of a single-value operator; <c>null</c> when nothing is entered.</summary>
        [XmlAttribute("value")]
        public string? Value { get; set; }

        /// <summary>Gets or sets the start of a range; <c>null</c> when open.</summary>
        [XmlAttribute("from")]
        public string? From { get; set; }

        /// <summary>Gets or sets the end of a range; <c>null</c> when open.</summary>
        [XmlAttribute("to")]
        public string? To { get; set; }

        /// <summary>Gets or sets the name of a relative period, a <see cref="RelativeDateKind"/> member;
        /// <c>null</c> when the range is entered as days.</summary>
        [XmlAttribute("period")]
        public string? Period { get; set; }

        /// <summary>Gets or sets the number of days of a <see cref="RelativeDateKind.LastDays"/> period.</summary>
        [XmlAttribute("days")]
        [DefaultValue(0)]
        public int Days { get; set; }

        /// <summary>Gets or sets the values of an <see cref="CriteriaOperator.In"/> row.</summary>
        [XmlElement("Value")]
        public List<UserQueryValue> Values { get; set; } = [];
    }
}
