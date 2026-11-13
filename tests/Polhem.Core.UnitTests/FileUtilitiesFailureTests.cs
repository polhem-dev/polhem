using System.ComponentModel;

namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// Tests for the failure path of <see cref="FileUtilities.FileWriteTextAtomic"/>: when the final move fails, the
    /// caller sees the exception and no temporary file is left beside the target.
    /// </summary>
    public sealed class FileUtilitiesFailureTests : IDisposable
    {
        private readonly string _root;

        public FileUtilitiesFailureTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-fileutil-fail-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Fact]
        [DisplayName("FileWriteTextAtomic onto a path that is a directory throws and removes its temporary file")]
        public void FileWriteTextAtomic_TargetIsDirectory_ThrowsAndCleansUp()
        {
            string target = Path.Combine(_root, "occupied");
            Directory.CreateDirectory(target);

            var ex = Record.Exception(() => FileUtilities.FileWriteTextAtomic(target, "content"));

            Assert.True(ex is IOException or UnauthorizedAccessException, $"Unexpected exception: {ex}");
            Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
            Assert.True(Directory.Exists(target));
        }
    }
}
