using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Polhem.Analyzers.Conventions
{
    /// <summary>
    /// Reports POLHEM3004: a repository reaches the database through its context's database access
    /// factory instead of <c>RepositoryBase.CreateDbAccess</c>.
    /// </summary>
    /// <remarks>
    /// Several companies can share one company database. A company-bound repository confines its
    /// hand-written statements through the database access <c>CreateDbAccess</c> hands out, which is
    /// bound to the session's company and refuses a statement that does not name it. The factory on the
    /// context hands out an unbound one, so a statement run through it reads and writes every company's
    /// rows without any sign that the company was forgotten. This rule makes that bypass visible at build
    /// time.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RepositoryDbAccessFactoryAnalyzer : DiagnosticAnalyzer
    {
        private const string RepositoryBase = "Polhem.Repository.RepositoryBase";
        private const string RepositoryContext = "Polhem.Repository.IRepositoryContext";
        private const string FactoryProperty = "DbAccessFactory";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.RepositoryBypassesCompanyBinding,
            title: "Repository must create its database access through CreateDbAccess",
            messageFormat: "Repository '{0}' reaches the database through the context's DbAccessFactory, which hands out "
                         + "a database access not bound to the company. On a company database shared by several "
                         + "companies its statements read and write every company's rows. Fix: call CreateDbAccess(), "
                         + "or CreateDbAccess(databaseId, companyId) when there is no session.",
            category: "Polhem.Repository",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A repository's database access must come from RepositoryBase.CreateDbAccess so that a "
                       + "company-bound repository confines its statements to its company.",
            helpLinkUri: null);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(startContext =>
            {
                var repositoryBase = startContext.Compilation.GetTypeByMetadataName(RepositoryBase);
                var repositoryContext = startContext.Compilation.GetTypeByMetadataName(RepositoryContext);
                if (repositoryBase is null || repositoryContext is null)
                    return;

                startContext.RegisterOperationAction(
                    operationContext => AnalyzePropertyReference(operationContext, repositoryBase, repositoryContext),
                    OperationKind.PropertyReference);
            });
        }

        private static void AnalyzePropertyReference(
            OperationAnalysisContext context, INamedTypeSymbol repositoryBase, INamedTypeSymbol repositoryContext)
        {
            var reference = (IPropertyReferenceOperation)context.Operation;
            if (reference.Property.Name != FactoryProperty) return;
            if (!IsOrImplements(reference.Property.ContainingType, repositoryContext)) return;

            var containingType = context.ContainingSymbol.ContainingType;
            if (containingType is null || !DerivesFrom(containingType, repositoryBase)) return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, reference.Syntax.GetLocation(), containingType.Name));
        }

        private static bool IsOrImplements(INamedTypeSymbol type, INamedTypeSymbol contract)
            => SymbolEqualityComparer.Default.Equals(type, contract)
               || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract));

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            }
            return false;
        }
    }
}
