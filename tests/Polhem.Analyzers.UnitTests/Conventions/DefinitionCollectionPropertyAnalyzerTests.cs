using Polhem.Base.Collections;
using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Conventions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Conventions
{
    /// <summary>
    /// Tests for POLHEM3002 (definition-layer collection properties must use framework collection types).
    /// </summary>
    /// <remarks>
    /// The rule applies only inside the <c>Polhem.Definition</c> assembly, so the tests name the assembly with
    /// <c>RunOnSourceAs</c>. With the default name the rule stays silent and the tests always pass.
    /// </remarks>
    public class DefinitionCollectionPropertyAnalyzerTests
    {
        private const string DefinitionAssembly = "Polhem.Definition";

        private static readonly Type[] s_anchors =
        {
            typeof(CollectionBase<>),
            typeof(CollectionItem),
        };

        [Theory]
        [InlineData("List<string>")]
        [InlineData("Collection<string>")]
        [DisplayName("A definition-layer property declared as a plain collection reports POLHEM3002")]
        public void PlainCollectionProperty_ReportsDiagnostic(string propertyType)
        {
            const string template = """
                using System.Collections.Generic;
                using System.Collections.ObjectModel;

                public sealed class SampleSettings
                {
                    public __TYPE__ Items { get; set; } = new __TYPE__();
                }
                """;

            var source = template.Replace("__TYPE__", propertyType, StringComparison.Ordinal);

            // Act
            var diagnostics = AnalyzerRunner.RunOnSourceAs(
                new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM3002", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Items'", message, StringComparison.Ordinal);
            Assert.Contains("KeyCollectionBase or CollectionBase", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A property using a framework collection type reports nothing")]
        public void FrameworkCollectionProperty_ReportsNothing()
        {
            const string source = """
                using Polhem.Definition.Collections;
                using MessagePack;

                [MessagePackObject(keyAsPropertyName: true)]
                public sealed class SampleItem : CollectionItem
                {
                    public string Name { get; set; } = string.Empty;
                }

                public sealed class SampleItems : CollectionBase<SampleItem>
                {
                }

                public sealed class SampleSettings
                {
                    public SampleItems Items { get; set; } = new SampleItems();
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSourceAs(
                new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Assemblies other than Polhem.Definition are not subject to this rule (a plain List is correct for cross-layer DTOs)")]
        public void OutsideDefinitionAssembly_StaysSilent()
        {
            const string source = """
                using System.Collections.Generic;

                public sealed class CheckPackageUpdateArgs
                {
                    public List<string> Queries { get; set; } = new List<string>();
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSourceAs(
                new DefinitionCollectionPropertyAnalyzer(), "Polhem.Business", source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Non-public properties are not subject to the rule")]
        public void NonPublicProperty_ReportsNothing()
        {
            const string source = """
                using System.Collections.Generic;

                public sealed class SampleSettings
                {
                    internal List<string> Items { get; set; } = new List<string>();
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSourceAs(
                new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A generic property that is not a collection is not falsely reported")]
        public void OtherGenericProperty_ReportsNothing()
        {
            const string source = """
                using System.Collections.Generic;

                public sealed class SampleSettings
                {
                    public Dictionary<string, string> Map { get; set; } = new Dictionary<string, string>();

                    public string? Optional { get; set; }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSourceAs(
                new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
