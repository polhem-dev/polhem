using System.Text;

namespace Polhem.Core.Serialization
{
    /// <summary>
    /// String writer that uses UTF-8 encoding.
    /// </summary>
    public sealed class Utf8StringWriter : StringWriter
    {
        /// <summary>
        /// Gets the default UTF-8 encoding (without BOM).
        /// </summary>
        public override Encoding Encoding
        {
            get { return new UTF8Encoding(false); }
        }
    }
}
