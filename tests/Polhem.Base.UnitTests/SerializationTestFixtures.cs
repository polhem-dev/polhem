using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Base class providing a temp directory and disposal pattern for serialization tests.
    /// </summary>
    public abstract class SerializationTestBase : IDisposable
    {
        protected readonly string _tempDir;

        protected SerializationTestBase()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "polhem-base-serialize-" + Guid.NewGuid().ToString("N"));
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

        protected string TempPath(string fileName) => Path.Combine(_tempDir, fileName);
    }

    /// <summary>
    /// Test payload used to verify serialization round-trips.
    /// </summary>
    public class SerializationTestPayload : IObjectSerializeFile
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }

        private string _objectFilePath = string.Empty;
        [XmlIgnore, JsonIgnore]
        public string ObjectFilePath => _objectFilePath;
        public void SetObjectFilePath(string filePath) => _objectFilePath = filePath;
    }

    /// <summary>
    /// Payload whose <see cref="Tags"/> list is gated by a get-only <c>Specified</c> property, the
    /// convention both codecs honour for omitting empty collections, declared on a base class the way
    /// definition types declare it.
    /// </summary>
    public class SpecifiedPayloadBase : IObjectSerializeBase
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = [];

        [XmlIgnore, JsonIgnore]
        public bool TagsSpecified => Tags.Count > 0;
    }

    /// <summary>
    /// The subclass actually serialized, so the tests cover a <c>Specified</c> property inherited from a base class.
    /// </summary>
    public class SpecifiedPayload : SpecifiedPayloadBase
    {
        /// <summary>
        /// Still written to JSON when empty, because only the <c>Specified</c> convention is consulted there.
        /// </summary>
        /// <remarks>
        /// Declared here, not on the base class: the reflection-only <c>XmlSerializer</c> throws on a
        /// <c>ShouldSerialize</c> method inherited from a base class.
        /// </remarks>
        public List<string> Notes { get; set; } = [];

        public bool ShouldSerializeNotes() => Notes.Count > 0;
    }
}
