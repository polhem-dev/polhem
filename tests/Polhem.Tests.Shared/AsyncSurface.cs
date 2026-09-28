using System.Reflection;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Finds the public and protected asynchronous members an assembly declares, for the tests that
    /// require each of them to take a trailing <see cref="CancellationToken"/>.
    /// </summary>
    /// <remarks>
    /// An override of a member declared in another assembly is left out: its signature belongs to that
    /// assembly (<c>ComponentBase.OnInitializedAsync</c> is the case in the Blazor package), and the
    /// assembly under test could not add a token to it.
    /// </remarks>
    public static class AsyncSurface
    {
        /// <summary>
        /// Returns the public and protected methods of the exported types of <paramref name="assembly"/>
        /// that return <see cref="Task"/>, <see cref="ValueTask"/> or their generic forms.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public static IEnumerable<MethodInfo> Methods(Assembly assembly)
            => assembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(method => (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)
                    && !method.IsSpecialName
                    && IsAwaitable(method.ReturnType)
                    && method.GetBaseDefinition().DeclaringType?.Assembly == assembly);

        /// <summary>
        /// Returns a stable, readable name for <paramref name="method"/>, used as a theory data row.
        /// </summary>
        /// <param name="method">The method to describe.</param>
        public static string Describe(MethodInfo method)
            => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";

        /// <summary>
        /// Returns whether <paramref name="method"/> ends with a <see cref="CancellationToken"/> parameter.
        /// </summary>
        /// <param name="method">The method to check.</param>
        public static bool EndsWithCancellationToken(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken);
        }

        private static bool IsAwaitable(Type type)
            => type == typeof(Task) || type == typeof(ValueTask)
                || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>)
                    || type.GetGenericTypeDefinition() == typeof(ValueTask<>)));
    }
}
