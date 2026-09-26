using System.ComponentModel;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests.Serialization
{
    /// <summary>
    /// When a definition file fails to deserialize, the exception message must not reveal the server's directory layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The risk is not theoretical: the JSON-RPC error contract maps <see cref="InvalidOperationException"/> to
    /// <c>UserMessage</c>, and that branch returns <c>ex.Message</c> to the caller **as is**. An authenticated remote
    /// caller who hits a corrupt definition file would get the server's absolute path.
    /// That breaks the rule in <c>scanning.md</c> (Leaking sensitive information) that exception messages must not
    /// contain internal paths.
    /// </para>
    /// <para>
    /// The full path goes into <see cref="Exception.Data"/> instead, where only the server-side log sees it.
    /// </para>
    /// </remarks>
    public class FilePathDisclosureTests
    {
        private static string WriteCorruptFile(string extension)
        {
            // The directory name carries a recognizable marker, so a leaked path shows up in the assertion.
            string dir = Path.Combine(Path.GetTempPath(), "polhem-secret-layout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "broken" + extension);
            File.WriteAllText(file, "this is not valid content");
            return file;
        }

        [Theory]
        [InlineData(".xml")]
        [InlineData(".json")]
        [DisplayName("A failed deserialization names only the file in the message, not the path; the full path is in Exception.Data")]
        public void DeserializeFromFile_Corrupt_MessageNamesFileNotPath(string extension)
        {
            string file = WriteCorruptFile(extension);
            string dir = Path.GetDirectoryName(file)!;
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => extension == ".xml"
                    ? XmlCodec.DeserializeFromFile<SampleValue>(file)
                    : JsonCodec.DeserializeFromFile<SampleValue>(file));

                // Control: the message does name the file, so the assertions below are not vacuous on an empty message.
                Assert.Contains(Path.GetFileName(file), ex.Message, StringComparison.Ordinal);

                Assert.DoesNotContain(dir, ex.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("polhem-secret-layout-", ex.Message, StringComparison.Ordinal);

                // The full path is still available, for the server-side log and not for the caller.
                Assert.Equal(file, ex.Data[SerializationErrorData.FilePath]);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>The deserialization target. The content is deliberately broken, so the type itself does not matter.</summary>
        public class SampleValue
        {
            /// <summary>An arbitrary property.</summary>
            public string Name { get; set; } = string.Empty;
        }
    }
}
