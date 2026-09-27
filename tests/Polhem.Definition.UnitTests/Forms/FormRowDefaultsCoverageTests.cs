using System.ComponentModel;
using System.Data;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Additional tests for <see cref="FormRowDefaults"/>: the guard branches (null formTable, null Fields, a field with no matching data column)
    /// and the default value <see cref="FormRowDefaults.DefaultForDbType"/> returns for each <see cref="FieldDbType"/>.
    /// Entirely in memory, with no database.
    /// </summary>
    public class FormRowDefaultsCoverageTests
    {
        private static DataRow NewSingleColumnRow()
        {
            var table = new DataTable("T");
            table.Columns.Add("sys_rowid", typeof(Guid));
            return table.NewRow();
        }

        [Fact]
        [DisplayName("Apply returns immediately without throwing when formTable is null")]
        public void Apply_NullFormTable_NoThrow()
        {
            var row = NewSingleColumnRow();

            var ex = Record.Exception(() => FormRowDefaults.Apply(null!, row));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("Apply returns without throwing when formTable.Fields is empty")]
        public void Apply_EmptyFields_NoThrow()
        {
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");
            var row = NewSingleColumnRow();

            var ex = Record.Exception(() => FormRowDefaults.Apply(formTable, row));

            Assert.Null(ex);
            Assert.Empty(formTable.Fields!);
        }

        [Fact]
        [DisplayName("Apply skips a field with no matching data column and still applies defaults to the other fields")]
        public void Apply_FieldColumnAbsent_SkipsMissingColumn()
        {
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");
            formTable.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            formTable.Fields.Add("ghost", "Ghost", FieldDbType.String);   // No matching data column.
            formTable.Fields.Add("qty", "Qty", FieldDbType.Integer);

            var table = new DataTable("Order");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("qty", typeof(int));   // Deliberately has no `ghost` column.
            var row = table.NewRow();

            var ex = Record.Exception(() => FormRowDefaults.Apply(formTable, row));

            Assert.Null(ex);
            Assert.NotEqual(Guid.Empty, (Guid)row[SysFields.RowId]);
            Assert.Equal(0, row["qty"]);
        }

        [Theory]
        [InlineData(FieldDbType.String)]
        [InlineData(FieldDbType.Text)]
        [DisplayName("DefaultForDbType returns an empty string for text types")]
        public void DefaultForDbType_TextTypes_ReturnEmptyString(FieldDbType dbType)
        {
            Assert.Equal(string.Empty, FormRowDefaults.DefaultForDbType(dbType));
        }

        [Fact]
        [DisplayName("DefaultForDbType returns false for Boolean")]
        public void DefaultForDbType_Boolean_ReturnsFalse()
        {
            Assert.False(Assert.IsType<bool>(FormRowDefaults.DefaultForDbType(FieldDbType.Boolean)));
        }

        [Fact]
        [DisplayName("DefaultForDbType returns the matching zero for the integer family (short / int / long)")]
        public void DefaultForDbType_IntegerFamily_ReturnsZero()
        {
            Assert.Equal((short)0, FormRowDefaults.DefaultForDbType(FieldDbType.Short));
            Assert.Equal(0, FormRowDefaults.DefaultForDbType(FieldDbType.Integer));
            Assert.Equal(0L, FormRowDefaults.DefaultForDbType(FieldDbType.Long));
        }

        [Fact]
        [DisplayName("DefaultForDbType returns 0m for Decimal / Currency")]
        public void DefaultForDbType_DecimalTypes_ReturnZeroDecimal()
        {
            Assert.Equal(0m, FormRowDefaults.DefaultForDbType(FieldDbType.Decimal));
            Assert.Equal(0m, FormRowDefaults.DefaultForDbType(FieldDbType.Currency));
        }

        [Fact]
        [DisplayName("DefaultForDbType returns the UTC date for Date and a DateTime value for DateTime")]
        public void DefaultForDbType_DateTypes_ReturnTodayAndNow()
        {
            // Read before the act as well as after it, so a run that crosses midnight cannot fail.
            var dayBefore = DateTime.UtcNow.Date;
            // UTC, not `DateTime.Today`: the framework's date default is `UtcNow.Date` (ADR-032 D12).
            // Asserting the local date would always fail locally between 00:00 and 08:00 in UTC+8, and CI running in UTC would never see it.
            Assert.InRange((DateTime)FormRowDefaults.DefaultForDbType(FieldDbType.Date), dayBefore, DateTime.UtcNow.Date);
            Assert.IsType<DateTime>(FormRowDefaults.DefaultForDbType(FieldDbType.DateTime));
        }

        [Fact]
        [DisplayName("DefaultForDbType returns Guid.Empty for Guid and an empty byte array for Binary")]
        public void DefaultForDbType_GuidAndBinary_ReturnEmptyValues()
        {
            Assert.Equal(Guid.Empty, FormRowDefaults.DefaultForDbType(FieldDbType.Guid));
            var binary = Assert.IsType<byte[]>(FormRowDefaults.DefaultForDbType(FieldDbType.Binary));
            Assert.Empty(binary);
        }

        [Fact]
        [DisplayName("DefaultForDbType agrees with FieldDbTypeExtensions.GetDefaultValue for every FieldDbType except Date and DateTime")]
        public void DefaultForDbType_EveryNonDateType_MatchesGetDefaultValue()
        {
            foreach (var dbType in Enum.GetValues<FieldDbType>())
            {
                if (dbType is FieldDbType.Date or FieldDbType.DateTime)
                    continue;

                var fromDefinition = FormRowDefaults.DefaultForDbType(dbType);
                var fromBase = dbType.GetDefaultValue();

                Assert.Equal(fromBase.GetType(), fromDefinition.GetType());
                if (fromBase is byte[] bytes)
                    Assert.Equal(bytes, (byte[])fromDefinition);
                else
                    Assert.Equal(fromBase, fromDefinition);
            }
        }

        [Theory]
        [InlineData(FieldDbType.AutoIncrement)]
        [InlineData(FieldDbType.Unknown)]
        [DisplayName("DefaultForDbType returns DBNull.Value for types with no natural empty value")]
        public void DefaultForDbType_NoNaturalDefault_ReturnsDBNull(FieldDbType dbType)
        {
            Assert.Equal(DBNull.Value, FormRowDefaults.DefaultForDbType(dbType));
        }

        [Fact]
        [DisplayName("DefaultForDbType takes the current DateTime by basis: Utc ignores the time zone, UserZone uses the user's time zone")]
        public void DefaultForDbType_DateTime_FollowsBasis()
        {
            const string kiritimati = "Pacific/Kiritimati";   // UTC+14, so the two bases always differ by 14 hours.
            var utcBefore = DateTime.UtcNow;
            var zoneBefore = FrameworkClock.Now(kiritimati);

            var utc = (DateTime)FormRowDefaults.DefaultForDbType(FieldDbType.DateTime, kiritimati, DateTimeBasis.Utc);
            var userZone = (DateTime)FormRowDefaults.DefaultForDbType(FieldDbType.DateTime, kiritimati);

            Assert.InRange(utc, utcBefore, DateTime.UtcNow);
            Assert.InRange(userZone, zoneBefore, FrameworkClock.Now(kiritimati));
        }

        [Theory]
        [InlineData("Pacific/Kiritimati")]
        [InlineData("Pacific/Pago_Pago")]
        [DisplayName("Apply on a table built with AddColumn sets Date to today in the user's time zone and DateTime to now by basis")]
        public void Apply_OnAddColumnTable_SeedsDateOnUserDayAndDateTimeOnBasis(string timeZoneId)
        {
            // Read before the act as well as after it, so a run that crosses midnight cannot fail.
            var zoneDayBefore = FrameworkClock.Today(timeZoneId).ToDateTime(TimeOnly.MinValue);
            // The server's `GetNewData` and the client's `BuildEmptyDataSet` both build tables with `AddColumn`. If a column carried a clock
            // default from when it was created, `NewRow()` would already have a value, and `Apply` would skip it and leave a UTC reading (ADR-032 D12).
            // At any moment at least one of the two time zones has a "today" different from UTC, so the Date assertion is never vacuous.
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");
            formTable.Fields!.Add("order_date", "Order Date", FieldDbType.Date);
            formTable.Fields.Add("created_at", "Created At", FieldDbType.DateTime);
            var table = new DataTable("Order");
            table.AddColumn("order_date", FieldDbType.Date);
            table.AddColumn("created_at", FieldDbType.DateTime);
            var row = table.NewRow();
            var utcBefore = DateTime.UtcNow;

            FormRowDefaults.Apply(formTable, row, null, timeZoneId, DateTimeBasis.Utc);

            Assert.InRange((DateTime)row["order_date"], zoneDayBefore, FrameworkClock.Today(timeZoneId).ToDateTime(TimeOnly.MinValue));
            Assert.InRange((DateTime)row["created_at"], utcBefore, DateTime.UtcNow);
        }
    }
}
