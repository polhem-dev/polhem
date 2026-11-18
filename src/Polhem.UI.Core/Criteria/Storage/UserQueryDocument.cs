using System.Xml.Serialization;

namespace Polhem.UI.Core.Criteria.Storage
{
    /// <summary>
    /// The stored form of a user query: the condition rows a user built, written as XML by
    /// <see cref="UserQueryCodec"/>.
    /// </summary>
    /// <remarks>
    /// It keeps what the user entered (a range, a relative period, the picked values and their text), not a filter
    /// already converted for one backend method, so a saved query can be shown in the query panel again. Captions are
    /// not stored: they come from the current definition and the user's language when the query is loaded.
    /// <see cref="UserQueryCodec"/> builds and reads it; callers do not build it themselves.
    /// </remarks>
    [XmlRoot("UserQuery")]
    public sealed class UserQueryDocument
    {
        /// <summary>Gets or sets the format version the document was written in.</summary>
        [XmlAttribute("version")]
        public int Version { get; set; }

        /// <summary>Gets or sets the condition rows, in the order they were added.</summary>
        [XmlElement("Condition")]
        public List<UserQueryCondition> Conditions { get; set; } = [];
    }
}
