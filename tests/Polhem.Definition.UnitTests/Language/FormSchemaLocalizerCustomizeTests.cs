using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Tenant customization overlay tests for <see cref="FormSchemaLocalizer"/>: a key in cust gives the cust value, a key missing from cust gives the base value,
    /// enums are overlaid, and an empty customizeId or the old 2-arg overload short-circuits to the base alone (zero reader calls, bit-for-bit the same as before).
    /// </summary>
    public class FormSchemaLocalizerCustomizeTests
    {
        private static readonly string[] s_statusCodes = ["1", "9"];
        private static readonly string[] s_statusTexts = ["暫停", "客製狀態"];

        private static string FieldKey(string fieldName)
            => string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.FieldCaptionKeyFormat, fieldName);

        private static string TableKey(string tableName)
            => string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.TableDisplayNameKeyFormat, tableName);

        [Fact]
        [DisplayName("A Field.Caption in cust overrides the base value")]
        public void Localize_CustHasFieldCaption_UsesCustValue()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer", (FieldKey("sys_name"), "客戶名稱"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer", (FieldKey("sys_name"), "客戶抬頭"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, "acme", "zh-TW");

            Assert.Equal("客戶抬頭", schema.Tables![0].Fields!["sys_name"].Caption);
        }

        [Fact]
        [DisplayName("A key missing from cust falls back to the base value (per-key overlay)")]
        public void Localize_CustMissesKey_FallsBackToBase()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer",
                (FormSchemaLocalizer.SchemaDisplayNameKey, "客戶"),
                (TableKey("Customer"), "客戶資料"),
                (FieldKey("sys_id"), "客戶編號"),
                (FieldKey("sys_name"), "客戶名稱"));
            var reader = new SpyCustomizeReader();
            // The customization file overrides only one key, so everything else must come from the base.
            reader.AddLanguage("acme", "zh-TW", "Customer", (FieldKey("sys_name"), "客戶抬頭"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, "acme", "zh-TW");

            Assert.Equal("客戶", schema.DisplayName);
            Assert.Equal("客戶資料", schema.Tables![0].DisplayName);
            Assert.Equal("客戶編號", schema.Tables![0].Fields!["sys_id"].Caption);
            Assert.Equal("客戶抬頭", schema.Tables![0].Fields!["sys_name"].Caption);
        }

        [Fact]
        [DisplayName("A LanguageEnum with the same name in cust replaces the ListItems with the customization's whole option set")]
        public void Localize_CustHasLangEnum_ListItemsReplacedByCustEnum()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Customer", "Status", ("0", "啟用"), ("1", "停用"), ("2", "凍結"));
            var reader = new SpyCustomizeReader();
            // The customization file must list the complete option set it wants, because base options it omits are not merged in.
            reader.AddEnum("acme", "zh-TW", "Customer", "Status", ("1", "暫停"), ("9", "客製狀態"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchemaWithLangEnumField("Status");

            localizer.Localize(schema, "acme", "zh-TW");

            var statusField = schema.Tables![0].Fields!["status"];
            Assert.Equal(s_statusCodes, statusField.ListItems!.Select(i => i.Value).ToArray());
            Assert.Equal(s_statusTexts, statusField.ListItems!.Select(i => i.Text).ToArray());
        }

        [Fact]
        [DisplayName("Every field falls back to the base value when the cust resource does not exist")]
        public void Localize_NoCustResource_AllValuesFromBase()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer",
                (FormSchemaLocalizer.SchemaDisplayNameKey, "客戶"),
                (FieldKey("sys_name"), "客戶名稱"));
            var reader = new SpyCustomizeReader(); // acme has no customization files at all.
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, "acme", "zh-TW");

            Assert.Equal("客戶", schema.DisplayName);
            Assert.Equal("客戶名稱", schema.Tables![0].Fields!["sys_name"].Caption);
        }

        // ---- Regression guard: a deployment without a CustomizeId must behave bit-for-bit as before ----

        [Fact]
        [DisplayName("Regression guard: the old 2-arg overload never touches the customization layer (zero reader calls)")]
        public void Localize_LegacyOverload_NeverTouchesCustomizeLayer()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer", (FieldKey("sys_name"), "客戶名稱"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer", (FieldKey("sys_name"), "客戶抬頭"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, "zh-TW");

            Assert.Equal("客戶名稱", schema.Tables![0].Fields!["sys_name"].Caption);
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        [Fact]
        [DisplayName("Regression guard: an empty customizeId never touches the customization layer (zero reader calls)")]
        public void Localize_EmptyCustomizeId_NeverTouchesCustomizeLayer()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer", (FieldKey("sys_name"), "客戶名稱"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer", (FieldKey("sys_name"), "客戶抬頭"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, string.Empty, "zh-TW");

            Assert.Equal("客戶名稱", schema.Tables![0].Fields!["sys_name"].Caption);
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        [Fact]
        [DisplayName("Regression guard: the result for an empty customizeId is identical to the old 2-arg overload")]
        public void Localize_EmptyCustomizeId_MatchesLegacyOverloadResult()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer",
                (FormSchemaLocalizer.SchemaDisplayNameKey, "客戶"),
                (TableKey("Customer"), "客戶資料"),
                (FieldKey("sys_id"), "客戶編號"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, null));

            var viaLegacy = BuildSchema();
            localizer.Localize(viaLegacy, "zh-TW");
            var viaCustomize = BuildSchema();
            localizer.Localize(viaCustomize, string.Empty, "zh-TW");

            Assert.Equal(viaLegacy.DisplayName, viaCustomize.DisplayName);
            Assert.Equal(viaLegacy.Tables![0].DisplayName, viaCustomize.Tables![0].DisplayName);
            Assert.Equal(viaLegacy.Tables![0].Fields!["sys_id"].Caption, viaCustomize.Tables![0].Fields!["sys_id"].Caption);
            Assert.Equal(viaLegacy.Tables![0].Fields!["sys_name"].Caption, viaCustomize.Tables![0].Fields!["sys_name"].Caption);
        }

        [Fact]
        [DisplayName("A blank Lang starts the fall-back chain at the default language, customization overlay included")]
        public void Localize_EmptyLang_ResolvesInDefaultLanguage()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer", (FieldKey("sys_name"), "客戶抬頭"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, "acme", "  ");

            Assert.Equal("客戶抬頭", schema.Tables![0].Fields!["sys_name"].Caption);
            Assert.Equal("Customer ID (raw)", schema.Tables![0].Fields!["sys_id"].Caption);
        }

        [Fact]
        [DisplayName("A blank Lang with no default language leaves the schema untouched and reads no customization")]
        public void Localize_EmptyLangNoDefault_NoOp()
        {
            var defineAccess = new StubDefineAccess("");
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer", (FieldKey("sys_name"), "客戶抬頭"));
            var localizer = new FormSchemaLocalizer(new LanguageService(defineAccess, reader));
            var schema = BuildSchema();

            localizer.Localize(schema, "acme", "  ");

            Assert.Equal("Customer Name (raw)", schema.Tables![0].Fields!["sys_name"].Caption);
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        // ---- Fixtures ----

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Customer", "Customer (raw)") { CategoryId = "common" };
            var table = schema.Tables!.Add("Customer", "Customer (raw table)");
            table.DbTableName = "ft_customer";
            table.Fields!.Add("sys_id", "Customer ID (raw)", FieldDbType.String);
            table.Fields!.Add("sys_name", "Customer Name (raw)", FieldDbType.String);
            return schema;
        }

        private static FormSchema BuildSchemaWithLangEnumField(string langEnumName)
        {
            var schema = new FormSchema("Customer", "Customer") { CategoryId = "common" };
            var table = schema.Tables!.Add("Customer", "Customer");
            table.Fields!.Add(new FormField("status", "Status", FieldDbType.String)
            {
                LangEnumName = langEnumName,
            });
            return schema;
        }

        // ---- Test doubles ----

        private sealed class SpyCustomizeReader : ICustomizeDefineReader
        {
            private readonly Dictionary<string, LanguageResource> _languages = [];

            public int GetCustomizeLanguageCallCount { get; private set; }

            public void AddLanguage(string customizeId, string lang, string ns, params (string Key, string Value)[] items)
            {
                var resource = GetOrCreate(customizeId, lang, ns);
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
            }

            public void AddEnum(string customizeId, string lang, string ns, string enumName, params (string Code, string Text)[] entries)
            {
                var resource = GetOrCreate(customizeId, lang, ns);
                var langEnum = new LanguageEnum { Name = enumName };
                foreach (var (code, text) in entries)
                    langEnum.Entries.Add(code, text);
                resource.Enums.Add(langEnum);
            }

            private LanguageResource GetOrCreate(string customizeId, string lang, string ns)
            {
                string key = $"{customizeId}.{lang}.{ns}";
                if (!_languages.TryGetValue(key, out var resource))
                {
                    resource = new LanguageResource { Namespace = ns, Lang = lang };
                    _languages[key] = resource;
                }
                return resource;
            }

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns)
            {
                GetCustomizeLanguageCallCount++;
                return _languages.TryGetValue($"{customizeId}.{lang}.{ns}", out var r) ? r : null;
            }

            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
        }

        private sealed class StubDefineAccess : IDefineAccess
        {
            private readonly Dictionary<string, LanguageResource> _resources = [];
            private readonly SystemSettings _systemSettings;

            public StubDefineAccess(string defaultLang)
            {
                _systemSettings = new SystemSettings();
                _systemSettings.CommonConfiguration.DefaultLanguage = defaultLang;
            }

            public void AddResource(string lang, string ns, params (string Key, string Value)[] items)
            {
                var resource = GetOrCreate(lang, ns);
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
            }

            public void AddEnum(string lang, string ns, string enumName, params (string Code, string Text)[] entries)
            {
                var resource = GetOrCreate(lang, ns);
                var langEnum = new LanguageEnum { Name = enumName };
                foreach (var (code, text) in entries)
                    langEnum.Entries.Add(code, text);
                resource.Enums.Add(langEnum);
            }

            private LanguageResource GetOrCreate(string lang, string ns)
            {
                string key = $"{lang}.{ns}";
                if (!_resources.TryGetValue(key, out var resource))
                {
                    resource = new LanguageResource { Namespace = ns, Lang = lang };
                    _resources[key] = resource;
                }
                return resource;
            }

            public LanguageResource GetLanguage(string lang, string ns)
                => _resources.TryGetValue($"{lang}.{ns}", out var r) ? r : null!;

            public SystemSettings GetSystemSettings() => _systemSettings;

            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            public ProgramSettings GetProgramSettings() => throw new NotImplementedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public FormSchema GetFormSchema(string progId) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotImplementedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }
    }
}
