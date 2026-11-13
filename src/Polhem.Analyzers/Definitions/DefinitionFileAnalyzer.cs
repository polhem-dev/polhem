using Microsoft.CodeAnalysis.Diagnostics;

namespace Polhem.Analyzers.Definitions
{
    /// <summary>
    /// Base of the analyzers that check definition files: each one reads the files once per compilation.
    /// </summary>
    public abstract class DefinitionFileAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Definition files are AdditionalFiles, which are only reachable from a compilation action.
            context.RegisterCompilationAction(Analyze);
        }

        /// <summary>
        /// Checks the definition files of one compilation.
        /// </summary>
        /// <param name="context">The compilation's analysis context.</param>
        protected abstract void Analyze(CompilationAnalysisContext context);
    }
}
