using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Polhem.Analyzers.Conventions
{
    /// <summary>
    /// Reports POLHEM3004: a repository creates a database access some other way than
    /// <c>RepositoryBase.CreateDbAccess</c>: through the context's <c>DbAccessFactory</c>, through any
    /// <c>IDbAccessFactory</c> (injected or resolved from services), or with <c>new DbAccess(...)</c>.
    /// </summary>
    /// <remarks>
    /// Several companies can share one company database. A company-bound repository confines its
    /// hand-written statements through the database access <c>CreateDbAccess</c> hands out, which is
    /// bound to the session's company and refuses a statement that does not name it. Every other way of
    /// creating one hands out an unbound access, so a statement run through it reads and writes every
    /// company's rows without any sign that the company was forgotten. This rule makes that bypass
    /// visible at build time, in the repository and in any type nested inside it. A helper outside the
    /// repository that is handed the context is not seen.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RepositoryDbAccessFactoryAnalyzer : DiagnosticAnalyzer
    {
        private const string RepositoryBase = "Polhem.Repository.RepositoryBase";
        private const string RepositoryContext = "Polhem.Repository.IRepositoryContext";
        private const string DbAccessFactoryType = "Polhem.Db.IDbAccessFactory";
        private const string DbAccessType = "Polhem.Db.DbAccess";
        private const string FactoryProperty = "DbAccessFactory";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.RepositoryBypassesCompanyBinding,
            title: "Repository must create its database access through CreateDbAccess",
            messageFormat: "Repository '{0}' creates a database access outside CreateDbAccess, so it is not bound to "
                         + "the company. On a company database shared by several companies its statements read and "
                         + "write every company's rows. Fix: call CreateDbAccess(), or CreateDbAccess(databaseId, "
                         + "companyId) when there is no session.",
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
                var factoryType = startContext.Compilation.GetTypeByMetadataName(DbAccessFactoryType);
                var dbAccessType = startContext.Compilation.GetTypeByMetadataName(DbAccessType);
                if (repositoryBase is null || repositoryContext is null || factoryType is null || dbAccessType is null)
                    return;

                startContext.RegisterOperationAction(operationContext =>
                {
                    var reference = (IPropertyReferenceOperation)operationContext.Operation;
                    if (reference.Property.Name == FactoryProperty
                        && IsOrImplements(reference.Property.ContainingType, repositoryContext))
                        ReportInRepository(operationContext, repositoryBase);
                }, OperationKind.PropertyReference);

                startContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    // A call on the context's factory property is already reported at the property.
                    bool onContextFactory = invocation.Instance is IPropertyReferenceOperation { Property.Name: FactoryProperty };
                    if (!onContextFactory
                        && IsOrImplements(invocation.TargetMethod.ContainingType, factoryType)
                        && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ReturnType, dbAccessType))
                        ReportInRepository(operationContext, repositoryBase);
                }, OperationKind.Invocation);

                startContext.RegisterOperationAction(operationContext =>
                {
                    var creation = (IObjectCreationOperation)operationContext.Operation;
                    if (SymbolEqualityComparer.Default.Equals(creation.Type, dbAccessType))
                        ReportInRepository(operationContext, repositoryBase);
                }, OperationKind.ObjectCreation);
            });
        }

        /// <summary>
        /// Reports the operation when it sits in a repository, or in a type nested inside one.
        /// </summary>
        private static void ReportInRepository(OperationAnalysisContext context, INamedTypeSymbol repositoryBase)
        {
            for (var type = context.ContainingSymbol.ContainingType; type is not null; type = type.ContainingType)
            {
                if (!DerivesFrom(type, repositoryBase)) continue;
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), type.Name));
                return;
            }
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
