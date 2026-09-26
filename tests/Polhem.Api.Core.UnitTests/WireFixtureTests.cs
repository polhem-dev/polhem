using System.ComponentModel;
using System.Reflection;
using System.Data;
using System.Text.Json;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Api.Core.Wire;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Pins the encoding rules of the JSON body codec as golden fixtures (`wire-fixtures/bodies/`)
    /// for a client in another language to compare against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a round trip in both directions stops a cross-language wire from drifting. On the .NET side,
    /// <c>WireContractDriftTests</c> guards the MessagePack registrations, but nothing can see the
    /// TypeScript side. The fixtures are the only fact both sides share: .NET produces and verifies them, and
    /// TS uses them to check that it can read what .NET writes and writes what .NET can read back.
    /// </para>
    /// <para>
    /// <b>The fixtures pin only the raw body, not the compressed or encrypted bytes.</b> gzip output is not
    /// guaranteed to be identical across .NET versions, and AES-CBC uses a random IV every time, so it cannot be
    /// pinned at all. Those two layers are standard algorithms that each language's library guarantees; what needs
    /// pinning is the JSON shape that only this framework knows.
    /// </para>
    /// <para>
    /// To regenerate (**only when the encoding rules are changed on purpose**):
    /// <c>POLHEM_REGENERATE_WIRE_FIXTURES=1 dotnet test tests/Polhem.Api.Core.UnitTests/…</c>
    /// then read the diff before committing. That diff is the description of the wire change.
    /// </para>
    /// </remarks>
    public class WireFixtureTests
    {
        private static readonly JsonSerializerOptions s_pretty = new() { WriteIndented = true };
        private static readonly Guid s_fixedGuid = new("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        private static readonly DateTime s_fixedUtc = new(2026, 3, 14, 15, 9, 26, 535, DateTimeKind.Utc);

        /// <summary>
        /// Each fixture covers one encoding rule, not one message type.
        /// </summary>
        /// <remarks>
        /// A fixture per message type would give hundreds of nearly identical files and still miss where things
        /// actually go wrong: discriminators, the DataTable shape, camelCase, enums as strings. Message types
        /// themselves are property bags; the TS side can generate them from the type definitions.
        /// </remarks>
        private static IEnumerable<(string Name, object Value, Type Type, string Description)> Cases()
        {
            // 1. The discriminated envelope of object-typed members: every group JSON cannot tell apart needs a fixture.
            foreach (var (name, value, desc) in ObjectMemberValues())
                yield return ($"value-{name}", new Parameter("v", value), typeof(Parameter), desc);

            // 2. DataTable: types are restored from column metadata, not through the envelope.
            yield return ("datatable", BuildTable(), typeof(DataTable),
                "DataTable: cell types are restored from column metadata, so cells carry no discriminator. Covers rowState and the original/current pair on a Modified row.");

            // 3. DataSet: master-detail and relations.
            yield return ("dataset", BuildDataSet(), typeof(DataSet),
                "DataSet: the shape of tables and relations.");

            // 4. Message types: camelCase, enums as strings, nested collections.
            yield return ("message-ping-request", new PingRequest { ClientName = "web", TraceId = "t-001" },
                typeof(PingRequest), "A message type: camelCase naming and the parameters collection inherited from ApiRequest.");

            yield return ("message-getlist-request", BuildGetListRequest(), typeof(GetListRequest),
                "A nested filter tree and sort fields; enums such as the comparison operator travel as strings.");
        }

        private static IEnumerable<(string Name, object Value, string Description)> ObjectMemberValues()
        {
            yield return ("boolean", true, "WireValueCode.Boolean.");
            yield return ("byte", (byte)200, "WireValueCode.Byte.");
            yield return ("sbyte", (sbyte)-100, "WireValueCode.SByte.");
            yield return ("int16", (short)-30000, "WireValueCode.Int16.");
            yield return ("uint16", (ushort)60000, "WireValueCode.UInt16.");
            yield return ("int32", -2000000000, "WireValueCode.Int32.");
            yield return ("uint32", 4000000000u, "WireValueCode.UInt32.");
            yield return ("int64", 9007199254740993L,
                "WireValueCode.Int64. Quoted: a JSON number is a double to every JavaScript reader, which cannot hold an integer past 2^53.");
            yield return ("uint64", 18446744073709551615ul,
                "WireValueCode.UInt64. Quoted for the same reason as Int64.");
            yield return ("single", 1.5f, "WireValueCode.Single.");
            yield return ("double", 1.7976931348623157E+308, "WireValueCode.Double.");
            yield return ("decimal", 79228162514264337593543950335m,
                "WireValueCode.Decimal. Quoted: a JSON number is a double and cannot hold a decimal's precision.");
            yield return ("string", "hello", "WireValueCode.String.");
            yield return ("datetime", s_fixedUtc,
                "WireValueCode.DateTime. Round-trip \"O\" format, which keeps DateTimeKind (ADR-032 depends on it).");
            yield return ("datetimeoffset", new DateTimeOffset(s_fixedUtc).ToOffset(TimeSpan.FromHours(8)),
                "WireValueCode.DateTimeOffset. Round-trip \"O\" format.");
            yield return ("timespan", new TimeSpan(1, 2, 3, 4, 5), "WireValueCode.TimeSpan. Constant \"c\" format.");
            yield return ("dateonly", new DateOnly(2026, 3, 14), "WireValueCode.DateOnly. Round-trip \"O\" format.");
            yield return ("guid", s_fixedGuid, "WireValueCode.Guid. \"D\" format; must not decay into a plain string.");
            yield return ("bytearray", new byte[] { 1, 2, 250, 255 }, "WireValueCode.ByteArray. Base64.");
            yield return ("dbnull", DBNull.Value,
                "WireValueCode.DBNull. The value is written as null; the discriminator is what separates it from a real null.");
            yield return ("objectarray", new object[] { 1, "two", 3.5m },
                "WireValueCode.ObjectArray. Each element carries its own discriminator, recursing through the same envelope.");
            yield return ("datatable", BuildTable(),
                "WireValueCode.DataTable. A DataTable reached through an object-typed member still carries a discriminator - unlike a top-level DataTable body, where the type is already known. See the `datatable` fixture for the payload shape itself.");
            yield return ("null", null!, "A null object-typed member is omitted from the JSON entirely - the property is absent, not written as null. A reader must treat a missing property as null.");
        }

        /// <summary>
        /// The master table used by the fixtures.
        /// </summary>
        /// <remarks>
        /// <c>amount</c> and <c>ref_no</c> deliberately use values that <b>do not fit in a double</b>. The whole
        /// point of the fixtures is to let a client in another language check that it reads and writes correctly,
        /// and a value such as <c>1234.56</c> happens to look unchanged even after <c>JSON.parse</c> turns it into a
        /// double, so the fixture would fail to demonstrate the rule. With <c>decimal.MaxValue</c> and 2^53+1, a reader
        /// that treats them as numbers stops matching immediately.
        /// </remarks>
        private static DataTable BuildTable()
        {
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("ref_no", typeof(long));
            table.Columns.Add("hired_at", typeof(DateTime));
            table.Columns.Add("row_guid", typeof(Guid));
            table.PrimaryKey = [table.Columns["sys_id"]!];

            var unchanged = table.Rows.Add("E001", 79228162514264337593543950335m, 9007199254740993L, s_fixedUtc, s_fixedGuid);
            var modified = table.Rows.Add("E002", 10m, 1L, s_fixedUtc, s_fixedGuid);
            table.AcceptChanges();
            _ = unchanged;
            modified["amount"] = 0.0000000000000000000000000001m;   // A Modified row puts both current and original on the wire.

            table.Rows.Add("E003", 7m, long.MaxValue, s_fixedUtc, s_fixedGuid);  // Added
            return table;
        }

        private static DataSet BuildDataSet()
        {
            var dataSet = new DataSet("Order");
            var master = new DataTable("Master");
            master.Columns.Add("sys_id", typeof(string));
            master.Rows.Add("O001");

            var detail = new DataTable("Detail");
            detail.Columns.Add("sys_id", typeof(string));
            detail.Columns.Add("sys_master_rowid", typeof(string));
            detail.Rows.Add("D001", "O001");

            dataSet.Tables.Add(master);
            dataSet.Tables.Add(detail);
            dataSet.Relations.Add("Master_Detail",
                master.Columns["sys_id"]!, detail.Columns["sys_master_rowid"]!);
            dataSet.AcceptChanges();
            return dataSet;
        }

        private static GetListRequest BuildGetListRequest()
        {
            var group = new FilterGroup(LogicalOperator.And);
            group.Nodes.Add(new FilterCondition("amount", ComparisonOperator.GreaterThan, 100m));
            group.Nodes.Add(new FilterCondition(
                "hired_at", ComparisonOperator.Between, s_fixedUtc, s_fixedUtc.AddDays(30)));

            var sortFields = new SortFieldCollection
            {
                new SortField("sys_id", SortDirection.Desc)
            };

            return new GetListRequest
            {
                SelectFields = "sys_id,amount",
                Filter = group,
                SortFields = sortFields
            };
        }

        #region Fixture files

        private static string FixtureDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Polhem.slnx")))
                dir = dir.Parent;

            Assert.NotNull(dir);   // Not finding the repository root must not pass silently.
            return Path.Combine(dir!.FullName, "wire-fixtures", "bodies");
        }

        private static bool RegenerateRequested =>
            Environment.GetEnvironmentVariable("POLHEM_REGENERATE_WIRE_FIXTURES") == "1";

        /// <summary>
        /// Serializes a case with the current codec and returns the JSON text of the body.
        /// </summary>
        private static string EncodeBody(object value, Type type)
        {
            var bytes = new JsonPayloadSerializer().Serialize(value, type);
            return global::System.Text.Encoding.UTF8.GetString(bytes);
        }

        private static string BuildFixtureText(string name, string description, Type type, string body)
        {
            var fixture = new global::System.Text.Json.Nodes.JsonObject
            {
                ["case"] = name,
                ["description"] = description,
                ["codec"] = PayloadCodecNames.Json,
                ["type"] = type.FullName + ", " + type.Assembly.GetName().Name,
                ["body"] = global::System.Text.Json.Nodes.JsonNode.Parse(body)
            };
            return fixture.ToJsonString(s_pretty);
        }

        #endregion

        [Fact]
        [DisplayName("Wire fixtures match the current JSON codec encoding (a mismatch means the wire changed)")]
        public void Fixtures_MatchCurrentEncoding()
        {
            var dir = FixtureDirectory();
            if (RegenerateRequested)
                Directory.CreateDirectory(dir);

            var mismatches = new List<string>();

            foreach (var (name, value, type, description) in Cases())
            {
                var body = EncodeBody(value, type);
                var expected = BuildFixtureText(name, description, type, body);
                var path = Path.Combine(dir, name + ".json");

                if (RegenerateRequested)
                {
                    File.WriteAllText(path, expected + global::System.Environment.NewLine);
                    continue;
                }

                if (!File.Exists(path))
                {
                    mismatches.Add($"{name}: fixture file does not exist ({path})");
                    continue;
                }

                var actual = File.ReadAllText(path).TrimEnd();
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    mismatches.Add($"{name}: fixture does not match the current encoding");
            }

            Assert.True(mismatches.Count == 0,
                "The JSON body codec encoding does not match the wire fixtures:" + global::System.Environment.NewLine +
                string.Join(global::System.Environment.NewLine, mismatches) + global::System.Environment.NewLine +
                "If this is an intended wire change, regenerate with POLHEM_REGENERATE_WIRE_FIXTURES=1 and read the diff entry by entry. " +
                "Cross-language clients parse according to these fixtures, so a change is a breaking change.");
        }

        [Fact]
        [DisplayName("Deserializing a fixture body and serializing it again reproduces it unchanged (.NET can read back what the TS side writes)")]
        public void Fixtures_RoundTripThroughDeserialize()
        {
            var dir = FixtureDirectory();
            if (RegenerateRequested)
                return;   // The regenerating run has no fixtures to read yet.

            foreach (var (name, _, type, _) in Cases())
            {
                var path = Path.Combine(dir, name + ".json");
                Assert.True(File.Exists(path), $"{name}: fixture file does not exist ({path})");

                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var body = doc.RootElement.GetProperty("body").GetRawText();

                var serializer = new JsonPayloadSerializer();
                var restored = serializer.Deserialize(global::System.Text.Encoding.UTF8.GetBytes(body), type);
                Assert.NotNull(restored);

                var reencoded = global::System.Text.Encoding.UTF8.GetString(serializer.Serialize(restored!, type));
                using var expectedDoc = JsonDocument.Parse(body);
                using var actualDoc = JsonDocument.Parse(reencoded);
                Assert.Equal(
                    JsonSerializer.Serialize(expectedDoc.RootElement),
                    JsonSerializer.Serialize(actualDoc.RootElement));
            }
        }

        [Fact]
        [DisplayName("The fixture set does not shrink: discriminator and structure cases are all present (so the two checks above cannot pass vacuously)")]
        public void FixtureSet_IsNotVacuous()
        {
            var names = Cases().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

            // Named canaries rather than a numeric lower bound: if the naming changes, a count comparison would still pass silently.
            string[] required =
            [
                "value-decimal", "value-int64", "value-guid", "value-datetime",
                "value-dbnull", "value-objectarray", "value-null",
                "datatable", "dataset", "message-getlist-request",
            ];
            foreach (var name in required)
                Assert.Contains(name, names);

            // Every discriminator needs a fixture: missing one means some type silently gets a wrong value on the TS side.
            //
            // This is deliberately derived by reflecting over the `WireValueCode` constants instead of comparing a number.
            // The original assertion compared a hard-coded count of 22 with the number of value cases, and it had two
            // problems. Adding a discriminator without a fixture left the count unchanged and still green. And it was
            // **already wrong** at the time: the 22 value-* files were really 21 discriminators plus value-null, and
            // `WireValueCode.DataTable` (21) never had a fixture. The equal numbers were pure coincidence.
            var codes = typeof(WireValueCode)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(int) && f.Name != nameof(WireValueCode.Count))
                .Select(f => f.Name)
                .ToList();

            // Guards against a vacuous loop: if the reflection filter is wrong, the `foreach` below never runs.
            Assert.Equal(WireValueCode.Count - 1, codes.Count);

            foreach (var code in codes)
            {
                string fixtureName = $"value-{code.ToLowerInvariant()}";
                Assert.True(
                    names.Contains(fixtureName),
                    $"WireValueCode.{code} has no matching {fixtureName} fixture. Cross-language clients have nothing " +
                    "to compare this discriminator's shape against, so a mistake would go unnoticed.");
            }
        }
    }
}
