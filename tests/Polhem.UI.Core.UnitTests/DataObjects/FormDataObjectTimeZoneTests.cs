using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Core.UnitTests.DataObjects
{
    /// <summary>
    /// The time zone a new row's <c>Date</c> default is taken in, which the head that constructs the
    /// <see cref="FormDataObject"/> supplies.
    /// </summary>
    /// <remarks>
    /// The two zones are 26 hours apart, so their dates differ at every moment of the day, and a wrong zone
    /// cannot pass by coincidence.
    /// </remarks>
    public class FormDataObjectTimeZoneTests
    {
        private const string EastZone = "Pacific/Kiritimati";
        private const string WestZone = "Etc/GMT+12";

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var fields = schema.Tables!.Add("Order", "Order").Fields!;
            fields.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            fields.Add("order_date", "Order Date", FieldDbType.Date);
            return schema;
        }

        private static DateTime NewRowDate(FormDataObject dataObject)
            => (DateTime)dataObject.MasterTable.NewRow()["order_date"];

        // The day can roll over between reading the clock and seeding the row, so either side of it counts.
        private static void AssertTodayIn(string timeZoneId, Func<DateTime> seed)
        {
            var before = FrameworkClock.Today(timeZoneId).ToDateTime(TimeOnly.MinValue);
            var actual = seed();
            var after = FrameworkClock.Today(timeZoneId).ToDateTime(TimeOnly.MinValue);
            Assert.True(actual == before || actual == after,
                $"Expected today in {timeZoneId} ({before:yyyy-MM-dd}), got {actual:yyyy-MM-dd}.");
        }

        [Fact]
        [DisplayName("A new row's date defaults to today in the time zone the provider returns")]
        public void NewRow_DateDefault_UsesProviderTimeZone()
        {
            var east = new FormDataObject(BuildSchema(), timeZoneProvider: () => EastZone);
            var west = new FormDataObject(BuildSchema(), timeZoneProvider: () => WestZone);

            AssertTodayIn(EastZone, () => NewRowDate(east));
            AssertTodayIn(WestZone, () => NewRowDate(west));
        }

        [Fact]
        [DisplayName("Without a time zone provider, a new row's date defaults to today in UTC")]
        public void NewRow_NoProvider_UsesUtc()
        {
            var dataObject = new FormDataObject(BuildSchema());

            AssertTodayIn(string.Empty, () => NewRowDate(dataObject));
        }

        [Fact]
        [DisplayName("The provider is read for each new row, so a time zone set after construction applies")]
        public void NewRow_ProviderChangedAfterConstruction_UsesCurrentValue()
        {
            var zone = EastZone;
            var dataObject = new FormDataObject(BuildSchema(), timeZoneProvider: () => zone);
            AssertTodayIn(EastZone, () => NewRowDate(dataObject));

            zone = WestZone;

            AssertTodayIn(WestZone, () => NewRowDate(dataObject));
        }
    }
}
