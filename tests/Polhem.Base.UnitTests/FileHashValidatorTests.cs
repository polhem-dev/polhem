using System.ComponentModel;
using Polhem.Base.Security;
using System.Text;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for FileHashValidator.
    /// </summary>
    public class FileHashValidatorTests
    {
        private static string CreateTempFile(string content = "Hello Polhem")
        {
            var dir = Path.Combine(Path.GetTempPath(), "PolhemBaseTests");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        [Fact]
        [DisplayName("Verifying a computed SHA256 hash succeeds case-insensitively")]
        public void ComputeAndVerifySha256_ValidFile_VerificationSucceeds()
        {
            var path = CreateTempFile();
            try
            {
                var hex = FileHashValidator.ComputeSha256(path);
                Assert.True(FileHashValidator.VerifySha256(path, hex));
                Assert.True(FileHashValidator.VerifySha256(path, hex.ToLowerInvariant()));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("VerifySha256 returns false for a mismatched hash")]
        public void VerifySha256_MismatchedHash_ReturnsFalse()
        {
            var path = CreateTempFile();
            try
            {
                var wrongHash = new string('0', 64);
                Assert.False(FileHashValidator.VerifySha256(path, wrongHash));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("VerifySha256 returns false when the expected hex has the wrong length")]
        public void VerifySha256_WrongLengthHex_ReturnsFalse()
        {
            var path = CreateTempFile();
            try
            {
                // Valid hex, but not 64 characters long (twice the SHA-256 output size).
                Assert.False(FileHashValidator.VerifySha256(path, "ABCD"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("VerifySha256 returns false for an odd-length hex string")]
        public void VerifySha256_OddLengthHex_ReturnsFalse()
        {
            var path = CreateTempFile();
            try
            {
                Assert.False(FileHashValidator.VerifySha256(path, "ABC"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("VerifySha256 returns false when the hex contains invalid characters")]
        public void VerifySha256_InvalidHexChars_ReturnsFalse()
        {
            var path = CreateTempFile();
            try
            {
                // 64 characters, but some are not hex.
                var invalidHex = "ZZ" + new string('0', 62);
                Assert.False(FileHashValidator.VerifySha256(path, invalidHex));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("VerifySha256 returns false for an empty hex string")]
        public void VerifySha256_EmptyHex_ReturnsFalse()
        {
            var path = CreateTempFile();
            try
            {
                Assert.False(FileHashValidator.VerifySha256(path, string.Empty));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("VerifySha256 throws FileNotFoundException for a missing file")]
        public void VerifySha256_MissingFile_ThrowsFileNotFoundException()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.missing");
            Assert.Throws<FileNotFoundException>(
                () => FileHashValidator.VerifySha256(path, new string('0', 64)));
        }

        [Fact]
        [DisplayName("VerifySha256 throws FileNotFoundException for an empty path")]
        public void VerifySha256_EmptyPath_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(
                () => FileHashValidator.VerifySha256(string.Empty, new string('0', 64)));
        }

        [Fact]
        [DisplayName("ComputeSha256 throws FileNotFoundException for a missing file")]
        public void ComputeSha256_MissingFile_ThrowsFileNotFoundException()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.missing");
            Assert.Throws<FileNotFoundException>(() => FileHashValidator.ComputeSha256(path));
        }

        [Fact]
        [DisplayName("ComputeSha256 throws FileNotFoundException for an empty path")]
        public void ComputeSha256_EmptyPath_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => FileHashValidator.ComputeSha256(string.Empty));
        }
    }
}
