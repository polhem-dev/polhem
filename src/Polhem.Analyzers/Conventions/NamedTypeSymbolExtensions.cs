using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.Conventions
{
    /// <summary>
    /// Extension methods for <see cref="INamedTypeSymbol"/>.
    /// </summary>
    internal static class NamedTypeSymbolExtensions
    {
        /// <summary>
        /// Determines whether <paramref name="type"/> derives from <paramref name="baseType"/>, directly or through
        /// other base types; a type does not derive from itself.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="baseType">The base type to look for.</param>
        /// <returns><c>true</c> when <paramref name="baseType"/> is among the base types of <paramref name="type"/>.</returns>
        public static bool DerivesFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
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
