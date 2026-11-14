using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Filters;

namespace Polhem.Web.Blazor.Wasm.BrowserTests
{
    /// <summary>
    /// The form, the rows and the filters the WebAssembly wire test runs, shared by the page that runs them in the browser
    /// and the test that checks what the page reports.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: <c>Polhem.Web.Blazor.Wasm.WireApp</c> compiles this file too (a linked <c>Compile</c> item), so both
    /// sides read one copy. It may only use what a browser-wasm application references.
    /// </remarks>
    public static class WireSample
    {
        /// <summary>The program id of the sample form, which is also its master table.</summary>
        public const string ProgId = "WireSample";

        /// <summary>The detail table of the sample form.</summary>
        public const string LineTable = "WireSampleLine";

        /// <summary>The account the page signs in with.</summary>
        public const string UserId = "wire";

        /// <summary>The password of <see cref="UserId"/>. A test value for a database the fixture creates.</summary>
        public const string Password = "wire-round-trip";

        /// <summary>
        /// The API key the clients send. The backend issues none, so it checks only that a request carries one.
        /// </summary>
        public const string ApiKey = "wire-test";

        /// <summary>The company the page enters.</summary>
        public const string CompanyId = "wire";

        /// <summary>The runtime name the page passes to <see cref="WireScenario.RunAsync"/>.</summary>
        public const string BrowserRuntime = "wasm";

        /// <summary>The runtime name the test passes when it runs the scenario on the desktop.</summary>
        public const string DesktopRuntime = "desktop";

        /// <summary>The query parameter that names the scenario.</summary>
        public const string ScenarioParameter = "scenario";

        /// <summary>The scenarios: how each request body is written.</summary>
        public static readonly IReadOnlyList<string> Scenarios = [MessagePack, Json, Plain];

        /// <summary>Encoded bodies in MessagePack, the codec a client that declares none uses.</summary>
        public const string MessagePack = "messagepack";

        /// <summary>Encoded bodies in the JSON body codec.</summary>
        public const string Json = "json";

        /// <summary>
        /// List requests as <c>Plain</c> JSON-RPC. The other actions refuse a plain body from a remote caller, so the
        /// scenario writes them as <see cref="MessagePack"/> does.
        /// </summary>
        public const string Plain = "plain";

        /// <summary>The value fields of the master table, one per data type a column can hold.</summary>
        /// <remarks><see cref="FieldDbType.AutoIncrement"/> is listed apart, because the database assigns its value.</remarks>
        public static readonly IReadOnlyList<(string Name, FieldDbType DbType)> Fields =
        [
            ("v_string", FieldDbType.String),
            ("v_text", FieldDbType.Text),
            ("v_boolean", FieldDbType.Boolean),
            ("v_short", FieldDbType.Short),
            ("v_integer", FieldDbType.Integer),
            ("v_long", FieldDbType.Long),
            ("v_decimal", FieldDbType.Decimal),
            ("v_currency", FieldDbType.Currency),
            ("v_date", FieldDbType.Date),
            ("v_datetime", FieldDbType.DateTime),
            ("v_guid", FieldDbType.Guid),
            ("v_binary", FieldDbType.Binary),
            ("v_time", FieldDbType.Time),
        ];

        /// <summary>
        /// The field whose value the server assigns on a new row: a save stores the current time in every
        /// <see cref="FieldDbType.DateTime"/> field and ignores the caller's value (<c>FormBusinessObject.NormalizeDateTimes</c>).
        /// </summary>
        public const string ServerTimeField = "v_datetime";

        /// <summary>The field the database numbers.</summary>
        public const string AutoIncrementField = "v_sequence";

        /// <summary>The fields of the detail table besides its keys.</summary>
        public static readonly IReadOnlyList<(string Name, FieldDbType DbType)> LineFields =
        [
            ("line_no", FieldDbType.Integer),
            ("qty", FieldDbType.Decimal),
            ("note", FieldDbType.String),
        ];

        /// <summary>The suffixes of the three rows each scenario saves; a row's <c>sys_id</c> ends with one.</summary>
        public static readonly IReadOnlyList<string> Rows = ["A", "B", "C"];

