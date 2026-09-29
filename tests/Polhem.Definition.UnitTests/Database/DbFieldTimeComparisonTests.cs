using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition.Database;

namespace Polhem.Definition.UnitTests.Database
{
    /// <summary>
    /// A time of day is stored as a fixed-width string, so the database reports it as a 5-length
    /// string and never as <see cref="FieldDbType.Time"/>. <see cref="DbField.Compare"/> must reduce
    /// both sides to that physical shape, or every comparison would report drift and re-issue an
    /// ALTER forever (ADR-033).
    /// </summary>
    public class DbFieldTimeComparisonTests
    {
        private static DbField Field(FieldDbType dbType, int length = 0) =>
            new() { FieldName = "work_start", Caption = "Start", DbType = dbType, Length = length };

        [Fact]
        [DisplayName("A Time definition and a String(5) reverse-mapped from the DB are equal and produce no schema diff")]
        public void Compare_TimeAgainstFiveLengthString_ReportsNoDifference()
        {
            var defined = Field(FieldDbType.Time);
            var actual = Field(FieldDbType.String, ValueUtilities.TimeOnlyLength);

            Assert.True(defined.Compare(actual));
        }

        [Fact]
        [DisplayName("Time on both sides is equal")]
        public void Compare_TimeAgainstTime_ReportsNoDifference()
        {
            Assert.True(Field(FieldDbType.Time).Compare(Field(FieldDbType.Time)));
        }

        [Fact]
        [DisplayName("A Time definition against a wider String in the DB is a difference that triggers an upgrade")]
        public void Compare_TimeAgainstWiderString_ReportsDifference()
        {
            var defined = Field(FieldDbType.Time);
            var actual = Field(FieldDbType.String, 50);

            Assert.False(defined.Compare(actual));
        }

        [Fact]
        [DisplayName("Time against a non-string type is still a difference")]
        public void Compare_TimeAgainstNonString_ReportsDifference()
        {
            Assert.False(Field(FieldDbType.Time).Compare(Field(FieldDbType.DateTime)));
            Assert.False(Field(FieldDbType.Time).Compare(Field(FieldDbType.Integer)));
        }

        [Fact]
        [DisplayName("Length comparison of existing String columns is unchanged")]
        public void Compare_StringLengths_Unchanged()
        {
            Assert.True(Field(FieldDbType.String, 50).Compare(Field(FieldDbType.String, 50)));
            Assert.False(Field(FieldDbType.String, 50).Compare(Field(FieldDbType.String, 20)));
        }
    }
}
