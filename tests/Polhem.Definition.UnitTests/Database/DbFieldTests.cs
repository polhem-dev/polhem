using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Database;

namespace Polhem.Definition.UnitTests.Database
{
    /// <summary>
    /// Tests for DbField construction, property defaults, Clone, Compare and ToString.
    /// </summary>
    public class DbFieldTests
    {
        [Fact]
        [DisplayName("DbField default constructor uses FieldDbType.String as the default DbType")]
        public void DefaultConstructor_HasExpectedDefaults()
        {
            var field = new DbField();

            Assert.Equal(string.Empty, field.FieldName);
            Assert.Equal(string.Empty, field.Caption);
            Assert.Equal(string.Empty, field.OriginalFieldName);
            Assert.Equal(FieldDbType.String, field.DbType);
            Assert.Equal(0, field.Length);
            Assert.Equal(18, field.Precision);
            Assert.Equal(0, field.Scale);
            Assert.False(field.AllowNull);
            Assert.Equal(string.Empty, field.DefaultValue);
            Assert.Equal(DbUpgradeAction.None, field.UpgradeAction);
        }

        [Fact]
        [DisplayName("DbField parameterized constructor assigns FieldName, Caption and DbType in order")]
        public void ParameterizedConstructor_AssignsCoreProperties()
        {
            var field = new DbField("sys_id", "編號", FieldDbType.Integer);

            Assert.Equal("sys_id", field.FieldName);
            Assert.Equal("編號", field.Caption);
            Assert.Equal(FieldDbType.Integer, field.DbType);
        }

        [Fact]
        [DisplayName("FieldName reads and writes the underlying Key")]
        public void FieldName_MirrorsKey()
        {
            var field = new DbField { FieldName = "k1" };

            Assert.Equal("k1", field.Key);

            field.Key = "k2";
            Assert.Equal("k2", field.FieldName);
        }

        [Fact]
        [DisplayName("Clone copies every field value")]
        public void Clone_CopiesAllFields()
        {
            var source = new DbField("amount", "金額", FieldDbType.Decimal)
            {
                OriginalFieldName = "legacy_amount",
                Length = 0,
                Precision = 19,
                Scale = 4,
                AllowNull = true,
                DefaultValue = "0"
            };

            var clone = source.Clone();

            Assert.NotSame(source, clone);
            Assert.Equal(source.FieldName, clone.FieldName);
            Assert.Equal(source.Caption, clone.Caption);
            Assert.Equal(source.OriginalFieldName, clone.OriginalFieldName);
            Assert.Equal(source.DbType, clone.DbType);
            Assert.Equal(source.Length, clone.Length);
            Assert.Equal(source.Precision, clone.Precision);
            Assert.Equal(source.Scale, clone.Scale);
            Assert.Equal(source.AllowNull, clone.AllowNull);
            Assert.Equal(source.DefaultValue, clone.DefaultValue);
        }

