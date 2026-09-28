using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;
using Polhem.Base.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.Save"/> refuses a row that leaves a <see cref="FormField.Required"/> field empty,
    /// after the server has filled its own values and before anything is written.
    /// </summary>
    public class FormBusinessObjectRequiredFieldTests : IClassFixture<SharedDbFixture>
    {
        private const string Code = "code";
        private const string Note = "note";
        private const string Defaulted = "defaulted";
        private const string Item = "item";
        private const string CodeCaption = "Code";
        private const string ItemCaption = "Item";
        private const string DetailDisplayName = "Lines";

        private readonly SharedDbFixture _fx;

        public FormBusinessObjectRequiredFieldTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An added master row with an empty required field is refused with a keyed message naming the caption, and nothing is written")]
        public void Save_AddedMasterRowRequiredFieldEmpty_ThrowsAndWritesNothing()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, code: "   ");

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = dataSet }));

                Assert.Equal(PolhemMessages.SaveFieldRequired, ex.MessageKey);
                Assert.Equal(CodeCaption, Assert.Single(ex.MessageArguments));
                Assert.Null(ReadText(form, form.Schema.ProgId, Note, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A record whose required fields are all filled is saved")]
        public void Save_RequiredFieldsFilled_Saves()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, code: "A-1");
                AddLine(form, dataSet, rowId, item: "bolt");

                NewBo(form).Save(new SaveArgs { DataSet = dataSet });

                Assert.Equal("A-1", ReadText(form, form.Schema.ProgId, Code, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A required field filled by its DefaultValueExpression counts as filled, because the check runs after the server fills values")]
        public void Save_RequiredFieldFilledByDefaultExpression_Saves()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, code: "A-2");
                dataSet.Tables[form.Schema.ProgId]!.Rows[0][Defaulted] = string.Empty;

                NewBo(form).Save(new SaveArgs { DataSet = dataSet });

                Assert.Equal("auto", ReadText(form, form.Schema.ProgId, Defaulted, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A detail row with an empty required field is refused with a message naming the field and the table")]
        public void Save_DetailRowRequiredFieldEmpty_ThrowsNamingTable()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, code: "A-3");
                AddLine(form, dataSet, rowId, item: string.Empty);

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = dataSet }));

                Assert.Equal(PolhemMessages.SaveDetailFieldRequired, ex.MessageKey);
                Assert.Equal([ItemCaption, DetailDisplayName], ex.MessageArguments);
                Assert.Null(ReadText(form, form.Schema.ProgId, Code, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A modified row that clears a required field is refused and the stored value stays")]
        public void Save_ModifiedRowClearsRequiredField_Throws()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                NewBo(form).Save(new SaveArgs { DataSet = NewRecord(form, rowId, code: "A-4") });

                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[form.Schema.ProgId]!.Rows[0][Code] = string.Empty;

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Equal(PolhemMessages.SaveFieldRequired, ex.MessageKey);
                Assert.Equal("A-4", ReadText(form, form.Schema.ProgId, Code, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("The message names the field by the caption the form's language resource gives in the caller's culture")]
        public void Save_RequiredFieldEmpty_UsesLocalizedCaption()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var inner = form.CreateContext();
                var context = new BusinessObjectContext
                {
                    DefineAccess = inner.DefineAccess,
                    SessionInfoService = inner.SessionInfoService,
                    LanguageService = new CaptionLanguageService(form.Schema.ProgId, "Field." + Code + ".Caption", "代碼"),
                    BoFactory = inner.BoFactory,
                    Services = inner.Services,
                };
                var dataSet = NewRecord(form, Guid.NewGuid(), code: string.Empty);

                var bo = new FormBusinessObject(context, TestSessionFactory.CreateAccessToken(_fx), form.Schema.ProgId);
                var ex = Assert.Throws<UserMessageException>(() => bo.Save(new SaveArgs { DataSet = dataSet }));

                Assert.Equal("代碼", Assert.Single(ex.MessageArguments));
            }
            finally
            {
                form.DropTables();
            }
        }

        public static TheoryData<FieldDbType, object?, bool> EmptinessCases => new()
        {
            { FieldDbType.String, null, true },
            { FieldDbType.String, DBNull.Value, true },
            { FieldDbType.String, "", true },
            { FieldDbType.String, " \t", true },
            { FieldDbType.String, "x", false },
            { FieldDbType.Guid, Guid.Empty, true },
            { FieldDbType.Guid, Guid.Empty.ToString(), true },
            { FieldDbType.Guid, Guid.NewGuid(), false },
            { FieldDbType.Binary, Array.Empty<byte>(), true },
            { FieldDbType.Binary, new byte[] { 1 }, false },
            { FieldDbType.Integer, 0, false },
            { FieldDbType.Decimal, 0m, false },
            { FieldDbType.Boolean, false, false },
            { FieldDbType.Date, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), false },
        };

        [Theory]
        [MemberData(nameof(EmptinessCases))]
        [DisplayName("IsEmpty treats null, blank text, an empty Guid and an empty byte array as empty, and never a number, boolean or date")]
        public void IsEmpty_Value_MatchesRule(FieldDbType dbType, object? value, bool expected)
        {
            var field = new FormField("f", "F", dbType);

            Assert.Equal(expected, RequiredFieldValidator.IsEmpty(field, value));
        }

        [Fact]
        [DisplayName("FindEmpty ignores a Required flag on a relation field, which the repository never writes")]
        public void FindEmpty_RequiredRelationField_IsIgnored()
        {
            var schema = new FormSchema("rf", "Relation");
            var table = schema.Tables!.Add("rf", "Master");
            table.Fields!.Add(new FormField("ref_name", "Name", FieldDbType.String) { Type = FieldType.RelationField, Required = true });
            var dataSet = new DataSet();
            var dataTable = dataSet.Tables.Add("rf");
            dataTable.Columns.Add("ref_name", typeof(string));
            dataTable.Rows.Add(string.Empty);

            Assert.Null(RequiredFieldValidator.FindEmpty(schema, dataSet));
        }

        [Fact]
        [DisplayName("FindEmpty judges an added row that omits a required text column by the empty value the insert would store")]
        public void FindEmpty_AddedRowOmitsRequiredColumn_ReportsField()
        {
            var schema = new FormSchema("om", "Omitted");
            var table = schema.Tables!.Add("om", "Master");
            table.Fields!.Add(new FormField(Code, CodeCaption, FieldDbType.String) { Required = true });
            var dataSet = new DataSet();
            var dataTable = dataSet.Tables.Add("om");
            dataTable.Columns.Add(Note, typeof(string));
            dataTable.Rows.Add("x");

            var found = RequiredFieldValidator.FindEmpty(schema, dataSet);

            Assert.NotNull(found);
            Assert.Equal(Code, found!.Value.Field.FieldName);
        }

        private FormBusinessObject NewBo(TransientForm form)
            => new(form.CreateContext(), TestSessionFactory.CreateAccessToken(_fx), form.Schema.ProgId);

        private TransientForm NewForm()
        {
            string progId = TransientForm.NewTableName("tb_req_");
            var schema = new FormSchema(progId, "Required fields") { CategoryId = TransientForm.CategoryId };

            var master = schema.Tables!.Add(progId, "Master");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields!.Add(new FormField(Code, CodeCaption, FieldDbType.String) { MaxLength = 20, Required = true });
            master.Fields!.Add(new FormField(Note, "Note", FieldDbType.String) { MaxLength = 50 });
            master.Fields!.Add(new FormField(Defaulted, "Defaulted", FieldDbType.String)
            {
                MaxLength = 20,
                Required = true,
                DefaultValueExpression = "\"auto\"",
            });

            var detail = schema.Tables.Add(progId + "_d", DetailDisplayName);
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields!.Add(SysFields.MasterRowId, "Master Row Id", FieldDbType.Guid);
            detail.Fields!.Add(new FormField(Item, ItemCaption, FieldDbType.String) { MaxLength = 20, Required = true });

            return new TransientForm(_fx, DatabaseType.SQLite, schema);
        }

        private static DataSet NewRecord(TransientForm form, Guid rowId, string code)
        {
            var dataSet = form.Repository.GetNewData();
            var master = dataSet.Tables[form.Schema.ProgId]!.Rows[0];
            master[SysFields.RowId] = rowId;
            master[Code] = code;
            master[Note] = "new";
            return dataSet;
        }

        private static void AddLine(TransientForm form, DataSet dataSet, Guid rowId, string item)
            => dataSet.Tables[form.Schema.ProgId + "_d"]!.Rows.Add(Guid.NewGuid(), rowId, item);

        private static string? ReadText(TransientForm form, string table, string column, Guid rowId)
            => form.DbAccess.ExecuteScalar(
                $"SELECT {form.Quote(column)} FROM {form.Quote(table)} WHERE {form.Quote(SysFields.RowId)}={{0}}", rowId)?.ToString();

        /// <summary>
        /// Answers one caption key in zh-TW, the default language, and misses everything else. The test
        /// session carries no culture, so the fall-back chain starts at the default language.
        /// </summary>
        private sealed class CaptionLanguageService(string @namespace, string subKey, string caption) : ILanguageService
        {
            public string DefaultLanguage => "zh-TW";

            public bool TryGetLangText(string lang, string ns, string key, out string text)
            {
                bool hit = lang == "zh-TW" && ns == @namespace && key == subKey;
                text = hit ? caption : string.Empty;
                return hit;
            }

            public string GetLangText(string lang, string fullKey) => fullKey;
            public string GetLangText(string lang, string ns, string key) => ns + "." + key;
            public bool TryGetLangText(string lang, string fullKey, out string text) { text = string.Empty; return false; }
            public LanguageEnum? GetLangEnum(string lang, string fullName) => null;
            public LanguageEnum? GetLangEnum(string lang, string ns, string enumName) => null;
            public string? GetLangEnumText(string lang, string fullName, string code) => null;
        }
    }
}
