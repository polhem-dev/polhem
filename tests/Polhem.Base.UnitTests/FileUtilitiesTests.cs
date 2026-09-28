using System.ComponentModel;
using System.Text;

namespace Polhem.Base.UnitTests
{
    public class FileUtilitiesTests : IDisposable
    {
        private readonly string _tempDir;

        public FileUtilitiesTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "polhem-base-fileutils-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch (IOException)
            {
                // Temp files may still be held by test runner; ignore on teardown.
            }
            catch (UnauthorizedAccessException)
            {
                // Temp files may still be held by test runner; ignore on teardown.
            }
            GC.SuppressFinalize(this);
        }

        private string TempPath(string relative) => Path.Combine(_tempDir, relative);

        [Fact]
        [DisplayName("FileWriteTextAtomic writes UTF-8 without a BOM and reads back")]
        public void FileWriteTextAtomic_DefaultEncoding_NoBom()
        {
            string path = TempPath("atomic.txt");
            FileUtilities.FileWriteTextAtomic(path, "哈囉 World");

            byte[] raw = File.ReadAllBytes(path);
            Assert.False(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF,
                "FileWriteTextAtomic must not write a UTF-8 BOM");
            Assert.Equal("哈囉 World", FileUtilities.FileReadText(path));
        }

        [Fact]
        [DisplayName("FileWriteTextAtomic replaces an existing file (the overwrite semantics of the three-argument File.Move)")]
        public void FileWriteTextAtomic_ExistingFile_IsReplaced()
        {
            string path = TempPath("atomic-replace.txt");
            FileUtilities.FileWriteTextAtomic(path, "OLD");

            FileUtilities.FileWriteTextAtomic(path, "NEW");

            Assert.Equal("NEW", FileUtilities.FileReadText(path));
        }

        [Fact]
        [DisplayName("FileWriteTextAtomic leaves no temporary file behind")]
        public void FileWriteTextAtomic_LeavesNoTempFile()
        {
            string dir = TempPath("atomic-dir");
            string path = Path.Combine(dir, "target.xml");

            FileUtilities.FileWriteTextAtomic(path, "<root/>");

            Assert.Equal("target.xml", Assert.Single(Directory.GetFiles(dir).Select(Path.GetFileName)));
        }

        [Fact]
        [DisplayName("FileWriteTextAtomic creates the target directory")]
        public void FileWriteTextAtomic_CreatesDirectory()
        {
            string path = TempPath(Path.Combine("nested", "deeper", "file.txt"));

            FileUtilities.FileWriteTextAtomic(path, "x");

            Assert.True(File.Exists(path));
        }

        [Fact]
        [DisplayName("FileWriteText writes UTF-8 without a BOM by default and reads back")]
        public void FileWriteText_DefaultEncoding_NoBom()
        {
            string path = TempPath("write.txt");
            FileUtilities.FileWriteText(path, "哈囉 World");

            byte[] raw = File.ReadAllBytes(path);
            // 0xEF 0xBB 0xBF is the UTF-8 BOM
            Assert.False(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF,
                "FileWriteText must not write a UTF-8 BOM by default");

            Assert.Equal("哈囉 World", FileUtilities.FileReadText(path));
        }

        [Fact]
        [DisplayName("FileWriteText writes with the specified encoding (UTF-16)")]
        public void FileWriteText_ExplicitEncoding_WritesAccordingly()
        {
            string path = TempPath("utf16.txt");
            var utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            FileUtilities.FileWriteText(path, "Hi", utf16);

            byte[] raw = File.ReadAllBytes(path);
            // UTF-16 LE BOM = 0xFF 0xFE
            Assert.True(raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE);
        }

