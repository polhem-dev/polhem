using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Base.Data;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Gate test: <see cref="PayloadZoneConverter"/> decides what to handle with a switch that lists concrete types,
    /// and its own type comment admits that "new message types are not covered automatically". This test turns that
    /// warning into a test that goes red: reflection finds every message type in <c>Polhem.Api.Core.Messages.*</c>
    /// that carries a <c>DataSet</c> / <c>DataTable</c> / <c>FilterNode</c>, runs each one through the converter and
    /// asserts that the handling really happened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handling depends on direction and carrier (ADR-032 D4): response data must be shifted into the user's time
    /// zone; request filter conditions must be converted to UTC; a request <c>DataSet</c> / <c>DataTable</c> must be
    /// replaced with a copy whose values are not converted, because in an in-process call the server rewrites it in
    /// place.
    /// </para>
    /// <para>
    /// This is stronger than maintaining a list of types: a list only proves that someone remembered to update the
    /// list, while this test proves the handling happens. Adding a message type that carries data but forgetting to
    /// wire it into the switch fails here and names the type.
    /// </para>
    /// </remarks>
    public class PayloadZoneCoverageGuardTests
    {
        private const string Taipei = "Asia/Taipei";
        private static readonly DateTime s_utc9Am = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        /// <summary>Finds every message type that carries temporal data, together with its carrier property.</summary>
        public static TheoryData<string> CarrierTypeNames()
        {
            var data = new TheoryData<string>();
            foreach (var type in CarrierTypes())
            {
                data.Add(type.FullName!);
            }
            return data;
        }

        private static IEnumerable<Type> CarrierTypes() =>
            typeof(ApiMessageBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.Namespace?.StartsWith("Polhem.Api.Core.Messages", StringComparison.Ordinal) == true
                            && t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Any(IsTimeCarrier))
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

        private static bool IsTimeCarrier(PropertyInfo p) =>
            p.CanRead && p.CanWrite
            && (p.PropertyType == typeof(DataSet)
                || p.PropertyType == typeof(DataTable)
                || typeof(FilterNode).IsAssignableFrom(p.PropertyType));

        [Fact]
        [DisplayName("The scan finds message types that carry temporal data (so a broken scan is visible)")]
        public void CarrierScan_FindsTypes()
        {
            Assert.NotEmpty(CarrierTypes());
        }

        [Theory]
        [MemberData(nameof(CarrierTypeNames))]
        [DisplayName("Every message type that carries temporal data is actually handled by PayloadZoneConverter")]
        public void EveryCarrierType_IsActuallyConverted(string typeName)
        {
            var type = CarrierTypes().Single(t => t.FullName == typeName);
            var instance = Activator.CreateInstance(type)!;

            var carrier = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .First(IsTimeCarrier);
            var payload = BuildPayload(carrier.PropertyType);
            carrier.SetValue(instance, payload);

            bool isRequest = typeof(ApiRequest).IsAssignableFrom(type);
            if (isRequest && payload is FilterNode)
            {
                using var swap = PayloadZoneConverter.IsolateRequest(instance, Taipei);
                AssertShifted(carrier.GetValue(instance), typeName, toUtc: true);
            }
            else if (isRequest)
            {
                using var swap = PayloadZoneConverter.IsolateRequest(instance, Taipei);
                AssertIsolatedUnconverted(payload, carrier.GetValue(instance), typeName);
            }
            else
            {
                PayloadZoneConverter.ToUserZone(instance, Taipei);
                AssertShifted(carrier.GetValue(instance), typeName, toUtc: false);
            }
        }

        private static object BuildPayload(Type carrierType)
        {
            if (typeof(FilterNode).IsAssignableFrom(carrierType))
            {
                return new FilterCondition("created_at", ComparisonOperator.Equal, s_utc9Am);
            }

            var table = new DataTable("t");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.Rows.Add(s_utc9Am);
            table.AcceptChanges();

            if (carrierType == typeof(DataTable)) { return table; }

            var dataSet = new DataSet();
            dataSet.Tables.Add(table);
            return dataSet;
        }

        private static void AssertShifted(object? converted, string typeName, bool toUtc)
        {
            var expected = Expected(toUtc);
            var actual = converted switch
            {
                DataSet ds => (DateTime)ds.Tables[0]!.Rows[0]["created_at"],
                DataTable dt => (DateTime)dt.Rows[0]["created_at"],
                FilterCondition condition => (DateTime)condition.Value!,
                _ => throw new InvalidOperationException($"Unhandled carrier shape on '{typeName}'.")
            };

            Assert.True(expected == actual,
                $"'{typeName}' was not converted by PayloadZoneConverter (the value is still {actual:O}, " +
                $"expected {expected:O}). When you add a message type that carries data, wire it into that class's switch too.");
        }

        private static void AssertIsolatedUnconverted(object payload, object? sent, string typeName)
        {
            Assert.False(ReferenceEquals(payload, sent),
                $"The request data of '{typeName}' was not replaced with a copy by PayloadZoneConverter.IsolateRequest. " +
                "In an in-process call the server rewrites it in place. When you add a message type that carries data, wire it into that class's switch too.");

            var actual = sent switch
            {
                DataSet ds => (DateTime)ds.Tables[0]!.Rows[0]["created_at"],
                DataTable dt => (DateTime)dt.Rows[0]["created_at"],
                _ => throw new InvalidOperationException($"Unhandled carrier shape on '{typeName}'.")
            };
            Assert.True(actual == s_utc9Am,
                $"The request data of '{typeName}' was converted (the value is {actual:O}). A DataSet in the request direction is not time-zone converted.");
        }

        private static DateTime Expected(bool toUtc)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(Taipei);
            var shifted = toUtc
                ? TimeZoneInfo.ConvertTimeToUtc(s_utc9Am, zone)
                : TimeZoneInfo.ConvertTimeFromUtc(s_utc9Am, zone);
            return DateTime.SpecifyKind(shifted, DateTimeKind.Unspecified);
        }
    }
}
