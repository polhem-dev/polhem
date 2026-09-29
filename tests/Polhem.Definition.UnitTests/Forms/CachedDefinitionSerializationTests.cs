using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Serializing a definition must not change what other readers of the same instance see. Definitions
    /// are cached process-wide, and the framework serializes the cached instance itself on every
    /// definition fetch, so any state the serializer sets on the object is visible to every concurrent
    /// request reading it.
    /// </summary>
    public class CachedDefinitionSerializationTests
    {
        /// <summary>
        /// Forwards every call to an inner writer and, each time an element starts or ends, records whether
        /// the schema's collections read as null at that moment. XmlSerializer drives the writer while it
        /// walks the instance, so every element it writes is an observation taken in the middle of
        /// serialization of that same instance.
        /// </summary>
        private sealed class ObservingXmlWriter : XmlWriter
        {
            private readonly XmlWriter _inner;
            private readonly FormSchema _schema;

            public ObservingXmlWriter(XmlWriter inner, FormSchema schema)
            {
                _inner = inner;
                _schema = schema;
            }

            public int Observations { get; private set; }

            public bool RulesWereNull { get; private set; }

            public bool TablesWereNull { get; private set; }

            private void Observe()
            {
                Observations++;
                RulesWereNull |= _schema.Rules is null;
                TablesWereNull |= _schema.Tables is null;
            }

            public override WriteState WriteState => _inner.WriteState;
            public override void Flush() => _inner.Flush();
            public override string? LookupPrefix(string ns) => _inner.LookupPrefix(ns);
            public override void WriteBase64(byte[] buffer, int index, int count) => _inner.WriteBase64(buffer, index, count);
            public override void WriteCData(string? text) => _inner.WriteCData(text);
            public override void WriteCharEntity(char ch) => _inner.WriteCharEntity(ch);
            public override void WriteChars(char[] buffer, int index, int count) => _inner.WriteChars(buffer, index, count);
            public override void WriteComment(string? text) => _inner.WriteComment(text);
            public override void WriteDocType(string name, string? pubid, string? sysid, string? subset) => _inner.WriteDocType(name, pubid, sysid, subset);
            public override void WriteEndAttribute() => _inner.WriteEndAttribute();
            public override void WriteEndDocument() => _inner.WriteEndDocument();
            public override void WriteEntityRef(string name) => _inner.WriteEntityRef(name);
            public override void WriteProcessingInstruction(string name, string? text) => _inner.WriteProcessingInstruction(name, text);
            public override void WriteRaw(char[] buffer, int index, int count) => _inner.WriteRaw(buffer, index, count);
            public override void WriteRaw(string data) => _inner.WriteRaw(data);
            public override void WriteStartAttribute(string? prefix, string localName, string? ns) => _inner.WriteStartAttribute(prefix, localName, ns);
            public override void WriteStartDocument() => _inner.WriteStartDocument();
            public override void WriteStartDocument(bool standalone) => _inner.WriteStartDocument(standalone);
            public override void WriteString(string? text) => _inner.WriteString(text);
            public override void WriteSurrogateCharEntity(char lowChar, char highChar) => _inner.WriteSurrogateCharEntity(lowChar, highChar);
            public override void WriteWhitespace(string? ws) => _inner.WriteWhitespace(ws);

            public override void WriteStartElement(string? prefix, string localName, string? ns)
            {
                Observe();
                _inner.WriteStartElement(prefix, localName, ns);
            }

            public override void WriteEndElement()
            {
                Observe();
                _inner.WriteEndElement();
            }

            public override void WriteFullEndElement()
            {
                Observe();
                _inner.WriteFullEndElement();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Serializes the schema through an <see cref="ObservingXmlWriter"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="XmlCodec.Serialize"/> owns its writer, so this builds the serializer itself. It is the
        /// same <c>new XmlSerializer(type)</c> that <see cref="XmlCodec"/> caches, and the codec sets no state
        /// of its own on the value, so the walk over the instance is the one the codec performs.
        /// </remarks>
        private static ObservingXmlWriter SerializeObserved(FormSchema schema)
        {
            using var text = new StringWriter();
            var observer = new ObservingXmlWriter(XmlWriter.Create(text), schema);
            using (observer)
            {
                new XmlSerializer(typeof(FormSchema)).Serialize(observer, schema);
            }
            return observer;
        }

        private static FormSchema BuildCommonShape(FormSchema schema)
        {
            schema.ProgId = "Probe";
            schema.DisplayName = "Probe";
            var table = schema.Tables!.Add("Probe", "Probe");
            table.Fields!.Add("sys_id", "Id", FieldDbType.String);
            return schema;
        }

        [Fact]
        [DisplayName("An empty collection still reads as an empty collection while the owning schema is being serialized")]
        public void Serialize_ReadDuringSerialization_SeesCollectionsNotNull()
        {
            var schema = BuildCommonShape(new FormSchema());
            var emptyTableSchema = new FormSchema { ProgId = "Empty" };

            var schemaObservation = SerializeObserved(schema);
            var emptyTableObservation = SerializeObserved(emptyTableSchema);
            string xml = XmlCodec.Serialize(schema);

            Assert.NotEqual(0, schemaObservation.Observations);
            Assert.NotEqual(0, emptyTableObservation.Observations);
            Assert.False(schemaObservation.RulesWereNull);
            Assert.False(emptyTableObservation.TablesWereNull);
            Assert.DoesNotContain("<Rules", xml, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Serializing a shared schema on one thread never makes another thread's read of its collections fail")]
        public async Task Serialize_ConcurrentReaders_NeverObserveNullOrEmitEmptyRules()
        {
            var schema = BuildCommonShape(new FormSchema());
            const int iterations = 20_000;
            int failedReads = 0;
            int xmlWithEmptyRules = 0;
            using var start = new ManualResetEventSlim();

            var writer = Task.Run(() =>
            {
                start.Wait();
                for (int i = 0; i < iterations; i++)
                {
                    if (XmlCodec.Serialize(schema).Contains("<Rules", StringComparison.Ordinal))
                        Interlocked.Increment(ref xmlWithEmptyRules);
                }
            });
            var reader = Task.Run(() =>
            {
                start.Wait();
                for (int i = 0; i < iterations * 4; i++)
                {
                    // The production shape in `FormExpressionCalculator.ValidateRules`: a null check,
                    // then a second read of the same property.
                    if (schema.Rules == null || schema.Tables == null) { Interlocked.Increment(ref failedReads); continue; }
                    try
                    {
                        _ = schema.Rules.Where(r => r.Enabled).ToList();
                    }
                    catch (ArgumentNullException) { Interlocked.Increment(ref failedReads); }
                }
            });

            start.Set();
            await Task.WhenAll(writer, reader);

            Assert.Equal(0, failedReads);
            Assert.Equal(0, xmlWithEmptyRules);
        }
    }
}