        [Fact]
        [DisplayName("FileReadText returns an empty string when the file does not exist")]
        public void FileReadText_MissingFile_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, FileUtilities.FileReadText(TempPath("no-file.txt")));
        }

        [Fact]
        [DisplayName("FileWriteText creates the parent directories")]
        public void FileWriteText_AutoCreatesParentDirectory()
        {
            string nestedDir = TempPath("auto/nested");
            Assert.False(Directory.Exists(nestedDir));

            string path = Path.Combine(nestedDir, "deep.txt");
            FileUtilities.FileWriteText(path, "ok");

            Assert.True(File.Exists(path));
            Assert.Equal("ok", FileUtilities.FileReadText(path));
        }

        [Theory]
        [InlineData(@"C:\temp\file.txt", true)]
        [InlineData(@"D:\folder", true)]
        [InlineData(@"\\server\share\file", true)]
        [InlineData("http://example.com", false)]
        [InlineData("relative/path", false)]
        [InlineData("", false)]
        [DisplayName("IsLocalPath recognizes Windows drive and UNC paths")]
        public void IsLocalPath_RecognizesLocalPaths(string input, bool expected)
        {
            Assert.Equal(expected, FileUtilities.IsLocalPath(input));
        }

        [Fact]
        [DisplayName("GetAssemblyPath returns a non-empty directory string")]
        public void GetAssemblyPath_ReturnsNonEmpty()
        {
            string path = FileUtilities.GetAssemblyPath();
            Assert.False(string.IsNullOrEmpty(path));
        }

        private const UnixFileMode GroupOrOther =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        [Fact]
        [DisplayName("FileWriteOwnerOnlyText creates a file only its owner can read, with the given content")]
        public void FileWriteOwnerOnlyText_NewFile_IsOwnerOnly()
        {
            string path = TempPath("secret.key");

            FileUtilities.FileWriteOwnerOnlyText(path, "s3cret", overwrite: false);

            Assert.Equal("s3cret", File.ReadAllText(path));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }
        }

        [Fact]
        [DisplayName("FileWriteOwnerOnlyText replacing a world-readable file leaves an owner-only file")]
        public void FileWriteOwnerOnlyText_Overwrite_TightensExistingFile()
        {
            string path = TempPath("apikey.txt");
            File.WriteAllText(path, "old");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }

            FileUtilities.FileWriteOwnerOnlyText(path, "new", overwrite: true);

            Assert.Equal("new", File.ReadAllText(path));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(path) & GroupOrOther);
            }
        }

        [Fact]
        [DisplayName("FileWriteOwnerOnlyText without overwrite fails on an existing file and keeps its content")]
        public void FileWriteOwnerOnlyText_NoOverwrite_ExistingFile_Throws()
        {
            string path = TempPath("taken.key");
            File.WriteAllText(path, "first");

            Assert.ThrowsAny<IOException>(() => FileUtilities.FileWriteOwnerOnlyText(path, "second", overwrite: false));

            Assert.Equal("first", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(_tempDir));
        }

        [Fact]
        [DisplayName("FileWriteOwnerOnlyText does not create a missing directory")]
        public void FileWriteOwnerOnlyText_MissingDirectory_Throws()
        {
            string path = TempPath(Path.Combine("missing", "secret.key"));

            Assert.ThrowsAny<IOException>(() => FileUtilities.FileWriteOwnerOnlyText(path, "s", overwrite: true));
            Assert.False(Directory.Exists(TempPath("missing")));
        }

        [Fact]
        [DisplayName("EnsureFileExists names only the file in the message and keeps the full path on FileName")]
        public void EnsureFileExists_MissingFile_MessageHasNoDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "polhem-missing-" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(directory, "Employee.FormSchema.xml");

            var ex = Assert.Throws<FileNotFoundException>(() => FileUtilities.EnsureFileExists(filePath));

            Assert.Contains("'Employee.FormSchema.xml'", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(directory, ex.Message, StringComparison.Ordinal);
            Assert.Equal(filePath, ex.FileName);
        }

        [Fact]
        [DisplayName("EnsureFileExists returns for a file that exists")]
        public void EnsureFileExists_ExistingFile_DoesNotThrow()
        {
            string filePath = Path.GetTempFileName();
            try
            {
                Assert.Null(Record.Exception(() => FileUtilities.EnsureFileExists(filePath)));
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }
}
