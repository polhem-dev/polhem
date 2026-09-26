using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Polhem.Analyzers.UnitTests
{
    /// <summary>
    /// Test harness that drives an analyzer over a minimal compilation and returns its diagnostics.
    /// </summary>
    /// <remarks>
    /// Definition file rules (POLHEM1xxx / POLHEM2xxx) do not depend on any C# type, so the compilation needs only
    /// a placeholder syntax tree and the runtime's <c>System.Private.CoreLib</c>. No reference assemblies are
    /// resolved from NuGet, so the tests run offline.
    /// </remarks>
    internal static class AnalyzerRunner
    {
        private const string PlaceholderSource = "internal sealed class Placeholder { }";

        /// <summary>
        /// The default simulated assembly name.
        /// </summary>
        private const string DefaultAssemblyName = "Polhem.Analyzers.TestAssembly";

        /// <summary>
        /// Runs the analyzer over the given definition files and returns the diagnostics it produces.
        /// </summary>
        /// <param name="analyzer">The analyzer to run.</param>
        /// <param name="additionalFiles">The simulated definition files (path and content).</param>
        /// <returns>The diagnostics the analyzer produces.</returns>
        public static ImmutableArray<Diagnostic> Run(
            DiagnosticAnalyzer analyzer,
            params (string Path, string Content)[] additionalFiles)
            => Run(analyzer, PlaceholderSource, additionalFiles);

        /// <summary>
        /// Runs the analyzer over the given source code and returns the diagnostics it produces.
        /// </summary>
        /// <param name="analyzer">The analyzer to run.</param>
        /// <param name="source">The C# source code to analyze.</param>
        /// <param name="anchorTypes">
        /// Representatives of the external types the source refers to (such as <c>MessagePack.KeyAttribute</c>),
        /// which make sure their assemblies are in the reference set.
        /// </param>
        /// <returns>The diagnostics the analyzer produces.</returns>
        /// <remarks>
        /// IMPORTANT: a rule that needs an external attribute must be given the matching anchor. Relying on the
        /// assemblies already loaded in the <c>AppDomain</c> is not reliable: assembly loading is lazy, so when a
        /// single test runs alone the assembly may not be loaded yet. The source then compiles to an error type and
        /// the rule silently does not fire, which looks exactly like a bug in the rule.
        /// </remarks>
        public static ImmutableArray<Diagnostic> RunOnSource(
            DiagnosticAnalyzer analyzer,
            string source,
            params Type[] anchorTypes)
            => Run(analyzer, source, BuildReferences(anchorTypes), [], DefaultAssemblyName);

        /// <summary>
        /// Runs the analyzer under the given assembly name, for rules that apply only inside a specific assembly.
        /// </summary>
        /// <param name="analyzer">The analyzer to run.</param>
        /// <param name="assemblyName">The simulated assembly name, for example <c>Polhem.Definition</c>.</param>
        /// <param name="source">The C# source code to analyze.</param>
        /// <param name="anchorTypes">Representatives of the external types the source refers to.</param>
        /// <returns>The diagnostics the analyzer produces.</returns>
        /// <remarks>
        /// POLHEM3002 applies only inside the <c>Polhem.Definition</c> assembly (see the rule's remarks). With the
        /// default assembly name the rule stays silent and the test always passes, which cannot be told apart from
        /// a rule that is not implemented.
        /// </remarks>
        public static ImmutableArray<Diagnostic> RunOnSourceAs(
            DiagnosticAnalyzer analyzer,
            string assemblyName,
            string source,
            params Type[] anchorTypes)
            => Run(analyzer, source, BuildReferences(anchorTypes), [], assemblyName);

        /// <summary>
        /// Runs the analyzer over the given source code and definition files and returns the diagnostics it produces.
        /// </summary>
        /// <param name="analyzer">The analyzer to run.</param>
        /// <param name="source">The C# source code to analyze.</param>
        /// <param name="additionalFiles">The simulated definition files (path and content).</param>
        /// <returns>The diagnostics the analyzer produces.</returns>
        public static ImmutableArray<Diagnostic> Run(
            DiagnosticAnalyzer analyzer,
            string source,
            params (string Path, string Content)[] additionalFiles)
            => Run(analyzer, source, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], additionalFiles, DefaultAssemblyName);

        /// <summary>
        /// Gets the compilation diagnostics of the given source code, to check whether the test input itself has
        /// compile errors.
        /// </summary>
        /// <param name="source">The C# source code to compile.</param>
        /// <param name="anchorTypes">Representatives of the external types the source refers to.</param>
        /// <returns>The compile-time errors and warnings.</returns>
        /// <remarks>
        /// A semantic rule silently stops working on an error type (it cannot find the attribute symbol), which
        /// looks exactly like a rule that did not fire. When the test input has compile errors, this method tells
        /// the two apart.
        /// </remarks>
        public static ImmutableArray<Diagnostic> GetCompilationDiagnostics(string source, params Type[] anchorTypes)
        {
            return CSharpCompilation.Create(
                assemblyName: DefaultAssemblyName,
                syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
                references: BuildReferences(anchorTypes),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .GetDiagnostics();
        }

        /// <summary>
        /// Builds the reference set: the assemblies of the anchor types plus the currently loaded non-dynamic
        /// assemblies.
        /// </summary>
        /// <param name="anchorTypes">The types whose assemblies must be included.</param>
        /// <returns>The de-duplicated assembly references.</returns>
        private static MetadataReference[] BuildReferences(Type[] anchorTypes)
        {
            var locations = new HashSet<string>(StringComparer.Ordinal);

            // Accessing `Assembly` forces the assembly to load, so the anchors must be added first.
            foreach (var type in anchorTypes)
            {
                if (!string.IsNullOrEmpty(type.Assembly.Location))
                    locations.Add(type.Assembly.Location);
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    locations.Add(assembly.Location);
            }

            return locations.Select(location => (MetadataReference)MetadataReference.CreateFromFile(location)).ToArray();
        }

        private static ImmutableArray<Diagnostic> Run(
            DiagnosticAnalyzer analyzer,
            string source,
            IEnumerable<MetadataReference> references,
            (string Path, string Content)[] additionalFiles,
            string assemblyName)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName: assemblyName,
                syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var texts = additionalFiles
                .Select(file => (AdditionalText)new TestAdditionalText(file.Path, file.Content))
                .ToImmutableArray();

            var withAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create(analyzer),
                new AnalyzerOptions(texts));

            return withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
    }
}
