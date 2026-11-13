using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Polhem.Analyzers.Conventions;
using Polhem.Analyzers.Serialization;

namespace Polhem.Analyzers.UnitTests.Serialization
{
    /// <summary>
    /// Tests for the collection rules (POLHEM3002, POLHEM4005 and POLHEM4006) over compilations that reference only
    /// the core library, so the framework collection bases are either absent or declared in the source itself. That
    /// is how the rules see a project that does not use the framework collections, or only some of them.
    /// </summary>
    public class SourceDefinedCollectionAnalyzerTests
    {
        private const string DefinitionAssembly = "Polhem.Definition";

        // Stand-ins for the framework bases, declared under their real metadata names.
        private const string CollectionBaseOnly = """
            namespace Polhem.Core.Collections
            {
                public abstract class CollectionBase<T>
                {
                    public System.Collections.Generic.List<T> Proxy { get; set; } = new System.Collections.Generic.List<T>();
                    public void Add(T item) { }
                }
            }
            """;

        private const string CollectionItemOnly = """
            namespace Polhem.Core.Collections
            {
                public abstract class CollectionItem { }
            }
            """;

        [Fact]
        [DisplayName("POLHEM3002 stays silent in the definition assembly when no framework collection base is available")]
        public void DefinitionCollectionProperty_NoFrameworkCollections_StaysSilent()
        {
            const string source = """
                public sealed class SampleSettings
                {
                    public System.Collections.Generic.List<string> Items { get; set; } = new System.Collections.Generic.List<string>();
                }
                """;

            Assert.Empty(Run(new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source));
        }

        [Fact]
        [DisplayName("POLHEM3002 leaves the collection base's own plain-list plumbing alone but reports a definition type")]
        public void DefinitionCollectionProperty_PropertyOnCollectionBase_IsExempt()
        {
            string source = CollectionBaseOnly + """

                public sealed class SampleSettings
                {
                    public System.Collections.Generic.List<string> Items { get; set; } = new System.Collections.Generic.List<string>();
                }
                """;

            var diagnostic = Assert.Single(Run(new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source));
            Assert.Equal("POLHEM3002", diagnostic.Id);
            Assert.Contains("SampleSettings", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("POLHEM3002 ignores static, array and non-generic properties")]
        public void DefinitionCollectionProperty_StaticArrayAndNonGeneric_StaySilent()
        {
            string source = CollectionBaseOnly + """

                public sealed class SampleSettings
                {
                    public static System.Collections.Generic.List<string> Shared { get; set; } = new System.Collections.Generic.List<string>();
                    public string[] Codes { get; set; } = new string[0];
                    public string Name { get; set; } = string.Empty;
                }
                """;

            Assert.Empty(Run(new DefinitionCollectionPropertyAnalyzer(), DefinitionAssembly, source));
        }

        [Fact]
        [DisplayName("POLHEM4005 and POLHEM4006 stay silent when the compilation has no framework collection types")]
        public void CollectionShapeRules_NoFrameworkCollections_StaySilent()
        {
            const string source = """
                public sealed class Items
                {
                    public Items(int capacity) { }
                    public void Add(string item) { }
                    public void Add(int item) { }
                }
                """;

            Assert.Empty(Run(new CollectionAddOverloadAnalyzer(), "Sample", source));
            Assert.Empty(Run(new ParameterlessConstructorAnalyzer(), "Sample", source));
        }

        [Fact]
        [DisplayName("POLHEM4005 does not count static or non-public Add methods of a framework collection")]
        public void CollectionAddOverload_StaticAndNonPublicAdd_StaySilent()
        {
            string source = CollectionBaseOnly + """

                public sealed class SampleItems : Polhem.Core.Collections.CollectionBase<string>
                {
                    public static void Add(int value) { }
                    internal void Add(long value) { }
                    private void Add(double value) { }
                }
                """;

            Assert.Empty(Run(new CollectionAddOverloadAnalyzer(), "Sample", source));
        }

        [Fact]
        [DisplayName("POLHEM4005 reports an extra public Add on a collection derived from a non-generic subclass")]
        public void CollectionAddOverload_GrandchildCollection_Reports()
        {
            string source = CollectionBaseOnly + """

                public class SampleItems : Polhem.Core.Collections.CollectionBase<string> { }

                public sealed class SpecialItems : SampleItems
                {
                    public void Add(int value) { }
                }
                """;

            var diagnostic = Assert.Single(Run(new CollectionAddOverloadAnalyzer(), "Sample", source));
            Assert.Equal("POLHEM4005", diagnostic.Id);
        }

        [Fact]
        [DisplayName("POLHEM4005 ignores a struct, even one with several Add methods")]
        public void CollectionAddOverload_Struct_StaysSilent()
        {
            string source = CollectionBaseOnly + """

                public struct SampleValue
                {
                    public void Add(int value) { }
                    public void Add(long value) { }
                }
                """;

            Assert.Empty(Run(new CollectionAddOverloadAnalyzer(), "Sample", source));
        }

        [Fact]
        [DisplayName("POLHEM4006 reports a collection item whose only parameterless constructor is not public")]
        public void ParameterlessConstructor_PrivateParameterlessConstructor_Reports()
        {
            string source = CollectionItemOnly + """

                public sealed class SampleItem : Polhem.Core.Collections.CollectionItem
                {
                    private SampleItem() { }
                }
                """;

            var diagnostic = Assert.Single(Run(new ParameterlessConstructorAnalyzer(), "Sample", source));
            Assert.Equal("POLHEM4006", diagnostic.Id);
        }

        [Fact]
        [DisplayName("POLHEM4006 ignores static classes, structs and types unrelated to the framework collections")]
        public void ParameterlessConstructor_StaticStructAndUnrelated_StaySilent()
        {
            string source = CollectionItemOnly + """

                public static class Helpers { }

                public struct SampleValue
                {
                    public SampleValue(int value) { }
                }

                public sealed class Unrelated
                {
                    public Unrelated(int value) { }
                }
                """;

            Assert.Empty(Run(new ParameterlessConstructorAnalyzer(), "Sample", source));
        }

        private static ImmutableArray<Diagnostic> Run(DiagnosticAnalyzer analyzer, string assemblyName, string source)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName: assemblyName,
                syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
                references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

            return compilation.WithAnalyzers(ImmutableArray.Create(analyzer))
                .GetAnalyzerDiagnosticsAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
    }
}
