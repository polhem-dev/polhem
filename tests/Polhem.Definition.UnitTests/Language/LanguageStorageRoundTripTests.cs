using System.ComponentModel;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Read/write integration tests of <see cref="FileDefineStorage"/> for <see cref="LanguageResource"/>.
    /// Uses an isolated temp directory so the shared fixture data is not polluted.
    /// </summary>
    public class LanguageStorageRoundTripTests
    {
        [Fact]
        [DisplayName("FileDefineStorage SaveLanguage writes the file to Language/{lang}/{ns}.Language.xml")]
        public void SaveLanguage_WritesFileAtExpectedPath()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-lang-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var paths = new PathOptions { DefinePath = tempDir };
                var storage = new FileDefineStorage(paths);
                var resource = new LanguageResource
                {
                    Namespace = "Common",
                    Lang = "zh-TW"
                };
                resource.Items.Add("OK", "確定");

                storage.SaveLanguage(resource);

                var expectedPath = paths.GetLanguageFilePath("zh-TW", "Common");
                Assert.True(File.Exists(expectedPath), $"File not found at {expectedPath}");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("FileDefineStorage GetLanguage deserializes the content written by Save")]
        public void GetLanguage_ReadsBackSavedContent()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-lang-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var paths = new PathOptions { DefinePath = tempDir };
                var storage = new FileDefineStorage(paths);
                var saved = new LanguageResource
                {
                    Namespace = "Customer",
                    Lang = "en-US"
                };
                saved.Items.Add("Field.Name.Caption", "Customer Name");
                saved.Items.Add("SaveFailed", "Save failed.");
                var orderStatus = new LanguageEnum { Name = "OrderStatus" };
                orderStatus.Entries.Add("0", "Pending");
                orderStatus.Entries.Add("1", "Processing");
                saved.Enums.Add(orderStatus);
                storage.SaveLanguage(saved);

                var loaded = storage.GetLanguage("en-US", "Customer");

                Assert.NotNull(loaded);
                Assert.Equal("Customer", loaded!.Namespace);
                Assert.Equal("en-US", loaded.Lang);
                Assert.Equal(2, loaded.Items.Count);
                Assert.Equal("Customer Name", loaded.GetText("Field.Name.Caption"));
                Assert.Equal("Save failed.", loaded.GetText("SaveFailed"));
                var loadedEnum = loaded.GetEnum("OrderStatus");
                Assert.NotNull(loadedEnum);
                Assert.Equal("Pending", loadedEnum!.GetText("0"));
                Assert.Equal("Processing", loadedEnum.GetText("1"));
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("FileDefineStorage GetLanguage returns null for a missing file (a missing translation is normal and can be negatively cached)")]
        public void GetLanguage_MissingFile_ReturnsNull()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-lang-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var paths = new PathOptions { DefinePath = tempDir };
                var storage = new FileDefineStorage(paths);

                var result = storage.GetLanguage("zh-TW", "Nonexistent");

                Assert.Null(result);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }
    }
}
