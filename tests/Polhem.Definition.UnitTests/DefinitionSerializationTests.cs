using System.ComponentModel;
using System.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    public class DefinitionSerializationTests
    {
        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <param name="value">The object.</param>
        /// <param name="isXml">Whether to test XML serialization.</param>
        /// <param name="isJson">Whether to test JSON serialization.</param>
        private static void SerializeObject<T>(object value, bool isXml = true, bool isJson = true)
        {
            // XML: re-serializing after the round-trip must equal the original string; only that proves every field was
            // restored (checking only NotNull lets a field lost in serialization pass as green).
            if (isXml)
            {
                string xml = XmlCodec.Serialize(value);
                var value2 = XmlCodec.Deserialize<T>(xml);
                Assert.NotNull(value2);
                Assert.Equal(xml, XmlCodec.Serialize(value2!));
            }
            // JSON: fidelity is verified the same way, by re-serializing and comparing.
            if (isJson)
            {
                string json = JsonCodec.Serialize(value);
                var value2 = JsonCodec.Deserialize<T>(json);
                Assert.NotNull(value2);
                Assert.Equal(json, JsonCodec.Serialize(value2!));
            }
        }

        /// <summary>
        /// Creates the test data set.
        /// </summary>
        private static DataSet CreateDataSet()
        {
            var dataSet = new DataSet("TestDataSet");
            dataSet.Tables.Add(CreateDataTable());
            return dataSet;
        }

        /// <summary>
        /// Creates the data table used by the tests.
        /// </summary>
        private static DataTable CreateDataTable()
        {
            var table = new DataTable("TestTable");
            table.Columns.Add("F1", typeof(int));
            table.Columns.Add("F2", typeof(string));
            table.Rows.Add(1, "張三");
            table.Rows.Add(2, "李四");
            return table;
        }

        /// <summary>
        /// Serialization of the list item collection.
        /// </summary>
        [Fact]
        [DisplayName("ListItemCollection round-trips through XML and JSON serialization")]
        public void SerializeListItems_XmlAndJson_RoundTripsCorrectly()
        {
            var items = new ListItemCollection
            {
                { "01", "項目一" },
                { "02", "項目二" },
                { "03", "項目三" }
            };
            SerializeObject<ListItemCollection>(items, true, true);
        }

        /// <summary>
        /// Serialization of the parameter collection.
        /// </summary>
        [Fact]
        [DisplayName("ParameterCollection round-trips through JSON serialization")]
        public void SerializeParameters_Json_RoundTripsCorrectly()
        {
            var parameters = new ParameterCollection
            {
                new Parameter("P1", 1),
                new Parameter("P2", "ABC"),
                new Parameter("P3", CreateDataTable()),
                new Parameter("P4", CreateDataSet())
            };
            SerializeObject<ParameterCollection>(parameters, false, true);
        }

        /// <summary>
        /// Serialization of the system settings.
        /// </summary>
        [Fact]
        [DisplayName("SystemSettings round-trips through XML serialization")]
        public void SerializeSystemSettings_Xml_RoundTripsCorrectly()
        {
            var settings = new SystemSettings();
            settings.CommonConfiguration.Version = "1.0.0";
            settings.BackendConfiguration.SecurityKeySettings.MasterKeySource.Value = "default";
            SerializeObject<SystemSettings>(settings, true, false);
        }

        /// <summary>
        /// Tests that Filters serialize and restore the filled property collections.
        /// </summary>
        [Fact]
        [DisplayName("FilterGroup round-trips through XML and JSON serialization")]
        public void SerializeFilters_XmlAndJson_RoundTripsCorrectly()
        {
            var root = FilterGroup.All(
                FilterCondition.Equal("DeptId", 10),
                FilterGroup.Any(
                    FilterCondition.Contains("Name", "Lee"),
                    FilterCondition.Between("HireDate", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Unspecified))
                )
            );
            SerializeObject<FilterGroup>(root, true, true);
        }
    }
}
