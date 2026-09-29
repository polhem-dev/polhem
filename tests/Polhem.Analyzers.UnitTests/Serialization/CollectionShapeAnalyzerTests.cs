using Polhem.Core.Collections;
using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Serialization;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Serialization
{
    /// <summary>
    /// Tests for POLHEM4005 (a framework collection has only one public Add) and POLHEM4006 (a serialized type needs a parameterless constructor).
    /// </summary>
    public class CollectionShapeAnalyzerTests
    {
        private static readonly Type[] s_anchors =
        {
            typeof(CollectionBase<>),
            typeof(CollectionItem),
            typeof(KeyCollectionBase<>),
            typeof(KeyCollectionItem),
        };

        private const string KeyItemDeclaration = """
            using Polhem.Core.Collections;

            public sealed class SampleKeyItem : KeyCollectionItem
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        private const string ItemDeclaration = """
            using Polhem.Core.Collections;

            public sealed class SampleItem : CollectionItem
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        [Fact]
        [DisplayName("A collection subclass that adds a public Add overload reports POLHEM4005")]
        public void ExtraPublicAddOverload_ReportsDiagnostic()
        {
            var source = ItemDeclaration + """

                public sealed class SampleItems : CollectionBase<SampleItem>
                {
                    public void Add(string name) => Add(new SampleItem { Name = name });
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new CollectionAddOverloadAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM4005", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("AmbiguousMatchException", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A keyed collection subclass that adds a public Add overload reports POLHEM4005")]
        public void KeyCollectionExtraPublicAddOverload_ReportsDiagnostic()
        {
            // The analyzer finds KeyCollectionBase<T> by its metadata name. If that name is wrong the
            // analyzer reports nothing and every other test in this class still passes, so the keyed
            // base needs a case of its own.
            var source = KeyItemDeclaration + """

                public sealed class SampleKeyItems : KeyCollectionBase<SampleKeyItem>
                {
                    public void Add(string name) => Add(new SampleKeyItem { Name = name });
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new CollectionAddOverloadAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM4005", diagnostic.Id);
        }

        [Fact]
        [DisplayName("A keyed collection item with only a parameterized constructor reports POLHEM4006")]
        public void KeyCollectionItemWithoutParameterlessCtor_ReportsDiagnostic()
        {
            // Same reason as the keyed-collection case above: KeyCollectionItem is looked up by its
            // metadata name and has no other test that fails when that name is wrong.
            const string source = """
                using Polhem.Core.Collections;

                public sealed class SampleKeyItem : KeyCollectionItem
                {
                    public SampleKeyItem(string name) => Name = name;

                    public string Name { get; set; }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new ParameterlessConstructorAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM4006", diagnostic.Id);
        }

        [Fact]
        [DisplayName("A collection subclass that adds no Add does not report POLHEM4005")]
        public void NoExtraAdd_ReportsNothing()
        {
            var source = ItemDeclaration + """

                public sealed class SampleItems : CollectionBase<SampleItem>
                {
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new CollectionAddOverloadAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A type that is not a framework collection may have an Add method without POLHEM4005")]
        public void NonCollectionTypeWithAdd_ReportsNothing()
        {
            const string source = """
                public sealed class Basket
                {
                    public void Add(string item) { }

                    public void Add(int quantity) { }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new CollectionAddOverloadAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A collection subclass with only a parameterized constructor reports POLHEM4006")]
        public void CollectionWithoutParameterlessCtor_ReportsDiagnostic()
        {
            var source = ItemDeclaration + """

                public sealed class SampleItems : CollectionBase<SampleItem>
                {
                    public SampleItems(string label) => Label = label;

                    public string Label { get; set; }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new ParameterlessConstructorAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM4006", diagnostic.Id);
            Assert.Contains("MissingMethodException", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A collection item with only a parameterized constructor reports POLHEM4006")]
        public void ContractTypeWithoutParameterlessCtor_ReportsDiagnostic()
        {
            const string source = """
                using Polhem.Core.Collections;

                public sealed class Sample : CollectionItem
                {
                    public Sample(string name) => Name = name;

                    public string Name { get; set; }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new ParameterlessConstructorAnalyzer(), source, s_anchors);

            // Assert
            Assert.Equal("POLHEM4006", Assert.Single(diagnostics).Id);
        }

        [Fact]
        [DisplayName("A type with both a parameterless and a parameterized constructor does not report POLHEM4006")]
        public void BothConstructors_ReportNothing()
        {
            const string source = """
                using Polhem.Core.Collections;

                public sealed class Sample : CollectionItem
                {
                    public Sample() { }

                    public Sample(string name) => Name = name;

                    public string Name { get; set; } = string.Empty;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new ParameterlessConstructorAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A type with no declared constructor has an implicit parameterless one and does not report POLHEM4006")]
        public void ImplicitConstructor_ReportsNothing()
        {
            const string source = """
                using Polhem.Core.Collections;

                public sealed class Sample : CollectionItem
                {
                    public string Name { get; set; } = string.Empty;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new ParameterlessConstructorAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("An abstract type is never constructed by the deserializer and does not report POLHEM4006")]
        public void AbstractType_ReportsNothing()
        {
            const string source = """
                using Polhem.Core.Collections;

                public abstract class Sample : CollectionItem
                {
                    protected SampleBase(string name) => Name = name;

                    public string Name { get; set; }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(new ParameterlessConstructorAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
