using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for the protection of <see cref="PathOptions"/> against externally supplied path segments.
    /// </summary>
    /// <remarks>
    /// These segments (progId / layoutId / categoryId / tableName / lang / ns) come from API arguments and deserialized
    /// definition objects, so they are untrusted input when composed into file paths.
    /// </remarks>
    public class PathOptionsTraversalTests
    {
        private static PathOptions CreateOptions()
            => new PathOptions { DefinePath = Path.Combine(Path.GetTempPath(), "polhem-define-root") };

        // The `params` constructor rather than a collection expression: the latter starts from a
        // parameterless `TheoryData<string>()`, which binds to the same `params` constructor with an
        // empty array and trips CA1825 on the analyzer set SonarCloud runs.
        public static TheoryData<string> HostilePathSegments()
            => new(
                "../../../etc/passwd",
                "..",
                "a/b",
                @"a\b",
                "/etc/cron.d/x");

        [Theory]
        [MemberData(nameof(HostilePathSegments))]
        [DisplayName("GetFormSchemaFilePath throws ArgumentException for a progId that can escape the root")]
        public void GetFormSchemaFilePath_HostileProgId_Throws(string progId)
        {
            Assert.Throws<ArgumentException>(() => CreateOptions().GetFormSchemaFilePath(progId));
        }

        [Theory]
        [MemberData(nameof(HostilePathSegments))]
        [DisplayName("GetFormLayoutFilePath throws ArgumentException for a layoutId that can escape the root")]
        public void GetFormLayoutFilePath_HostileLayoutId_Throws(string layoutId)
        {
            Assert.Throws<ArgumentException>(() => CreateOptions().GetFormLayoutFilePath(layoutId));
        }

        [Theory]
        [MemberData(nameof(HostilePathSegments))]
        [DisplayName("GetTableSchemaFilePath throws ArgumentException for a categoryId that can escape the root")]
        public void GetTableSchemaFilePath_HostileCategoryId_Throws(string categoryId)
        {
            Assert.Throws<ArgumentException>(() => CreateOptions().GetTableSchemaFilePath(categoryId, "Employee"));
        }

        [Theory]
        [MemberData(nameof(HostilePathSegments))]
        [DisplayName("GetLanguageFilePath throws ArgumentException for a namespace that can escape the root")]
        public void GetLanguageFilePath_HostileNamespace_Throws(string ns)
        {
            Assert.Throws<ArgumentException>(() => CreateOptions().GetLanguageFilePath("zh-TW", ns));
        }

        [Fact]
        [DisplayName("A rooted segment is blocked, because Path.Combine discards every segment before it")]
        public void GetFormSchemaFilePath_RootedSegment_DoesNotEscapeRoot()
        {
            var options = CreateOptions();

            // This is a subtler hole than "..": `Path.Combine(a, b, "/x")` returns "/x" directly and never goes
            // through `DefinePath`. If it were not blocked, the file could land anywhere in the file system.
            var ex = Assert.Throws<ArgumentException>(
                () => options.GetFormSchemaFilePath("/tmp/evil"));
            Assert.Contains("illegal path characters", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Employee")]
        [InlineData("common")]
        [InlineData("zh-TW")]
        [InlineData("")]
        [DisplayName("Valid segments compose the path as usual")]
        public void ValidSegments_ProduceExpectedPath(string progId)
        {
            var options = CreateOptions();

            var path = options.GetFormSchemaFilePath(progId);

            Assert.StartsWith(options.DefinePath, path, StringComparison.Ordinal);
        }
    }
}
