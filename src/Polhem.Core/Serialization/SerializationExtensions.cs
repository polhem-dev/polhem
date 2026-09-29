namespace Polhem.Core.Serialization
{
    /// <summary>
    /// Extension methods for serialization.
    /// </summary>
    public static class SerializationExtensions
    {
        /// <summary>
        /// Serializes the object to an XML string.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        public static string ToXml(this IObjectSerializeBase value)
        {
            return XmlCodec.Serialize(value);
        }

        /// <summary>
        /// Serializes the object to a JSON string.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        public static string ToJson(this IObjectSerializeBase value)
        {
            return JsonCodec.Serialize(value);
        }

        /// <summary>
        /// Saves the object to its bound file path.
        /// </summary>
        /// <param name="value">The object to save.</param>
        public static void Save(this IObjectSerializeFile value)
        {
            string extension;

            if (StringUtilities.IsEmpty(value.ObjectFilePath))
                throw new ArgumentException("ObjectFilePath is empty");

            extension = Path.GetExtension(value.ObjectFilePath);
            if (StringUtilities.IsEquals(extension, ".xml"))
                XmlCodec.SerializeToFile(value, value.ObjectFilePath);
            else if (StringUtilities.IsEquals(extension, ".json"))
                JsonCodec.SerializeToFile(value, value.ObjectFilePath);
            else
                throw new NotSupportedException();
        }
    }
}