        [Fact]
        [DisplayName("Compare returns true when every key field is equal")]
        public void Compare_SameFields_ReturnsTrue()
        {
            var a = new DbField("name", "名稱", FieldDbType.String)
            {
                Length = 100,
                AllowNull = true,
                DefaultValue = "N/A"
            };
            var b = a.Clone();

            Assert.True(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare returns false for a different DbType")]
        public void Compare_DifferentDbType_ReturnsFalse()
        {
            var a = new DbField("x", "x", FieldDbType.String);
            var b = new DbField("x", "x", FieldDbType.Integer);

            Assert.False(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare returns false for a different AllowNull")]
        public void Compare_DifferentAllowNull_ReturnsFalse()
        {
            var a = new DbField("x", "x", FieldDbType.String) { AllowNull = true };
            var b = new DbField("x", "x", FieldDbType.String) { AllowNull = false };

            Assert.False(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare returns false for a different Length on a String type")]
        public void Compare_StringDifferentLength_ReturnsFalse()
        {
            var a = new DbField("x", "x", FieldDbType.String) { Length = 50 };
            var b = new DbField("x", "x", FieldDbType.String) { Length = 100 };

            Assert.False(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare ignores a Length difference on a non-String type")]
        public void Compare_NonStringLengthIgnored()
        {
            var a = new DbField("x", "x", FieldDbType.Integer) { Length = 10 };
            var b = new DbField("x", "x", FieldDbType.Integer) { Length = 20 };

            Assert.True(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare returns false for a different Decimal Precision")]
        public void Compare_DecimalDifferentPrecision_ReturnsFalse()
        {
            var a = new DbField("x", "x", FieldDbType.Decimal) { Precision = 18, Scale = 4 };
            var b = new DbField("x", "x", FieldDbType.Decimal) { Precision = 19, Scale = 4 };

            Assert.False(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare returns false for a different Decimal Scale")]
        public void Compare_DecimalDifferentScale_ReturnsFalse()
        {
            var a = new DbField("x", "x", FieldDbType.Decimal) { Precision = 18, Scale = 2 };
            var b = new DbField("x", "x", FieldDbType.Decimal) { Precision = 18, Scale = 4 };

            Assert.False(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare ignores Precision/Scale differences on a non-Decimal type")]
        public void Compare_NonDecimalPrecisionScaleIgnored()
        {
            var a = new DbField("x", "x", FieldDbType.String) { Precision = 10, Scale = 2 };
            var b = new DbField("x", "x", FieldDbType.String) { Precision = 20, Scale = 8 };

            Assert.True(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare normalizes an unset DateTime scale on the defined side to 7, matching datetime2(7)")]
        public void Compare_DateTimeDefinedScaleUnset_MatchesDatetime2()
        {
            // Defined field carries no explicit scale (XML never sets it); the real datetime2(7)
            // column reverse-maps to scale 7. Normalization makes them compare equal.
            var defined = new DbField("created_at", "Created", FieldDbType.DateTime);
            var real = new DbField("created_at", "Created", FieldDbType.DateTime) { Scale = 7 };

            Assert.True(defined.Compare(real));
        }

        [Fact]
        [DisplayName("Compare returns false for a defined datetime2(7) against an existing datetime (scale 3)")]
        public void Compare_DateTimeDefinedVsLegacyDatetime_ReturnsFalse()
        {
            // Legacy `datetime` reverse-maps to scale 3; a defined datetime2 normalizes to 7,
            // so the comparer detects a difference and drives the in-place ALTER upgrade.
            var defined = new DbField("created_at", "Created", FieldDbType.DateTime);
            var real = new DbField("created_at", "Created", FieldDbType.DateTime) { Scale = 3 };

            Assert.False(defined.Compare(real));
        }

        [Fact]
        [DisplayName("Compare treats datetime2(7) on both sides as equal")]
        public void Compare_DateTimeBothDatetime2_ReturnsTrue()
        {
            var a = new DbField("created_at", "Created", FieldDbType.DateTime) { Scale = 7 };
            var b = new DbField("created_at", "Created", FieldDbType.DateTime) { Scale = 7 };

            Assert.True(a.Compare(b));
        }

        [Fact]
        [DisplayName("Compare returns false for a different DefaultValue")]
        public void Compare_DifferentDefaultValue_ReturnsFalse()
        {
            var a = new DbField("x", "x", FieldDbType.String) { DefaultValue = "A" };
            var b = new DbField("x", "x", FieldDbType.String) { DefaultValue = "B" };

            Assert.False(a.Compare(b));
        }

        [Fact]
        [DisplayName("ToString returns the FieldName - Caption format")]
        public void ToString_ReturnsFieldNameAndCaption()
        {
            var field = new DbField("sys_id", "編號", FieldDbType.String);

            Assert.Equal("sys_id - 編號", field.ToString());
        }

        [Fact]
        [DisplayName("Compare ignores a different OriginalFieldName (it is only a rename hint)")]
        public void Compare_OriginalFieldNameIgnored()
        {
            var a = new DbField("x", "x", FieldDbType.String) { OriginalFieldName = "old_x" };
            var b = new DbField("x", "x", FieldDbType.String);

            Assert.True(a.Compare(b));
        }
    }
}
