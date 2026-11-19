using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class CriteriaRowSummaryTests
    {
        private static readonly Dictionary<string, string> s_zhTw = new(StringComparer.Ordinal)
        {
            [PolhemUIText.CriteriaLabeledValue] = "{0}：{1}",
            [PolhemUIText.CriteriaRangeValue] = "{0}～{1}",
            [PolhemUIText.CriteriaValueSeparator] = "、",
            [PolhemUIText.CriteriaMoreValues] = "{0} 等 {1} 筆",
            [PolhemUIText.DateThisMonth] = "本月",
            [PolhemUIText.CurrentUser] = "我",
        };

        private static string English(string key) => PolhemUIText.DefaultTexts[key];

        private static string Chinese(string key) => s_zhTw.TryGetValue(key, out var text) ? text : English(key);

        private static CriteriaRow Row(CriteriaFieldKind kind, FieldDbType dbType, string caption, CriteriaOperator? op = null)
            => new QueryCriteria().Add(new CriteriaField("f", caption, kind, dbType), op);

        private static string Describe(CriteriaRow row, Func<string, string> text)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            try { return CriteriaRowSummary.Describe(row, text); }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Fact]
        [DisplayName("A row with nothing entered has no summary")]
        public void Describe_NoValue_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, Describe(Row(CriteriaFieldKind.Text, FieldDbType.String, "Name"), English));
        }

        [Fact]
        [DisplayName("Contains and starts with name their operator between the field and the text")]
        public void Describe_TextOperators_NameOperator()
        {
            var contains = Row(CriteriaFieldKind.Text, FieldDbType.String, "Name");
            contains.SetValue("Lee");
            var startsWith = Row(CriteriaFieldKind.Text, FieldDbType.String, "Name", CriteriaOperator.StartsWith);
            startsWith.SetValue("A");

            Assert.Equal("Name contains Lee", Describe(contains, English));
            Assert.Equal("Name starts with A", Describe(startsWith, English));
        }

        [Fact]
        [DisplayName("A range with both ends, only a start or only an end reads as such")]
        public void Describe_Range_BothOrOneEnd()
        {
            var both = Row(CriteriaFieldKind.Code, FieldDbType.String, "Order No");
            both.SetRange("10248", "10300");
            var from = Row(CriteriaFieldKind.Code, FieldDbType.String, "Order No");
            from.SetRange("10248", null);
            var to = Row(CriteriaFieldKind.Number, FieldDbType.Currency, "Freight");
            to.SetRange(null, 12.5m);

            Assert.Equal("Order No: 10248 – 10300", Describe(both, English));
            Assert.Equal("Order No: ≥ 10248", Describe(from, English));
            Assert.Equal("Freight: ≤ 12.5", Describe(to, English));
        }

        [Fact]
        [DisplayName("A relative period shows its name and fixed days show in the current culture")]
        public void Describe_Dates_PeriodOrDays()
        {
            var period = Row(CriteriaFieldKind.Date, FieldDbType.Date, "Order date");
            period.SetRelativeDate(new RelativeDate(RelativeDateKind.LastDays, 7));
            var days = Row(CriteriaFieldKind.Date, FieldDbType.Date, "Order date");
            days.SetRange(new DateOnly(1996, 7, 1), null);

            Assert.Equal("Order date: Last 7 days", Describe(period, English));
            Assert.Equal("Order date: ≥ 7/1/1996", Describe(days, English));
        }

        [Fact]
        [DisplayName("A yes / no row shows the answer")]
        public void Describe_Boolean_ShowsAnswer()
        {
            var row = Row(CriteriaFieldKind.Boolean, FieldDbType.Boolean, "Discontinued");
            row.SetValue(true);

            Assert.Equal("Discontinued: Yes", Describe(row, English));
        }

        [Fact]
        [DisplayName("Up to two values are listed, and more show the first one with the count")]
        public void Describe_Values_ListOrCount()
        {
            var two = Row(CriteriaFieldKind.Relation, FieldDbType.Guid, "Shipper");
            two.TryAddValue(Guid.NewGuid(), "Federal Shipping");
            two.TryAddValue(Guid.NewGuid(), "United Package");
            var three = Row(CriteriaFieldKind.Relation, FieldDbType.Guid, "Shipper");
            three.TryAddValue(Guid.NewGuid(), "Federal Shipping");
            three.TryAddValue(Guid.NewGuid(), "United Package");
            three.TryAddValue(Guid.NewGuid(), "Speedy Express");

            Assert.Equal("Shipper: Federal Shipping, United Package", Describe(two, English));
            Assert.Equal("Shipper: Federal Shipping and 2 more", Describe(three, English));
        }

        [Fact]
        [DisplayName("The separators and the count come from the language, so Traditional Chinese reads naturally")]
        public void Describe_TraditionalChinese_UsesItsSeparators()
        {
            var values = Row(CriteriaFieldKind.Relation, FieldDbType.Guid, "貨運商");
            values.TryAddValue(Guid.NewGuid(), "Federal Shipping");
            values.TryAddValue(Guid.NewGuid(), "United Package");
            values.TryAddValue(Guid.NewGuid(), "Speedy Express");
            var period = Row(CriteriaFieldKind.Date, FieldDbType.Date, "訂單日期");
            period.SetRelativeDate(new RelativeDate(RelativeDateKind.ThisMonth));
            var range = Row(CriteriaFieldKind.Code, FieldDbType.String, "訂單編號");
            range.SetRange("10248", "10300");

            Assert.Equal("貨運商：Federal Shipping 等 3 筆", Describe(values, Chinese));
            Assert.Equal("訂單日期：本月", Describe(period, Chinese));
            Assert.Equal("訂單編號：10248～10300", Describe(range, Chinese));
        }

        [Fact]
        [DisplayName("Me shows in the reader's language beside a picked name")]
        public void Describe_CurrentUser_ShowsInReadersLanguage()
        {
            var row = new QueryCriteria().Add(
                new CriteriaField("f", "Salesperson", CriteriaFieldKind.Relation, FieldDbType.Guid) { OffersCurrentUser = true });
            row.TryAddValue(Guid.NewGuid(), "Nancy");
            row.AddCurrentUser();

            Assert.Equal("Salesperson: Nancy, Me", Describe(row, English));
            Assert.Equal("Salesperson：Nancy、我", Describe(row, Chinese));
        }
    }
}
