using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Polhem.Analyzers.UnitTests
{
    /// <summary>
    /// An <see cref="AdditionalText"/> for tests that simulates a definition file with an in-memory string.
    /// </summary>
    internal sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="path">The simulated file path.</param>
        /// <param name="content">The file content.</param>
        public TestAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        /// <inheritdoc />
        public override string Path { get; }

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
