using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for <see cref="Defaults"/> — framework default define files embedded
    /// as manifest resources in Polhem.Definition.dll.
    /// </summary>
    public class DefaultsTests
    {
        // Expected manifest contents after Phase 1.1 migration:
        // - 18 TableSchemas (7 common + 6 company + 5 log)
        // - 3 FormSchemas (Department, Employee, AuditRule)
        // - 3 FormLayouts (Department, Employee, AuditRule)
        // - 6 Language resources (Department/Employee/AuditRule × en-US/zh-TW)
        // - 1 DbCategorySettings.xml (minimal — st_* only, no ft_project)
        // - 1 CurrencySettings.xml (curated system currency master)
        // - 1 UnitSettings.xml (curated system unit-of-measure master)
        // - 1 SystemSettings.xml (template with sensible defaults)
        // - 1 DatabaseSettings.xml (empty stub — connection strings are deployment-specific)
        // - 1 PermissionModels.xml (registry seeded with the framework's own AuditRule model)
        // Total: 36
        private const int ExpectedEmbeddedCount = 36;

        [Fact]
        [DisplayName("ListEmbedded returns 36 framework default files (18 st_* + 3 FormSchema + 3 FormLayout + 6 Language + 1 DbCategorySettings + 1 CurrencySettings + 1 UnitSettings + 1 SystemSettings + 1 DatabaseSettings + 1 PermissionModels)")]
        public void ListEmbedded_ReturnsExpectedCount()
        {
            var files = Defaults.ListEmbedded();

            Assert.Equal(ExpectedEmbeddedCount, files.Count);
        }

        [Fact]
        [DisplayName("ListEmbedded uses forward slashes and returns sorted results")]
        public void ListEmbedded_UsesForwardSlashAndSorted()
        {
            var files = Defaults.ListEmbedded();

            Assert.All(files, p => Assert.DoesNotContain('\\', p));
            Assert.Equal(files.OrderBy(x => x, StringComparer.Ordinal), files);
        }

        [Theory]
        [InlineData("DbCategorySettings.xml")]
        [InlineData("CurrencySettings.xml")]
        [InlineData("UnitSettings.xml")]
        [InlineData("SystemSettings.xml")]
        [InlineData("DatabaseSettings.xml")]
        [InlineData("TableSchema/common/st_user.TableSchema.xml")]
        [InlineData("TableSchema/company/st_employee.TableSchema.xml")]
        [InlineData("FormSchema/Department.FormSchema.xml")]
        [InlineData("FormLayout/Employee.FormLayout.xml")]
        [InlineData("Language/zh-TW/Department.Language.xml")]
        [DisplayName("ListEmbedded contains the key framework default files")]
        public void ListEmbedded_ContainsKeyFiles(string expected)
        {
            var files = Defaults.ListEmbedded();

            Assert.Contains(expected, files);
        }

        [Fact]
        [DisplayName("OpenEmbedded of AuditRule.FormSchema.xml declares the permission model and localized dropdown sources")]
        public void OpenEmbedded_AuditRuleFormSchema_DeclaresPermissionModelAndLangEnum()
        {
            var schema = XmlCodec.Deserialize<FormSchema>(ReadEmbedded("FormSchema/AuditRule.FormSchema.xml"));

            Assert.NotNull(schema);
            Assert.Equal("AuditRule", schema!.ProgId);
            Assert.Equal("company", schema.CategoryId);
            // Audit policy is a privileged operation; it is the only framework-bundled form that declares a permission model.
            Assert.Equal("AuditRule", schema.PermissionModelId);

            var fields = schema.MasterTable!.Fields!;
            Assert.Equal("AuditRuleMode", fields["change_mode"]!.LangEnumName);
            Assert.Equal("AuditRuleMode", fields["access_mode"]!.LangEnumName);
            // `ControlType.Auto` falls back to TextEdit and does not become a dropdown just because there is an option source.
            // Without this attribute, the options load but have nowhere to be shown.
            Assert.Equal(ControlType.DropDownEdit, fields["change_mode"]!.ControlType);
            Assert.Equal(ControlType.DropDownEdit, fields["access_mode"]!.ControlType);
            Assert.Equal(ControlType.CheckEdit, fields["is_sensitive"]!.ControlType);
        }

        [Fact]
        [DisplayName("The AuditRule mode dropdowns carry static options whose codes are the persisted AuditRuleMode values")]
        public void OpenEmbedded_AuditRuleFormSchema_ModeFieldsHaveStaticOptions()
        {
            var fields = ReadAuditRuleSchema().MasterTable!.Fields!;
            var expectedCodes = Enum.GetValues<AuditRuleMode>()
                .Select(mode => ((int)mode).ToString(System.Globalization.CultureInfo.InvariantCulture));

            // These are what a head shows when no localization runs (no definition loader, a blank culture) or when
            // it runs for a language with no resource file. Without them the dropdowns were empty.
            Assert.Equal(expectedCodes, fields["change_mode"]!.ListItems!.Select(item => item.Value));
            Assert.Equal(expectedCodes, fields["access_mode"]!.ListItems!.Select(item => item.Value));
        }

        [Fact]
        [DisplayName("The shipped AuditRule schema and layout carry no Chinese text in their captions and display names")]
        public void OpenEmbedded_AuditRuleDefinitions_HaveNoChineseCaptions()
        {
            var schema = ReadAuditRuleSchema();
            var layout = XmlCodec.Deserialize<FormLayout>(ReadEmbedded("FormLayout/AuditRule.FormLayout.xml"))!;
            var texts = new List<string> { schema.DisplayName, schema.MasterTable!.DisplayName, layout.Caption };
            texts.AddRange(schema.MasterTable.Fields!.Select(field => field.Caption));
            texts.AddRange(schema.MasterTable.Fields!.SelectMany(field => field.ListItems!).Select(item => item.Text));
            texts.AddRange(layout.Sections!.SelectMany(section => section.Fields!).Select(field => field.Caption));

            Assert.All(texts, text => Assert.DoesNotContain(text, c => c >= '\u2E80'));
        }

        [Theory]
        [InlineData("fr-FR", "Inherit")]
        [InlineData("zh-TW", "沿用預設")]
        [DisplayName("Localizing the AuditRule schema keeps the static options on a miss and replaces them from the language enum on a hit")]
        public void AuditRuleFormSchema_Localize_FallsBackToStaticOptions(string lang, string expectedFirstText)
        {
            var schema = ReadAuditRuleSchema();
            var resource = lang == "zh-TW"
                ? XmlCodec.Deserialize<LanguageResource>(ReadEmbedded("Language/zh-TW/AuditRule.Language.xml"))
                : null;

            new FormSchemaLocalizer(new SingleResourceLanguageService(resource)).Localize(schema, lang);

            var options = schema.MasterTable!.Fields!["change_mode"]!.ListItems!;
            Assert.Equal(3, options.Count);
            Assert.Equal(expectedFirstText, options[0].Text);
        }

        private static FormSchema ReadAuditRuleSchema()
            => XmlCodec.Deserialize<FormSchema>(ReadEmbedded("FormSchema/AuditRule.FormSchema.xml"))!;

        /// <summary>
        /// Answers from one language resource, or from none, whatever language is asked for.
        /// </summary>
        private sealed class SingleResourceLanguageService : ILanguageService
        {
            private readonly LanguageResource? _resource;

            public SingleResourceLanguageService(LanguageResource? resource) => _resource = resource;

            public string GetLangText(string lang, string fullKey) => fullKey;

            public string GetLangText(string lang, string @namespace, string subKey) => subKey;

            public bool TryGetLangText(string lang, string fullKey, out string text)
            {
                text = string.Empty;
                return false;
            }

            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            {
                text = _resource?.GetText(subKey) ?? string.Empty;
                return _resource?.GetText(subKey) != null;
            }

            public LanguageEnum? GetLangEnum(string lang, string fullName) => null;

            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => _resource?.GetEnum(enumName);

            public string? GetLangEnumText(string lang, string fullName, string code) => null;
        }

        [Theory]
        [InlineData("zh-TW")]
        [InlineData("en-US")]
        [DisplayName("The AuditRule language file contains the AuditRuleMode enum for the three-state dropdown")]
        public void OpenEmbedded_AuditRuleLanguage_HasThreeStateEnum(string lang)
        {
            var resource = XmlCodec.Deserialize<LanguageResource>(
                ReadEmbedded($"Language/{lang}/AuditRule.Language.xml"));

            Assert.NotNull(resource);
            var modes = resource!.GetEnum("AuditRuleMode");

            Assert.NotNull(modes);
            // The three codes must match the persisted values of `AuditRuleMode`; if one is wrong, the dropdown cannot select the right value.
            Assert.Equal(["0", "1", "2"], modes!.Entries.Select(e => e.Code));
            Assert.All(modes.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Text)));
        }

        [Fact]
        [DisplayName("OpenEmbedded of PermissionModels.xml contains the framework's own AuditRule model")]
        public void OpenEmbedded_PermissionModels_ContainsAuditRuleModel()
        {
            var models = XmlCodec.Deserialize<PermissionModels>(ReadEmbedded("PermissionModels.xml"));

            Assert.NotNull(models);
            var model = models!.Models!["AuditRule"];

            Assert.NotNull(model);
            Assert.Contains(model!.Rules!, r => r.Action == PermissionActions.Update);
        }

        [Fact]
        [DisplayName("OpenEmbedded of st_user.TableSchema.xml deserializes to a TableSchema")]
        public void OpenEmbedded_StUserTableSchema_DeserializesSuccessfully()
        {
            var schema = XmlCodec.Deserialize<TableSchema>(ReadEmbedded("TableSchema/common/st_user.TableSchema.xml"));

            Assert.NotNull(schema);
            Assert.Equal("st_user", schema!.TableName);
            Assert.NotEmpty(schema.Fields!);
        }

        [Fact]
        [DisplayName("OpenEmbedded of Department.FormSchema.xml deserializes")]
        public void OpenEmbedded_DepartmentFormSchema_DeserializesSuccessfully()
        {
            var schema = XmlCodec.Deserialize<FormSchema>(ReadEmbedded("FormSchema/Department.FormSchema.xml"));

            Assert.NotNull(schema);
            Assert.Equal("Department", schema!.ProgId);
        }

        [Fact]
        [DisplayName("OpenEmbedded of CurrencySettings.xml deserializes with decimals by currency (JPY=0, USD=2, BHD=3)")]
        public void OpenEmbedded_CurrencySettings_DeserializesWithCurrencyDecimals()
        {
            var settings = XmlCodec.Deserialize<CurrencySettings>(ReadEmbedded("CurrencySettings.xml"));

            Assert.NotNull(settings);
            Assert.NotEmpty(settings!);
            Assert.Equal(2, settings.GetDecimals("USD"));
            Assert.Equal(0, settings.GetDecimals("JPY"));
            Assert.Equal(3, settings.GetDecimals("BHD"));
        }

        [Fact]
        [DisplayName("OpenEmbedded of UnitSettings.xml deserializes with decimals by unit (KG=3, PCS=0)")]
        public void OpenEmbedded_UnitSettings_DeserializesWithUnitDecimals()
        {
            var settings = XmlCodec.Deserialize<UnitSettings>(ReadEmbedded("UnitSettings.xml"));

            Assert.NotNull(settings);
            Assert.NotEmpty(settings!);
            Assert.Equal(3, settings.GetDecimals("KG"));
            Assert.Equal(0, settings.GetDecimals("PCS"));
        }

        [Fact]
        [DisplayName("OpenEmbedded of the minimal DbCategorySettings.xml lists only st_* tables under company (no ft_project)")]
        public void OpenEmbedded_DbCategorySettings_HasOnlyStTables()
        {
            var settings = XmlCodec.Deserialize<DbCategorySettings>(ReadEmbedded("DbCategorySettings.xml"));

            Assert.NotNull(settings);
            var company = settings!.Categories!.First(c => c.Id == "company");
            Assert.All(company.Tables!, t => Assert.StartsWith("st_", t.TableName));
            Assert.DoesNotContain(company.Tables!, t => t.TableName == "ft_project");
        }

        [Fact]
        [DisplayName("OpenEmbedded of SystemSettings.xml deserializes with sensible production defaults (IsDebugMode=false, MasterKeySource=Environment)")]
        public void OpenEmbedded_SystemSettings_HasConservativeDefaults()
        {
            var settings = XmlCodec.Deserialize<SystemSettings>(ReadEmbedded("SystemSettings.xml"));

            Assert.NotNull(settings);
            // Conservative defaults: debug is off and the master key points to an environment variable (the consumer decides the
            // actual value, or changes the source, when deploying).
            Assert.False(settings!.CommonConfiguration.IsDebugMode);
            var masterKey = settings.BackendConfiguration.SecurityKeySettings.MasterKeySource;
            Assert.Equal(Polhem.Definition.Security.MasterKeySourceType.Environment, masterKey.Type);
            Assert.Equal("POLHEM_MASTER_KEY", masterKey.Value);
            Assert.Equal("aes-cbc-hmac", settings.CommonConfiguration.ApiPayloadOptions.Encryptor);
        }

        [Fact]
        [DisplayName("OpenEmbedded of DatabaseSettings.xml is an empty stub (Items null or empty), because connection strings are a deployment choice")]
        public void OpenEmbedded_DatabaseSettings_IsEmptyStub()
        {
            var settings = XmlCodec.Deserialize<DatabaseSettings>(ReadEmbedded("DatabaseSettings.xml"));

            Assert.NotNull(settings);
            Assert.Empty(settings!.Items!);
            Assert.Empty(settings.Servers!);
        }

        [Fact]
        [DisplayName("OpenEmbedded accepts Windows-style backslash paths (normalized automatically)")]
        public void OpenEmbedded_AcceptsBackslashPath()
        {
            using var stream = Defaults.OpenEmbedded("TableSchema\\common\\st_user.TableSchema.xml");

            Assert.NotNull(stream);
        }

        [Fact]
        [DisplayName("OpenEmbedded throws FileNotFoundException for a relativePath that does not exist")]
        public void OpenEmbedded_UnknownPath_ThrowsFileNotFound()
        {
            Assert.Throws<FileNotFoundException>(
                () => Defaults.OpenEmbedded("TableSchema/common/does_not_exist.TableSchema.xml"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("OpenEmbedded throws ArgumentException (including ArgumentNullException) for null, empty or whitespace")]
        public void OpenEmbedded_InvalidArg_ThrowsArgumentException(string? path)
        {
            // `ArgumentException.ThrowIfNullOrWhiteSpace` throws ArgumentNullException for null and ArgumentException for an
            // empty string. Both derive from ArgumentException, so ThrowsAny covers them.
            Assert.ThrowsAny<ArgumentException>(() => Defaults.OpenEmbedded(path!));
        }

        [Fact]
        [DisplayName("MaterializeTo writes every framework default file to an empty directory (including the subdirectory structure)")]
        public void MaterializeTo_EmptyDirectory_WritesAllFiles()
        {
            var tempDir = CreateTempDir();
            try
            {
                var result = Defaults.MaterializeTo(tempDir);

                Assert.Equal(ExpectedEmbeddedCount, result.WrittenCount);
                Assert.Equal(0, result.SkippedCount);

                // Spot-check that the files exist.
                Assert.True(File.Exists(Path.Combine(tempDir, "DbCategorySettings.xml")));
                Assert.True(File.Exists(Path.Combine(tempDir, "TableSchema", "common", "st_user.TableSchema.xml")));
                Assert.True(File.Exists(Path.Combine(tempDir, "Language", "zh-TW", "Department.Language.xml")));
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        [Fact]
        [DisplayName("MaterializeTo with the default Overwrite=false skips every file on the second run")]
        public void MaterializeTo_DefaultOverwriteFalse_SkipsExistingOnSecondRun()
        {
            var tempDir = CreateTempDir();
            try
            {
                Defaults.MaterializeTo(tempDir);
                var secondRun = Defaults.MaterializeTo(tempDir);

                Assert.Equal(0, secondRun.WrittenCount);
                Assert.Equal(ExpectedEmbeddedCount, secondRun.SkippedCount);
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        [Fact]
        [DisplayName("MaterializeTo with Overwrite=true overwrites every file on the second run")]
        public void MaterializeTo_OverwriteTrue_RewritesExisting()
        {
            var tempDir = CreateTempDir();
            try
            {
                Defaults.MaterializeTo(tempDir);

                // Deliberately overwrite the first file with an empty string to simulate a user edit.
                var sentinelFile = Path.Combine(tempDir, "DbCategorySettings.xml");
                File.WriteAllText(sentinelFile, string.Empty);

                var secondRun = Defaults.MaterializeTo(tempDir, new MaterializeOptions { Overwrite = true });

                Assert.Equal(ExpectedEmbeddedCount, secondRun.WrittenCount);
                Assert.Equal(0, secondRun.SkippedCount);
                Assert.True(new FileInfo(sentinelFile).Length > 0);
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        [Fact]
        [DisplayName("MaterializeTo also writes SystemSettings.xml and DatabaseSettings.xml")]
        public void MaterializeTo_WritesSystemAndDatabaseSettings()
        {
            var tempDir = CreateTempDir();
            try
            {
                Defaults.MaterializeTo(tempDir);

                Assert.True(File.Exists(Path.Combine(tempDir, "SystemSettings.xml")));
                Assert.True(File.Exists(Path.Combine(tempDir, "DatabaseSettings.xml")));
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        [Fact]
        [DisplayName("MaterializeTo with a Filter limited to TableSchema writes only TableSchema files")]
        public void MaterializeTo_FilterTableSchemaOnly_WritesTableSchemasOnly()
        {
            var tempDir = CreateTempDir();
            try
            {
                var options = new MaterializeOptions
                {
                    Filter = p => p.StartsWith("TableSchema/", StringComparison.Ordinal),
                };

                var result = Defaults.MaterializeTo(tempDir, options);

                Assert.Equal(18, result.WrittenCount);
                Assert.All(result.WrittenRelativePaths, p => Assert.StartsWith("TableSchema/", p));
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("MaterializeTo throws ArgumentException (including ArgumentNullException) for a null, empty or whitespace path")]
        public void MaterializeTo_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => Defaults.MaterializeTo(path!));
        }

        [Fact]
        [DisplayName("MaterializeTo creates a directory that does not exist")]
        public void MaterializeTo_NonexistentDirectory_CreatesIt()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-defaults-{Guid.NewGuid():N}");
            // Deliberately no `Directory.CreateDirectory` call.
            try
            {
                var result = Defaults.MaterializeTo(tempDir);

                Assert.True(Directory.Exists(tempDir));
                Assert.Equal(ExpectedEmbeddedCount, result.WrittenCount);
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        [Fact]
        [DisplayName("MaterializeResult WrittenRelativePaths plus SkippedRelativePaths equals ExpectedEmbeddedCount")]
        public void MaterializeResult_WrittenPlusSkipped_EqualsTotal()
        {
            var tempDir = CreateTempDir();
            try
            {
                var first = Defaults.MaterializeTo(tempDir);
                Assert.Equal(ExpectedEmbeddedCount, first.WrittenRelativePaths.Count + first.SkippedRelativePaths.Count);

                var second = Defaults.MaterializeTo(tempDir);
                Assert.Equal(ExpectedEmbeddedCount, second.WrittenRelativePaths.Count + second.SkippedRelativePaths.Count);
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        private static string ReadEmbedded(string relativePath)
        {
            using var stream = Defaults.OpenEmbedded(relativePath);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string CreateTempDir()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-defaults-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static void Cleanup(string tempDir)
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }
}
