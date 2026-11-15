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

        [Fact]
        [DisplayName("FileDefineStorage GetLanguage finds a file whose language folder and namespace differ from the request only in case")]
        public void GetLanguage_RequestInOtherCase_FindsFile()
        {
            WithStorage((paths, storage) =>
            {
                SaveCommon(storage, "Common", "zh-TW", "OK");

                var loaded = storage.GetLanguage("zh-tw", "common");

                Assert.NotNull(loaded);
                Assert.Equal("OK", loaded!.GetText("Ok"));
            });
        }

        [Fact]
        [DisplayName("FileDefineStorage SaveLanguage in another case overwrites the existing file instead of adding a second one")]
        public void SaveLanguage_OtherCase_OverwritesExistingFile()
        {
            WithStorage((paths, storage) =>
            {
                SaveCommon(storage, "Common", "zh-TW", "first");
                SaveCommon(storage, "common", "zh-TW", "second");

                var folder = Path.GetDirectoryName(paths.GetLanguageFilePath("zh-TW", "Common"))!;
                Assert.Single(Directory.GetFiles(folder));
                Assert.Equal("second", storage.GetLanguage("zh-TW", "Common")!.GetText("Ok"));
            });
        }

        [Fact]
        [DisplayName("FileDefineStorage GetChangeSource watches the existing file when the request names it in another case")]
        public void GetChangeSource_RequestInOtherCase_WatchesExistingFile()
        {
            WithStorage((paths, storage) =>
            {
                SaveCommon(storage, "Common", "zh-TW", "OK");

                var source = storage.GetChangeSource(DefineType.Language, "zh-TW", "common");

                var watched = Assert.Single(source.FilePaths!);
                // On a case-sensitive file system only the resolved path exists; elsewhere both spellings do.
                Assert.True(File.Exists(watched), $"Watched file not found at {watched}");
            });
        }

        [Fact]
        [DisplayName("FileDefineStorage GetLanguage reports two files that differ only in case instead of reading either")]
        public void GetLanguage_TwoFilesDifferingInCase_Throws()
        {
            WithStorage((paths, storage) =>
            {
                var upper = paths.GetLanguageFilePath("zh-TW", "Common");
                var lower = paths.GetLanguageFilePath("zh-TW", "common");
                Directory.CreateDirectory(Path.GetDirectoryName(upper)!);
                File.WriteAllText(upper, "<LanguageResource />");
                File.WriteAllText(lower, "<LanguageResource />");

                if (Directory.GetFiles(Path.GetDirectoryName(upper)!).Length == 1)
                {
                    // A case-insensitive file system (the macOS and Windows defaults) cannot hold both files,
                    // so the request that names neither spelling exactly still reads the one file there is.
                    Assert.NotNull(storage.GetLanguage("zh-TW", "COMMON"));
                    return;
                }

                Assert.Throws<InvalidOperationException>(() => storage.GetLanguage("zh-TW", "COMMON"));
            });
        }

        private static void SaveCommon(FileDefineStorage storage, string ns, string lang, string text)
        {
            var resource = new LanguageResource { Namespace = ns, Lang = lang };
            resource.Items.Add("Ok", text);
            storage.SaveLanguage(resource);
        }

        private static void WithStorage(Action<PathOptions, FileDefineStorage> test)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-lang-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var paths = new PathOptions { DefinePath = tempDir };
                test(paths, new FileDefineStorage(paths));
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }
    }
}
