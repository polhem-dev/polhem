using System.Xml;

namespace Polhem.Base.Serialization
{
    /// <summary>
    /// XML serialization codec. Round-trips objects via <see cref="System.Xml.Serialization.XmlSerializer"/>.
    /// </summary>
    /// <remarks>
    /// The codec sets no state on the value it serializes, so serializing a process-wide cached
    /// definition does not change what concurrent readers of it see (pinned for definitions by
    /// <c>CachedDefinitionSerializationTests</c>). Empty collections are omitted by the owning type's
    /// get-only <c>{Property}Specified</c> properties, which <c>XmlSerializer</c> reads.
    /// <para>
    /// NOTE: definitions use <c>{Property}Specified</c> rather than <c>ShouldSerialize{Property}()</c>.
    /// The reflection-only <c>XmlSerializer</c> (the path on iOS, where dynamic code is unavailable)
    /// throws <see cref="NullReferenceException"/> when a <c>ShouldSerialize</c> method is declared on a
    /// base class of the type being written, and several definition types are serialized through a
    /// subclass (<c>LayoutField</c> and <c>LayoutColumn</c> share <c>LayoutFieldBase</c>). A
    /// <c>Specified</c> property does not have that defect; the mobile AOT gate in the CI workflow
    /// runs the serialization tests with dynamic code disabled.
    /// </para>
    /// </remarks>
    public static class XmlCodec
    {
        /// <summary>
        /// Serializes an object to an XML string.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        public static string Serialize(object value)
        {
            if (value == null)
                return string.Empty;

            using Utf8StringWriter writer = new Utf8StringWriter();
            var serializer = XmlSerializerCache.Get(value.GetType());
            serializer.Serialize(writer, value);
            return writer.ToString();
        }

        /// <summary>
        /// Deserializes an XML string to an object.
        /// </summary>
        /// <typeparam name="T">The generic type.</typeparam>
        /// <param name="xml">The XML string.</param>
        public static T? Deserialize<T>(string xml)
        {
            return (T?)Deserialize(xml, typeof(T));
        }

        /// <summary>
        /// Deserializes an XML string to an object.
        /// </summary>
        /// <param name="xml">The XML string.</param>
        /// <param name="type">The object type.</param>
        /// <remarks>
        /// This is the framework's single entry point for turning XML back into objects, and it is
        /// reached with wire input (definition payloads arrive as XML strings), so it reads through
        /// a hardened <see cref="XmlReader"/> rather than handing the string straight to
        /// <c>XmlSerializer</c>. <c>XmlSerializer(TextReader)</c> already nulls its resolver,
        /// which blocks external entities, but leaves internal entity expansion enabled — enough for
        /// a billion-laughs denial of service. Prohibiting DTD processing closes that, and doing it
        /// here means every present and future caller inherits the setting.
        /// </remarks>
        public static object? Deserialize(string xml, Type type)
        {
            if (StringUtilities.IsEmpty(xml))
                return default;

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (StringReader stringReader = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(stringReader, settings))
            {
                var serializer = XmlSerializerCache.Get(type);
                return serializer.Deserialize(reader);
            }
        }

        /// <summary>
        /// Serializes an object to an XML file.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="filePath">The XML file path.</param>
        public static void SerializeToFile(object value, string filePath)
        {
            string xml = Serialize(value);
            FileUtilities.FileWriteText(filePath, xml);
            // Set the serialization-bound file
            if (value is IObjectSerializeFile fileSerialize) { fileSerialize.SetObjectFilePath(filePath); }
        }

        /// <summary>
        /// Deserializes an XML file to an object.
        /// </summary>
        /// <typeparam name="T">The generic type.</typeparam>
        /// <param name="filePath">The XML file path.</param>
        public static T? DeserializeFromFile<T>(string filePath)
        {
            try
            {
                // Read file contents and deserialize the XML string to an object
                string xml = FileUtilities.FileReadText(filePath);
                T? value = Deserialize<T>(xml);
                // Set the serialization-bound file
                if (value is IObjectSerializeFile objSerializeFile) { objSerializeFile.SetObjectFilePath(filePath); }
                return value;
            }
            catch (Exception ex)
            {
                // WARNING: the file name only, never the path. InvalidOperationException maps to
                // JsonRpcErrorCode.UserMessage, and that mapping returns Message verbatim to the
                // caller — so an authenticated remote caller hitting a corrupt definition file
                // would otherwise be handed the server's absolute directory layout. The full path
                // goes in Data for the server's own log, which is where it belongs.
                var error = new InvalidOperationException(
                    $"DeserializeFromFile Error: {ex.Message}\nFileName: {Path.GetFileName(filePath)}", ex);
                error.Data[SerializationErrorData.FilePath] = filePath;
                throw error;
            }
        }
    }
}
