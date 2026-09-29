using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Db.Schema;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure rule tests covering the type family classification and the narrowing checks of
    /// <see cref="AlterCompatibilityRules"/>. These rules touch no SQL syntax and every provider shares one
    /// implementation, so they are tested once. The SQLite override of <c>GetKindForTypeChange</c> is covered by
    /// <c>SqliteAlterCompatibilityRulesTests</c>.
    /// </summary>
    public class AlterCompatibilityRulesTests
    {
        #region GetKindForTypeChange

        [Theory]
        [InlineData(FieldDbType.String, FieldDbType.String)]
        [InlineData(FieldDbType.Short, FieldDbType.Short)]
        [InlineData(FieldDbType.Integer, FieldDbType.Integer)]
        [InlineData(FieldDbType.Decimal, FieldDbType.Decimal)]
        [InlineData(FieldDbType.Time, FieldDbType.Time)]
        [InlineData(FieldDbType.AutoIncrement, FieldDbType.AutoIncrement)]
        [DisplayName("GetKindForTypeChange returns Alter for the same type")]
        public void GetKindForTypeChange_SameType_ReturnsAlter(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.Alter, AlterCompatibilityRules.GetKindForTypeChange(from, to));
        }

        [Theory]
        [InlineData(FieldDbType.String, FieldDbType.Text)]
        [InlineData(FieldDbType.Text, FieldDbType.String)]
        [InlineData(FieldDbType.String, FieldDbType.Time)]
        [InlineData(FieldDbType.Time, FieldDbType.String)]
        [InlineData(FieldDbType.Short, FieldDbType.Integer)]
        [InlineData(FieldDbType.Integer, FieldDbType.Long)]
        [InlineData(FieldDbType.Long, FieldDbType.Decimal)]
        [InlineData(FieldDbType.Decimal, FieldDbType.Currency)]
        [InlineData(FieldDbType.Date, FieldDbType.DateTime)]
        [InlineData(FieldDbType.DateTime, FieldDbType.Date)]
        [DisplayName("GetKindForTypeChange returns Alter within the same family")]
        public void GetKindForTypeChange_SameFamily_ReturnsAlter(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.Alter, AlterCompatibilityRules.GetKindForTypeChange(from, to));
        }

        [Theory]
        [InlineData(FieldDbType.String, FieldDbType.Integer)]
        [InlineData(FieldDbType.String, FieldDbType.Date)]
        [InlineData(FieldDbType.Integer, FieldDbType.DateTime)]
        [InlineData(FieldDbType.Boolean, FieldDbType.Integer)]
        [InlineData(FieldDbType.Binary, FieldDbType.String)]
        [InlineData(FieldDbType.Guid, FieldDbType.String)]
        [InlineData(FieldDbType.Time, FieldDbType.DateTime)]
        [DisplayName("GetKindForTypeChange returns Rebuild across families")]
        public void GetKindForTypeChange_CrossFamily_ReturnsRebuild(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.Rebuild, AlterCompatibilityRules.GetKindForTypeChange(from, to));
        }

        [Theory]
        [InlineData(FieldDbType.Integer, FieldDbType.AutoIncrement)]
        [InlineData(FieldDbType.AutoIncrement, FieldDbType.Integer)]
        [InlineData(FieldDbType.AutoIncrement, FieldDbType.Long)]
        [DisplayName("GetKindForTypeChange returns Rebuild when the AutoIncrement state changes")]
        public void GetKindForTypeChange_AutoIncrementToggle_ReturnsRebuild(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.Rebuild, AlterCompatibilityRules.GetKindForTypeChange(from, to));
        }

        [Theory]
        [InlineData(FieldDbType.Unknown, FieldDbType.String)]
        [InlineData(FieldDbType.String, FieldDbType.Unknown)]
        [InlineData(FieldDbType.Unknown, FieldDbType.Unknown)]
        [DisplayName("GetKindForTypeChange returns NotSupported for Unknown")]
        public void GetKindForTypeChange_UnknownType_ReturnsNotSupported(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.NotSupported, AlterCompatibilityRules.GetKindForTypeChange(from, to));
        }

        #endregion

        #region IsNarrowing — string capacity

        [Fact]
        [DisplayName("IsNarrowing treats a shorter String length as narrowing")]
        public void IsNarrowing_StringLengthReduced_ReturnsTrue()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing does not treat a longer String length as narrowing")]
        public void IsNarrowing_StringLengthIncreased_ReturnsFalse()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing treats Text to String (bounded length) as narrowing")]
        public void IsNarrowing_TextToString_ReturnsTrue()
        {
            var oldField = new DbField("note", "Note", FieldDbType.Text);
            var newField = new DbField("note", "Note", FieldDbType.String) { Length = 200 };

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing does not treat String to Text as narrowing")]
        public void IsNarrowing_StringToText_ReturnsFalse()
        {
            var oldField = new DbField("note", "Note", FieldDbType.String) { Length = 200 };
            var newField = new DbField("note", "Note", FieldDbType.Text);

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing treats String to Time as narrowing when the length exceeds the time literal length")]
        public void IsNarrowing_WiderStringToTime_ReturnsTrue()
        {
            var oldField = new DbField("t", "T", FieldDbType.String) { Length = ValueUtilities.TimeOnlyLength + 10 };
            var newField = new DbField("t", "T", FieldDbType.Time);

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing does not treat Time to Text as narrowing")]
        public void IsNarrowing_TimeToText_ReturnsFalse()
        {
            var oldField = new DbField("t", "T", FieldDbType.Time);
            var newField = new DbField("t", "T", FieldDbType.Text);

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        #endregion

        #region IsNarrowing — numeric

        [Theory]
        [InlineData(FieldDbType.Long, FieldDbType.Integer)]
        [InlineData(FieldDbType.Integer, FieldDbType.Short)]
        [InlineData(FieldDbType.Long, FieldDbType.Short)]
        [InlineData(FieldDbType.Decimal, FieldDbType.Integer)]
        [InlineData(FieldDbType.Currency, FieldDbType.Long)]
        [DisplayName("IsNarrowing treats a smaller numeric type as narrowing")]
        public void IsNarrowing_NumericRankReduced_ReturnsTrue(FieldDbType from, FieldDbType to)
        {
            var oldField = new DbField("v", "V", from);
            var newField = new DbField("v", "V", to);

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Theory]
        [InlineData(FieldDbType.Short, FieldDbType.Integer)]
        [InlineData(FieldDbType.Integer, FieldDbType.Long)]
        [InlineData(FieldDbType.Integer, FieldDbType.Decimal)]
        [DisplayName("IsNarrowing does not treat a larger numeric type as narrowing")]
        public void IsNarrowing_NumericRankIncreased_ReturnsFalse(FieldDbType from, FieldDbType to)
        {
            var oldField = new DbField("v", "V", from);
            var newField = new DbField("v", "V", to);

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing treats a smaller Decimal precision as narrowing")]
        public void IsNarrowing_DecimalPrecisionReduced_ReturnsTrue()
        {
            var oldField = new DbField("v", "V", FieldDbType.Decimal) { Precision = 18, Scale = 2 };
            var newField = new DbField("v", "V", FieldDbType.Decimal) { Precision = 10, Scale = 2 };

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing treats a smaller Decimal scale as narrowing")]
        public void IsNarrowing_DecimalScaleReduced_ReturnsTrue()
        {
            var oldField = new DbField("v", "V", FieldDbType.Decimal) { Precision = 18, Scale = 4 };
            var newField = new DbField("v", "V", FieldDbType.Decimal) { Precision = 18, Scale = 2 };

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing does not treat an unchanged Decimal precision and scale as narrowing")]
        public void IsNarrowing_DecimalSamePrecisionScale_ReturnsFalse()
        {
            var oldField = new DbField("v", "V", FieldDbType.Decimal) { Precision = 18, Scale = 4 };
            var newField = new DbField("v", "V", FieldDbType.Decimal) { Precision = 18, Scale = 4 };

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        #endregion

        #region IsNarrowing — date and time

        [Fact]
        [DisplayName("IsNarrowing treats DateTime to Date as narrowing (the time part is lost)")]
        public void IsNarrowing_DateTimeToDate_ReturnsTrue()
        {
            var oldField = new DbField("dt", "Dt", FieldDbType.DateTime);
            var newField = new DbField("dt", "Dt", FieldDbType.Date);

            Assert.True(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing does not treat Date to DateTime as narrowing")]
        public void IsNarrowing_DateToDateTime_ReturnsFalse()
        {
            var oldField = new DbField("dt", "Dt", FieldDbType.Date);
            var newField = new DbField("dt", "Dt", FieldDbType.DateTime);

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        #endregion

        #region IsNarrowing — across families

        [Fact]
        [DisplayName("IsNarrowing returns false for a cross-family change (String → Integer) without checking narrowing")]
        public void IsNarrowing_CrossFamily_ReturnsFalse()
        {
            var oldField = new DbField("v", "V", FieldDbType.String) { Length = 50 };
            var newField = new DbField("v", "V", FieldDbType.Integer);

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        [Fact]
        [DisplayName("IsNarrowing returns false for Boolean to Boolean, which is outside the narrowing checks")]
        public void IsNarrowing_BooleanToBoolean_ReturnsFalse()
        {
            var oldField = new DbField("v", "V", FieldDbType.Boolean);
            var newField = new DbField("v", "V", FieldDbType.Boolean);

            Assert.False(AlterCompatibilityRules.IsNarrowing(oldField, newField));
        }

        #endregion
    }
}
