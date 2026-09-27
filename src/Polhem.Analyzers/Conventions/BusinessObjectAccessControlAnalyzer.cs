using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Polhem.Analyzers.Conventions
{
    /// <summary>
    /// Reports POLHEM3001: a public method on a business object that no <c>[ApiAccessControl]</c> covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A business object's public methods are its API surface: the JSON-RPC executor resolves an action
    /// name straight to a public method. Before invoking it the framework demands an access-control
    /// declaration and throws <c>UnauthorizedAccessException</c> when none is found, so an unmarked
    /// method is not an open one — it is one that cannot be called at all, and only says so at run time
    /// once a client tries.
    /// </para>
    /// <para>
    /// The attribute is looked up exactly as the framework does: on the method itself or any method it
    /// overrides, then on the declaring type or any of its base types (the attribute is declared
    /// <c>Inherited = true</c>, and the runtime reads it with inheritance). A type-level attribute
    /// therefore covers all of its methods and those of its subclasses, and only genuinely uncovered
    /// methods are reported.
    /// </para>
    /// <para>
    /// The methods considered are the ones an action name can resolve to, by the same rule as
    /// <c>JsonRpcExecutor.IsResolvableAction</c>: public, non-static, non-generic, ordinary (not an
    /// accessor, operator or constructor), taking exactly one parameter, and not an override of a
    /// <see cref="object"/> member. Reporting anything else would be noise about methods that cannot be
    /// called; missing any of them would leave a callable method unreported.
    /// </para>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BusinessObjectAccessControlAnalyzer : DiagnosticAnalyzer
    {
        private const string BusinessObjectType = "Polhem.Business.BusinessObject";
        private const string AccessControlAttribute = "Polhem.Definition.Attributes.ApiAccessControlAttribute";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.MissingApiAccessControl,
            title: "Business object API method must declare access control",
            messageFormat: "Public method '{1}' on business object '{0}' has no [ApiAccessControl] on "
                         + "itself, on the method it overrides, or on its declaring type. The framework "
                         + "rejects such calls with UnauthorizedAccessException, so the method cannot be "
                         + "invoked at all — the failure only appears when a client first calls it. "
                         + "Fix: add [ApiAccessControl] to the method, or to the type to cover all of "
                         + "its methods.",
            category: "Polhem.Business",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Public methods on a business object are reachable as API actions and require an "
                       + "access-control declaration before the framework will invoke them.",
            helpLinkUri: null,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(startContext =>
            {
                var businessObject = startContext.Compilation.GetTypeByMetadataName(BusinessObjectType);
                var accessControl = startContext.Compilation.GetTypeByMetadataName(AccessControlAttribute);
                if (businessObject is null || accessControl is null)
                    return;

                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeType(symbolContext, businessObject, accessControl),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeType(
            SymbolAnalysisContext context,
            INamedTypeSymbol businessObject,
            INamedTypeSymbol accessControl)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class || !DerivesFrom(type, businessObject))
                return;

            // A type-level attribute, here or on a base type, covers every method, so there is nothing
            // left to report.
            if (HasAttributeOnTypeOrBase(type, accessControl))
                return;

            foreach (var member in type.GetMembers())
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (member is not IMethodSymbol method || !IsApiCandidate(method))
                    continue;

                if (HasAttribute(method, accessControl) || IsCoveredByOverriddenMethod(method, accessControl))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    method.Locations.FirstOrDefault() ?? Location.None,
                    type.Name,
                    method.Name));
            }
        }

        /// <summary>
        /// Determines whether the method is one the JSON-RPC executor could resolve as an action.
        /// </summary>
        /// <param name="method">The method to test.</param>
        /// <returns><c>true</c> when the method forms part of the API surface.</returns>
        /// <remarks>
        /// Mirrors <c>JsonRpcExecutor.IsResolvableAction</c>, which the executor applies before it looks
        /// for an access declaration. Static methods, accessors and generic methods are excluded because
        /// the executor refuses to resolve an action name to them, not because they happen to be rare.
        /// </remarks>
        private static bool IsApiCandidate(IMethodSymbol method)
            => method.MethodKind == MethodKind.Ordinary
            && !method.IsStatic
            && !method.IsGenericMethod
            && method.Parameters.Length == 1
            && method.DeclaredAccessibility == Accessibility.Public
            && !OverridesObjectMember(method);

        /// <summary>
        /// Determines whether the method overrides, directly or through intermediate overrides, a member
        /// declared by <see cref="object"/>.
        /// </summary>
        private static bool OverridesObjectMember(IMethodSymbol method)
        {
            var root = method;
            while (root.OverriddenMethod is not null)
                root = root.OverriddenMethod;
            return root.ContainingType?.SpecialType == SpecialType.System_Object;
        }

        /// <summary>
        /// Determines whether an overridden method further up the hierarchy carries the attribute.
        /// </summary>
        /// <param name="method">The overriding method.</param>
        /// <param name="accessControl">The resolved attribute symbol.</param>
        /// <returns><c>true</c> when any overridden method declares access control.</returns>
        /// <remarks>
        /// Only the overridden methods themselves are consulted. The types that declare them are base
        /// types of the overriding method's own type, and those are already covered by
        /// <see cref="HasAttributeOnTypeOrBase"/>, exactly as the runtime reads the declaring type's
        /// attribute with inheritance.
        /// </remarks>
        private static bool IsCoveredByOverriddenMethod(IMethodSymbol method, INamedTypeSymbol accessControl)
        {
            for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
            {
                if (HasAttribute(current, accessControl))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the type or any of its base types carries the attribute.
        /// </summary>
        private static bool HasAttributeOnTypeOrBase(INamedTypeSymbol type, INamedTypeSymbol accessControl)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (HasAttribute(current, accessControl))
                    return true;
            }

            return false;
        }

        private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes()
                .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));
        }

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;
            }

            return false;
        }
    }
}
