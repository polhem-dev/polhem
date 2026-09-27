using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;


namespace Polhem.Definition.UnitTests.Security
{
    /// <summary>
    /// Tests for source loading and error paths of MasterKeyProvider.
    /// </summary>
    [Collection(Polhem.Definition.UnitTests.ProcessWideStateCollection.Name)]
    public class MasterKeyProviderTests
    {
        [Fact]
        [DisplayName("GetMasterKey returns the bytes of a file source that holds valid Base64")]
        public void GetMasterKey_FileSource_ReturnsBytes()
        {
            // Arrange
            byte[] expected = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            string filePath = Path.Combine(Path.GetTempPath(), $"polhem-mk-{Guid.NewGuid()}.key");
            File.WriteAllText(filePath, Convert.ToBase64String(expected));

            try
            {
                // Act
                byte[] actual = MasterKeyProvider.GetMasterKey(new MasterKeySource
                {
                    Type = MasterKeySourceType.File,
                    Value = filePath
                }, definePath: string.Empty);

                // Assert
                Assert.Equal(expected, actual);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey throws FileNotFoundException for a missing file with autoCreate=false")]
        public void GetMasterKey_FileMissing_NoAutoCreate_ThrowsFileNotFound()
        {
            // Arrange
            string missing = Path.Combine(Path.GetTempPath(), $"polhem-mk-missing-{Guid.NewGuid()}.key");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
                MasterKeyProvider.GetMasterKey(new MasterKeySource
                {
                    Type = MasterKeySourceType.File,
                    Value = missing
                }, definePath: string.Empty));
        }

        [Fact]
        [DisplayName("GetMasterKey creates the file and returns its content for a missing file with autoCreate=true")]
        public void GetMasterKey_FileMissing_AutoCreate_CreatesAndReturnsKey()
        {
            // Arrange
            string filePath = Path.Combine(Path.GetTempPath(), $"polhem-mk-auto-{Guid.NewGuid()}.key");

            try
            {
                // Act
                byte[] result = MasterKeyProvider.GetMasterKey(
                    new MasterKeySource { Type = MasterKeySourceType.File, Value = filePath },
                    definePath: string.Empty,
                    autoCreate: true);

                // Assert
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                Assert.True(File.Exists(filePath));
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey with autoCreate=true creates the key file readable by its owner only")]
        public void GetMasterKey_FileMissing_AutoCreate_CreatesOwnerOnlyFile()
        {
            string filePath = Path.Combine(Path.GetTempPath(), $"polhem-mk-mode-{Guid.NewGuid()}.key");

            try
            {
                byte[] result = MasterKeyProvider.GetMasterKey(
                    new MasterKeySource { Type = MasterKeySourceType.File, Value = filePath },
                    definePath: string.Empty,
                    autoCreate: true);

                Assert.NotEmpty(result);
                if (!OperatingSystem.IsWindows())
                {
                    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(filePath));
                }
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey throws InvalidOperationException when the file content is not Base64")]
        public void GetMasterKey_InvalidBase64Content_ThrowsInvalidOperation()
        {
            // Arrange
            string filePath = Path.Combine(Path.GetTempPath(), $"polhem-mk-bad-{Guid.NewGuid()}.key");
            File.WriteAllText(filePath, "@@not-base64@@");

            try
            {
                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    MasterKeyProvider.GetMasterKey(new MasterKeySource
                    {
                        Type = MasterKeySourceType.File,
                        Value = filePath
                    }, definePath: string.Empty));
                Assert.IsType<FormatException>(ex.InnerException);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey throws InvalidOperationException when the file content is empty")]
        public void GetMasterKey_EmptyFileContent_ThrowsInvalidOperation()
        {
            // Arrange
            string filePath = Path.Combine(Path.GetTempPath(), $"polhem-mk-empty-{Guid.NewGuid()}.key");
            File.WriteAllText(filePath, "   ");

            try
            {
                // Act & Assert
                Assert.Throws<InvalidOperationException>(() =>
                    MasterKeyProvider.GetMasterKey(new MasterKeySource
                    {
                        Type = MasterKeySourceType.File,
                        Value = filePath
                    }, definePath: string.Empty));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey returns the bytes of an existing environment variable source")]
        public void GetMasterKey_EnvironmentSource_ReturnsBytes()
        {
            // Arrange
            string varName = $"POLHEM_TEST_MK_{Guid.NewGuid():N}";
            byte[] expected = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            Environment.SetEnvironmentVariable(varName, Convert.ToBase64String(expected));

            try
            {
                // Act
                byte[] actual = MasterKeyProvider.GetMasterKey(new MasterKeySource
                {
                    Type = MasterKeySourceType.Environment,
                    Value = varName
                }, definePath: string.Empty);

                // Assert
                Assert.Equal(expected, actual);
            }
            finally
            {
                Environment.SetEnvironmentVariable(varName, null);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey throws InvalidOperationException for a missing environment variable with autoCreate=false")]
        public void GetMasterKey_EnvironmentMissing_NoAutoCreate_Throws()
        {
            // Arrange
            string varName = $"POLHEM_TEST_MK_MISSING_{Guid.NewGuid():N}";
            Environment.SetEnvironmentVariable(varName, null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                MasterKeyProvider.GetMasterKey(new MasterKeySource
                {
                    Type = MasterKeySourceType.Environment,
                    Value = varName
                }, definePath: string.Empty));
        }

        [Fact]
        [DisplayName("GetMasterKey creates the variable and returns its content for a missing environment variable with autoCreate=true")]
        public void GetMasterKey_EnvironmentMissing_AutoCreate_CreatesAndReturnsKey()
        {
            // Arrange
            string varName = $"POLHEM_TEST_MK_AUTO_{Guid.NewGuid():N}";
            Environment.SetEnvironmentVariable(varName, null);

            try
            {
                // Act
                byte[] result = MasterKeyProvider.GetMasterKey(
                    new MasterKeySource { Type = MasterKeySourceType.Environment, Value = varName },
                    definePath: string.Empty,
                    autoCreate: true);

                // Assert
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)));
            }
            finally
            {
                Environment.SetEnvironmentVariable(varName, null);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey applies the default file name Master.key for a blank file path")]
        public void GetMasterKey_EmptyFilePath_UsesDefaultFileName()
        {
            // Arrange: a blank value is replaced with "Master.key" and looked up under DefinePath.
            // A fresh empty temp directory makes sure the default file does not exist, so the call throws
            // (and existing fixtures such as tests/Define/Master.key cannot interfere).
            var tempPath = Path.Combine(Path.GetTempPath(), $"polhem-mk-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempPath);
            try
            {
                var source = new MasterKeySource
                {
                    Type = MasterKeySourceType.File,
                    Value = "   "
                };

                // Act & Assert: definePath is an empty temp folder, so the default Master.key does not exist and the call throws.
                var ex = Record.Exception(() => MasterKeyProvider.GetMasterKey(source, tempPath));
                Assert.NotNull(ex);
            }
            finally
            {
                try { Directory.Delete(tempPath, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("GetMasterKey with a blank variable name reads the value of POLHEM_MASTER_KEY")]
        public void GetMasterKey_EmptyVarName_ReadsPolhemMasterKey()
        {
            // Deployments set this variable by name. The test below only checks that a missing
            // variable throws, which holds for any default name, so it cannot tell a wrong default.
            string? original = Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY");
            var expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", Convert.ToBase64String(expected));

            try
            {
                var source = new MasterKeySource
                {
                    Type = MasterKeySourceType.Environment,
                    Value = "   "
                };

                Assert.Equal(expected, MasterKeyProvider.GetMasterKey(source, definePath: string.Empty));
            }
            finally
            {
                Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", original);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey with a blank variable name falls back to POLHEM_MASTER_KEY and throws when it is not set")]
        public void GetMasterKey_EmptyVarName_UsesDefaultVarName()
        {
            // Arrange: make sure the default variable is empty before the call.
            string? original = Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY");
            Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", null);

            try
            {
                var source = new MasterKeySource
                {
                    Type = MasterKeySourceType.Environment,
                    Value = "   "
                };

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => MasterKeyProvider.GetMasterKey(source, definePath: string.Empty));
            }
            finally
            {
                Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", original);
            }
        }

        [Fact]
        [DisplayName("A missing POLHEM_MASTER_KEY while BEE_MASTER_KEY is set says the Bee.NET variable should be renamed")]
        public void GetMasterKey_DefaultVarMissingButBeeVarSet_MessageNamesBeeVar()
        {
            string? original = Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY");
            string? originalBee = Environment.GetEnvironmentVariable("BEE_MASTER_KEY");
            Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", null);
            Environment.SetEnvironmentVariable("BEE_MASTER_KEY", Convert.ToBase64String(new byte[64]));
            try
            {
                var source = new MasterKeySource { Type = MasterKeySourceType.Environment, Value = string.Empty };

                var ex = Assert.Throws<InvalidOperationException>(() => MasterKeyProvider.GetMasterKey(source, definePath: string.Empty));

                Assert.Contains("POLHEM_MASTER_KEY", ex.Message, StringComparison.Ordinal);
                Assert.Contains("BEE_MASTER_KEY", ex.Message, StringComparison.Ordinal);
                Assert.Contains("Migrating from Bee.NET", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", original);
                Environment.SetEnvironmentVariable("BEE_MASTER_KEY", originalBee);
            }
        }

        [Fact]
        [DisplayName("A missing POLHEM_MASTER_KEY without BEE_MASTER_KEY carries no Bee.NET hint")]
        public void GetMasterKey_DefaultVarMissingNoBeeVar_MessageHasNoBeeHint()
        {
            string? original = Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY");
            string? originalBee = Environment.GetEnvironmentVariable("BEE_MASTER_KEY");
            Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", null);
            Environment.SetEnvironmentVariable("BEE_MASTER_KEY", null);
            try
            {
                var source = new MasterKeySource { Type = MasterKeySourceType.Environment, Value = string.Empty };

                var ex = Assert.Throws<InvalidOperationException>(() => MasterKeyProvider.GetMasterKey(source, definePath: string.Empty));

                Assert.DoesNotContain("BEE_MASTER_KEY", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", original);
                Environment.SetEnvironmentVariable("BEE_MASTER_KEY", originalBee);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey throws InvalidOperationException for an unsupported Type (the default branch)")]
        public void GetMasterKey_UnsupportedType_ThrowsInvalidOperation()
        {
            // The enum only has File=0 and Environment=1, so casting 99 reaches the switch default.
            var source = new MasterKeySource
            {
                Type = (MasterKeySourceType)99,
                Value = "irrelevant"
            };

            var ex = Assert.Throws<InvalidOperationException>(() => MasterKeyProvider.GetMasterKey(source, definePath: string.Empty));
            Assert.Contains("Unsupported", ex.Message);
        }

        [Fact]
        [DisplayName("GetMasterKey with autoCreate=true reads the existing file when it already exists")]
        public void GetMasterKey_FileExists_AutoCreate_ReturnsExistingKey()
        {
            // The file exists, so `File.Exists` is true and the code goes straight to `ReadAllTextShared` without the CreateNew path.
            byte[] expected = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            string filePath = Path.Combine(Path.GetTempPath(), $"polhem-mk-existing-{Guid.NewGuid()}.key");
            File.WriteAllText(filePath, Convert.ToBase64String(expected));

            try
            {
                byte[] actual = MasterKeyProvider.GetMasterKey(
                    new MasterKeySource { Type = MasterKeySourceType.File, Value = filePath },
                    definePath: string.Empty,
                    autoCreate: true);

                Assert.Equal(expected, actual);
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }
}
