using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Base.Serialization;
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
        /// A schema with one property that XmlSerializer reads after the base members, so its getter runs
        /// in the middle of serialization and records what the collections looked like at that point.
        /// </summary>
        public class ObservingFormSchema : FormSchema
        {
            [System.Xml.Serialization.XmlIgnore]
            public bool RulesWereNullDuringSerialization { get; private set; }

            [System.Xml.Serialization.XmlIgnore]
            public bool TablesWereNullDuringSerialization { get; private set; }

            public string Probe
            {
                get
                {
                    RulesWereNullDuringSerialization |= Rules is null;
                    TablesWereNullDuringSerialization |= Tables is null;
                    return string.Empty;
                }
                set { _ = value; }
            }
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
            var schema = (ObservingFormSchema)BuildCommonShape(new ObservingFormSchema());
            var emptyTableSchema = new ObservingFormSchema { ProgId = "Empty" };

            string xml = XmlCodec.Serialize(schema);
            XmlCodec.Serialize(emptyTableSchema);

            Assert.False(schema.RulesWereNullDuringSerialization);
            Assert.False(emptyTableSchema.TablesWereNullDuringSerialization);
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
