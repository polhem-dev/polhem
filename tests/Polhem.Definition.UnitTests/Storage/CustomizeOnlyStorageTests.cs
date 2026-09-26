using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Tests for the strictly read-only behavior of <see cref="CustomizeOnlyStorage"/>: an existing customization file is
    /// returned; a missing one returns null (no fallback); methods for types other than ProgramSettings, FormLayout and
    /// Language throw <see cref="NotSupportedException"/>.
    /// </summary>
    public sealed class CustomizeOnlyStorageTests : IDisposable
    {
        private readonly string _root;
        private const string CustomizeId = "acme";

        public CustomizeOnlyStorageTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-custstore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private CustomizeOnlyStorage CreateStorage()
            => new(new CustomizeOnlyPathOptions(_root, CustomizeId));

        [Fact]
        [DisplayName("GetFormLayout returns the content of an existing customization file")]
        public void GetFormLayout_FileExists_ReturnsLayout()
        {
            var paths = new CustomizeOnlyPathOptions(_root, CustomizeId);
            var layout = new FormLayout { LayoutId = "EmployeeDefault" };
            XmlCodec.SerializeToFile(layout, paths.GetFormLayoutFilePath("EmployeeDefault"));

            var result = CreateStorage().GetFormLayout("EmployeeDefault");

            Assert.NotNull(result);
            Assert.Equal("EmployeeDefault", result!.LayoutId);
        }

        [Fact]
        [DisplayName("GetFormLayout returns null when the customization file is missing (no fallback, no exception)")]
        public void GetFormLayout_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateStorage().GetFormLayout("NonExistent"));
        }

        [Fact]
        [DisplayName("GetLanguage returns the content of an existing customization file")]
        public void GetLanguage_FileExists_ReturnsResource()
        {
            var paths = new CustomizeOnlyPathOptions(_root, CustomizeId);
            var resource = new LanguageResource { Lang = "zh-TW", Namespace = "Customer" };
            XmlCodec.SerializeToFile(resource, paths.GetLanguageFilePath("zh-TW", "Customer"));

            var result = CreateStorage().GetLanguage("zh-TW", "Customer");

            Assert.NotNull(result);
            Assert.Equal("Customer", result!.Namespace);
        }

        [Fact]
        [DisplayName("GetLanguage returns null when the customization file is missing")]
        public void GetLanguage_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateStorage().GetLanguage("zh-TW", "NonExistent"));
        }

        [Fact]
        [DisplayName("GetFormSchema throws NotSupportedException (the override layer does not serve it)")]
        public void GetFormSchema_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => CreateStorage().GetFormSchema("Employee"));
        }

        [Fact]
        [DisplayName("GetTableSchema throws NotSupportedException")]
        public void GetTableSchema_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => CreateStorage().GetTableSchema("common", "st_user"));
        }

        [Fact]
        [DisplayName("GetDbCategorySettings throws NotSupportedException")]
        public void GetDbCategorySettings_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => CreateStorage().GetDbCategorySettings());
        }

        [Fact]
        [DisplayName("Save methods throw NotSupportedException (strictly read-only)")]
        public void SaveMethods_ThrowNotSupported()
        {
            var storage = CreateStorage();
            Assert.Throws<NotSupportedException>(() => storage.SaveFormLayout(new FormLayout()));
            Assert.Throws<NotSupportedException>(() => storage.SaveLanguage(new LanguageResource()));
            Assert.Throws<NotSupportedException>(() => storage.SaveFormSchema(new FormSchema()));
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for null paths")]
        public void Constructor_NullPaths_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomizeOnlyStorage(null!));
        }

        [Fact]
        [DisplayName("GetChangeSource reports the file path the getter actually reads for the customizable types")]
        public void GetChangeSource_CustomizableTypes_ReportsSameFilePathAsGetter()
        {
            var paths = new CustomizeOnlyPathOptions(_root, CustomizeId);
            var storage = CreateStorage();

            Assert.Equal(
                new[] { paths.GetProgramSettingsFilePath() },
                storage.GetChangeSource(DefineType.ProgramSettings).FilePaths);
            Assert.Equal(
                new[] { paths.GetFormLayoutFilePath("EmployeeDefault") },
                storage.GetChangeSource(DefineType.FormLayout, "EmployeeDefault").FilePaths);
            Assert.Equal(
                new[] { paths.GetLanguageFilePath("zh-TW", "Customer") },
                storage.GetChangeSource(DefineType.Language, "zh-TW", "Customer").FilePaths);
        }

        [Fact]
        [DisplayName("GetChangeSource still reports the path when the customization file does not exist yet (the file appearing is itself a change)")]
        public void GetChangeSource_FileMissing_StillReportsPath()
        {
            var source = CreateStorage().GetChangeSource(DefineType.FormLayout, "NeverCreated");

            Assert.NotNull(source.FilePaths);
            Assert.False(File.Exists(source.FilePaths![0]));
        }

        [Fact]
        [DisplayName("GetChangeSource reports no signal instead of throwing for types the override layer does not serve")]
        public void GetChangeSource_UnsupportedTypes_ReturnsNone()
        {
            var storage = CreateStorage();

            Assert.Equal(DefineChangeSource.None, storage.GetChangeSource(DefineType.FormSchema, "Employee"));
            Assert.Equal(DefineChangeSource.None, storage.GetChangeSource(DefineType.TableSchema, "common", "st_user"));
            Assert.Equal(DefineChangeSource.None, storage.GetChangeSource(DefineType.DbCategorySettings));
        }

        [Fact]
        [DisplayName("GetChangeSource reports no signal when a required key is missing")]
        public void GetChangeSource_MissingKeys_ReturnsNone()
        {
            var storage = CreateStorage();

            Assert.Equal(DefineChangeSource.None, storage.GetChangeSource(DefineType.FormLayout));
            Assert.Equal(DefineChangeSource.None, storage.GetChangeSource(DefineType.Language, "zh-TW"));
        }
    }
}