        private static readonly DateTime s_y2k = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The value of each field a client saves in a row, which is every field but <see cref="ServerTimeField"/>. The rows
        /// differ in every field, so each filter tells them apart.
        /// </summary>
        /// <param name="row">The row suffix, one of <see cref="Rows"/>.</param>
        /// <returns>The values by field name.</returns>
        public static IReadOnlyDictionary<string, object> Values(string row)
        {
            int n = row switch { "A" => 1, "B" => 2, "C" => 3, _ => throw new ArgumentOutOfRangeException(nameof(row)) };
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["v_string"] = $"Row {row} – ü 中",
                ["v_text"] = new string('t', 300 * n),
                ["v_boolean"] = n == 2,
                ["v_short"] = (short)(-100 * n),
                ["v_integer"] = 100_000 * n,
                ["v_long"] = 9_000_000_000L * n,
                ["v_decimal"] = 10.25m * n,
                ["v_currency"] = 1234.5m * n,
                ["v_date"] = new DateTime(2026, 2, n, 0, 0, 0, DateTimeKind.Unspecified),
                ["v_guid"] = new Guid($"6c1f4c0e-8a0b-4d8e-9a3c-2f5b7d9e1a0{n}"),
                ["v_binary"] = new byte[] { 0, (byte)n, 250, 255 },
                ["v_time"] = $"{7 + n:00}:45",
            };
        }

        /// <summary>
        /// The filters each scenario lists the rows with, by name, and the row suffixes each must return. Each is
        /// combined with the scenario's own <c>sys_id</c> prefix.
        /// </summary>
        public static IReadOnlyList<(string Name, Func<FilterNode> Filter, string[] Expected)> Filters
        {
            get
            {
                var a = Values("A");
                var b = Values("B");
                var c = Values("C");
                var filters = new List<(string, Func<FilterNode>, string[])>();
                foreach (var (name, dbType) in Fields)
                {
                    // A text column is matched with LIKE, a binary one is not a filter a client writes, and the server
                    // chose the date and time.
                    if (dbType is FieldDbType.Text or FieldDbType.Binary || name == ServerTimeField) continue;
                    var value = FilterValue(b[name]);
                    filters.Add(($"{name} = B", () => FilterCondition.Equal(name, value), ["B"]));
                }
                filters.Add(("v_integer between A and B", () => FilterCondition.Between("v_integer", a["v_integer"], b["v_integer"]), ["A", "B"]));
                filters.Add(("v_date between B and C", () => FilterCondition.Between("v_date", FilterValue(b["v_date"]), FilterValue(c["v_date"])), ["B", "C"]));
                filters.Add(("v_long in A, C", () => FilterCondition.In("v_long", [a["v_long"], c["v_long"]]), ["A", "C"]));
                filters.Add(("v_guid in A, C", () => FilterCondition.In("v_guid", [a["v_guid"], c["v_guid"]]), ["A", "C"]));
                filters.Add(("v_datetime after 2000", () => new FilterCondition(ServerTimeField, ComparisonOperator.GreaterThan, s_y2k), ["A", "B", "C"]));
                filters.Add(("v_datetime before 2000", () => new FilterCondition(ServerTimeField, ComparisonOperator.LessThan, s_y2k), []));
                filters.Add(("v_decimal <= B", () => new FilterCondition("v_decimal", ComparisonOperator.LessThanOrEqual, b["v_decimal"]), ["A", "B"]));
                return filters;
            }
        }

        // A filter value's own type states what it means (ADR-032): a DateTime is an instant and moves from the user's
        // time zone to UTC, a DateOnly is a calendar day and does not. A Date column is filtered with a DateOnly, as a
        // client filters it.
        private static object FilterValue(object value) => value is DateTime day && day.Kind == DateTimeKind.Unspecified
            ? DateOnly.FromDateTime(day)
            : value;

        /// <summary>
        /// Writes a value the way the page reports it and the test compares it: culture-free, with the type of the value
        /// and the kind of a date and time, so a value that comes back as another type or kind compares unequal.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The text.</returns>
        public static string Format(object? value) => value switch
        {
            null or DBNull => "null",
            string text => $"string:{text}",
            bool flag => $"bool:{flag}",
            short number => $"short:{number.ToString(CultureInfo.InvariantCulture)}",
            int number => $"int:{number.ToString(CultureInfo.InvariantCulture)}",
            long number => $"long:{number.ToString(CultureInfo.InvariantCulture)}",
            // The value, not its scale: SQLite gives `3.0` back as `3`.
            decimal number => $"decimal:{number.ToString("G29", CultureInfo.InvariantCulture)}",
            DateTime instant => $"datetime:{instant.Kind}:{instant.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}",
            Guid id => $"guid:{id:D}",
            byte[] bytes => $"bytes:{Convert.ToBase64String(bytes)}",
            _ => $"{value.GetType().Name}:{Convert.ToString(value, CultureInfo.InvariantCulture)}",
        };
    }
}
