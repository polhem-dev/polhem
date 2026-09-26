using System.ComponentModel;
using Polhem.Analyzers.Definitions;
using Polhem.Definition;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Keeps the analyzer's built-in ProgId allowlist in sync with the default FormSchemas the framework actually embeds.
    /// </summary>
    /// <remarks>
    /// POLHEM2003 must tell "the ProgId really does not exist" apart from "the framework supplies the ProgId as an
    /// embedded resource and the consumer has no file for it". A false report of the latter is an error-level
    /// misdiagnosis that blocks the build, so the allowlist must match what the framework actually embeds.
    /// </remarks>
    public class FrameworkProgIdsSyncTests
    {
        [Fact]
        [DisplayName("The analyzer's built-in ProgId allowlist matches the FormSchemas embedded in the framework")]
        public void All_MatchesEmbeddedFormSchemas()
        {
            // Arrange
            const string suffix = ".FormSchema.xml";
            var embedded = Defaults.ListEmbedded()
                .Where(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Substring(path.LastIndexOf('/') + 1))
                .Select(name => name.Substring(0, name.Length - suffix.Length))
                .OrderBy(progId => progId, StringComparer.Ordinal)
                .ToArray();

            // Act
            var whitelisted = FrameworkProgIds.All
                .OrderBy(progId => progId, StringComparer.Ordinal)
                .ToArray();

            // Assert
            Assert.NotEmpty(embedded);
            Assert.Equal(embedded, whitelisted);
        }

        [Fact]
        [DisplayName("A ProgId that is not built in is not treated as framework-supplied")]
        public void IsFrameworkSupplied_RejectsConsumerProgIds()
        {
            // Assert
            Assert.False(FrameworkProgIds.IsFrameworkSupplied("Product"));
            Assert.False(FrameworkProgIds.IsFrameworkSupplied("Supplier"));
        }
    }
}
