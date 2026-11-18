using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;
using Polhem.Core.Data;
using Polhem.UI.Core.Criteria;
using Polhem.UI.Core.Criteria.Storage;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class UserQueryCodecTests
    {
        private static readonly CriteriaField s_name = new("company_name", "Company", CriteriaFieldKind.Text, FieldDbType.String);
        private static readonly CriteriaField s_orderNo = new("order_no", "Order No", CriteriaFieldKind.Code, FieldDbType.String);
        private static readonly CriteriaField s_freight = new("freight", "Freight", CriteriaFieldKind.Number, FieldDbType.Currency);
        private static readonly CriteriaField s_orderDate = new("order_date", "Order date", CriteriaFieldKind.Date, FieldDbType.Date);
        private static readonly CriteriaField s_shipped = new("shipped_date", "Shipped", CriteriaFieldKind.DateTime, FieldDbType.DateTime);
        private static readonly CriteriaField s_discontinued = new("discontinued", "Discontinued", CriteriaFieldKind.Boolean, FieldDbType.Boolean);
        private static readonly CriteriaField s_status = new("status", "Status", CriteriaFieldKind.Choice, FieldDbType.Integer);
        private static readonly CriteriaField s_shipper = new("shipper_rowid", "Shipper", CriteriaFieldKind.Relation, FieldDbType.Guid);

        private static readonly CriteriaField[] s_fields =
            [s_name, s_orderNo, s_freight, s_orderDate, s_shipped, s_discontinued, s_status, s_shipper];

        private static UserQueryLoadResult RoundTrip(QueryCriteria criteria, IReadOnlyList<CriteriaField>? fields = null)
            => UserQueryCodec.Deserialize(UserQueryCodec.Serialize(criteria), fields ?? s_fields);

        [Fact]
        [DisplayName("Every kind of row comes back with its field, operator and what was entered")]
        public void RoundTrip_EveryKind_KeepsWhatWasEntered()
        {
            var shipperId = Guid.NewGuid();
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue("Lee & <Sons>");
            criteria.Add(s_orderNo).SetRange("10248", "10300");
            criteria.Add(s_freight).SetRange(null, 12.50m);
            criteria.Add(s_orderDate).SetRelativeDate(new RelativeDate(RelativeDateKind.LastDays, 7));
            criteria.Add(s_shipped).SetRange(new DateOnly(1996, 7, 1), new DateOnly(1996, 7, 31));
            criteria.Add(s_discontinued).SetValue(false);
            var status = criteria.Add(s_status);
            status.TryAddValue(0, "Draft");
            status.TryAddValue(1, "Confirmed");
            criteria.Add(s_shipper).TryAddValue(shipperId, "Federal Shipping");

            var result = RoundTrip(criteria);

            Assert.True(result.IsSupported);
            Assert.Empty(result.Skipped);
            var rows = result.Criteria.Rows;
            Assert.Equal(criteria.Rows.Select(r => (r.Field.FieldName, r.Operator)), rows.Select(r => (r.Field.FieldName, r.Operator)));
            Assert.Equal("Lee & <Sons>", rows[0].Value);
            Assert.Equal(("10248", "10300"), (rows[1].From, rows[1].To));
            Assert.Equal((null, (object)12.50m), (rows[2].From, rows[2].To));
            Assert.Equal(new RelativeDate(RelativeDateKind.LastDays, 7), rows[3].RelativeDate);
            Assert.Equal((new DateOnly(1996, 7, 1), new DateOnly(1996, 7, 31)), (rows[4].From, rows[4].To));
            Assert.False(Assert.IsType<bool>(rows[5].Value));
            Assert.Equal([new CriteriaValue(0L, "Draft"), new CriteriaValue(1L, "Confirmed")], rows[6].Values);
            Assert.Equal(new CriteriaValue(shipperId, "Federal Shipping"), Assert.Single(rows[7].Values));
        }

        [Fact]
        [DisplayName("A row with nothing entered is kept, so a saved query can hold the rows to fill in")]
        public void RoundTrip_EmptyRow_IsKept()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_orderDate);

            var row = Assert.Single(RoundTrip(criteria).Criteria.Rows);

            Assert.Equal(s_orderDate.FieldName, row.Field.FieldName);
            Assert.False(row.HasValue);
        }

        [Fact]
        [DisplayName("Values are written in an invariant form, so the text does not depend on the user's culture")]
        public void Serialize_IsCultureInvariant()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_freight).SetRange(1234.5m, null);
            criteria.Add(s_orderDate).SetRange(new DateOnly(1996, 7, 4), null);

            string invariant = WithCulture("en-US", () => UserQueryCodec.Serialize(criteria));
            string german = WithCulture("de-DE", () => UserQueryCodec.Serialize(criteria));

            Assert.Equal(invariant, german);
            Assert.Contains("from=\"1234.5\"", invariant);
            Assert.Contains("from=\"1996-07-04\"", invariant);
            Assert.Contains($"version=\"{UserQueryCodec.CurrentVersion}\"", invariant);
            Assert.DoesNotContain("Freight", invariant);
        }

        [Fact]
        [DisplayName("A row whose field is no longer offered is left out and reported")]
        public void Deserialize_UnknownField_IsSkipped()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue("Lee");
            criteria.Add(s_freight).SetRange(1m, 2m);

            var result = RoundTrip(criteria, [s_freight]);

            Assert.Equal(s_freight.FieldName, Assert.Single(result.Criteria.Rows).Field.FieldName);
            Assert.Equal(new UserQuerySkip(s_name.FieldName, UserQuerySkipReason.UnknownField), Assert.Single(result.Skipped));
        }

        [Fact]
        [DisplayName("Fields are matched ignoring the case of their names")]
        public void Deserialize_FieldNameCase_IsIgnored()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue("Lee");
            var renamed = new CriteriaField("COMPANY_NAME", "Company", CriteriaFieldKind.Text, FieldDbType.String);

            var result = RoundTrip(criteria, [renamed]);

            Assert.Empty(result.Skipped);
            Assert.Equal("Lee", Assert.Single(result.Criteria.Rows).Value);
        }

        [Fact]
        [DisplayName("A row whose operator the field no longer offers is left out and reported")]
        public void Deserialize_OperatorNoLongerOffered_IsSkipped()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_name, CriteriaOperator.StartsWith).SetValue("Le");
            var narrowed = new CriteriaField(s_name.FieldName, "Company", CriteriaFieldKind.Text, FieldDbType.String,
                [CriteriaOperator.Contains]);

            var result = RoundTrip(criteria, [narrowed]);

            Assert.Empty(result.Criteria.Rows);
            Assert.Equal(UserQuerySkipReason.OperatorNotOffered, Assert.Single(result.Skipped).Reason);
        }

        [Theory]
        [InlineData("Lee")]
        [InlineData("99")]
        [DisplayName("An operator name this version does not know is reported as not offered")]
        public void Deserialize_UnknownOperatorName_IsSkipped(string name)
        {
            string xml = Document($"<Condition field=\"company_name\" operator=\"{name}\" value=\"x\" />");

            var result = UserQueryCodec.Deserialize(xml, s_fields);

            Assert.Equal(UserQuerySkipReason.OperatorNotOffered, Assert.Single(result.Skipped).Reason);
        }

        [Fact]
        [DisplayName("A value that no longer reads as the field's type leaves the row out")]
        public void Deserialize_ValueOfChangedType_IsSkipped()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_orderNo, CriteriaOperator.Equal).SetValue("ABC");
            var nowNumber = new CriteriaField(s_orderNo.FieldName, "Order No", CriteriaFieldKind.Number, FieldDbType.Integer);

            var result = RoundTrip(criteria, [nowNumber]);

            Assert.Empty(result.Criteria.Rows);
            Assert.Equal(UserQuerySkipReason.InvalidValue, Assert.Single(result.Skipped).Reason);
        }

        [Theory]
        [InlineData("period=\"Fortnight\"")]
        [InlineData("period=\"LastDays\" days=\"0\"")]
        [InlineData("period=\"ThisMonth\" days=\"3\"")]
        [DisplayName("A period this version cannot read leaves the row out")]
        public void Deserialize_InvalidPeriod_IsSkipped(string period)
        {
            string xml = Document($"<Condition field=\"order_date\" operator=\"Range\" {period} />");

            var result = UserQueryCodec.Deserialize(xml, s_fields);

            Assert.Empty(result.Criteria.Rows);
            Assert.Equal(UserQuerySkipReason.InvalidValue, Assert.Single(result.Skipped).Reason);
        }

        [Fact]
        [DisplayName("A value of an In row that the field's stored type cannot take leaves the row out")]
        public void Deserialize_InValueOfWrongType_IsSkipped()
        {
            string xml = Document("<Condition field=\"shipper_rowid\" operator=\"In\"><Value text=\"x\">not-a-guid</Value></Condition>");

            var result = UserQueryCodec.Deserialize(xml, s_fields);

            Assert.Empty(result.Criteria.Rows);
            Assert.Equal(UserQuerySkipReason.InvalidValue, Assert.Single(result.Skipped).Reason);
        }

        [Theory]
        [InlineData("<UserQuery version=\"2\" />")]
        [InlineData("<UserQuery />")]
        [InlineData("<FormSchema version=\"1\" />")]
        [InlineData("<UserQuery version=\"1\">")]
        [InlineData("not xml")]
        [InlineData("")]
        [InlineData("<!DOCTYPE UserQuery [<!ENTITY x \"y\">]><UserQuery version=\"1\" />")]
        [DisplayName("A text that is not a user query of the current version is refused as a whole")]
        public void Deserialize_UnreadableText_IsNotSupported(string xml)
        {
            var result = UserQueryCodec.Deserialize(xml, s_fields);

            Assert.False(result.IsSupported);
            Assert.Empty(result.Criteria.Rows);
            Assert.Empty(result.Skipped);
        }

        [Fact]
        [DisplayName("Serializing the same criteria twice gives the same text, so an edited query can be told apart")]
        public void Serialize_SameCriteria_SameText()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue("Lee");
            string first = UserQueryCodec.Serialize(criteria);

            string reloaded = UserQueryCodec.Serialize(UserQueryCodec.Deserialize(first, s_fields).Criteria);
            criteria.Rows[0].SetValue("Kim");

            Assert.Equal(first, reloaded);
            Assert.NotEqual(first, UserQueryCodec.Serialize(criteria));
        }

        [Fact]
        [DisplayName("The stored types have the shape the reflection-only XmlSerializer on iOS requires")]
        public void StorageTypes_MeetTheReflectionOnlyXmlSerializerShapeRules()
        {
            var types = typeof(UserQueryDocument).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(UserQueryDocument).Namespace && t.IsPublic)
                .ToList();
            var violations = new List<string>();
            foreach (var type in types)
            {
                if (type.GetConstructor(Type.EmptyTypes) is null)
                    violations.Add($"{type.Name}: no public parameterless constructor");
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.PropertyType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                        continue;
                    if (property.IsDefined(typeof(XmlElementAttribute)) && property.SetMethod is not { IsPublic: true })
                        violations.Add($"{type.Name}.{property.Name}: a collection mapped to [XmlElement] without a public setter");
                    int adds = property.PropertyType.GetMethods().Count(m => m.Name == "Add");
                    if (adds != 1)
                        violations.Add($"{type.Name}.{property.Name}: {adds} public Add methods; exactly one is allowed");
                }
            }

            Assert.Contains(typeof(UserQueryValue), types);
            Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
        }

        private static string Document(string conditions)
            => $"<UserQuery version=\"{UserQueryCodec.CurrentVersion}\">{conditions}</UserQuery>";

        private static string WithCulture(string name, Func<string> action)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            try { return action(); }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
