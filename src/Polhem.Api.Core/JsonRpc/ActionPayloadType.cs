using System.Collections.Concurrent;
using System.Reflection;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Decides which type an encoded request body is decoded into, from the method it is addressed
    /// to rather than from the name the caller wrote on the envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMPORTANT: the envelope's <see cref="ApiPayload.TypeName"/> is chosen by the caller. Decoding
    /// into whatever it names let a caller have the server construct any public type in the
    /// allow-listed namespaces, through its constructor and setters, before anything checked that
    /// the type suited the method. The method is resolved and its access checked before the body is
    /// read, so the server already knows the one type the body can legitimately be. The caller's
    /// name is kept only as a consistency check: a mismatch is refused, never followed.
    /// </para>
    /// <para>
    /// The target is the method's parameter type, or the framework request type that is its wire
    /// counterpart. A business object method takes a business-layer argument (<c>LoginArgs</c>)
    /// while the client sends the wire message (<see cref="Messages.System.LoginRequest"/>); the two
    /// meet at a contract interface (<see cref="Polhem.Api.Contracts.System.ILoginRequest"/>) that
    /// both implement, and
    /// <see cref="Conversion.ApiInputConverter"/> copies one onto the other after decoding. Only the
    /// framework's own request types are searched, so an application method whose argument has no
    /// framework counterpart decodes into its parameter type, and its callers name that type.
    /// </para>
    /// </remarks>
    internal static class ActionPayloadType
    {
        private static readonly Assembly s_contractsAssembly = typeof(Polhem.Api.Contracts.IExecFuncRequest).Assembly;

        private static readonly Type[] s_requestTypes = typeof(ApiRequest).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ApiRequest).IsAssignableFrom(t))
            .ToArray();

        private static readonly ConcurrentDictionary<Type, Type> s_cache = new();

        /// <summary>
        /// Returns the type an encoded body addressed to <paramref name="method"/> is decoded into.
        /// </summary>
        /// <param name="method">
        /// A resolved action; <see cref="JsonRpcExecutor.IsResolvableAction"/> guarantees it takes
        /// exactly one parameter.
        /// </param>
        /// <returns>The request counterpart of the parameter type when one exists; otherwise the parameter type.</returns>
        public static Type Resolve(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return s_cache.GetOrAdd(method.GetParameters()[0].ParameterType, ResolveCore);
        }

        private static Type ResolveCore(Type parameterType)
        {
            if (!parameterType.IsAbstract && typeof(ApiRequest).IsAssignableFrom(parameterType))
                return parameterType;

            var contracts = parameterType.GetInterfaces()
                .Append(parameterType)
                .Where(t => t.IsInterface && t.Assembly == s_contractsAssembly)
                .ToArray();
            if (contracts.Length == 0)
                return parameterType;

            var counterparts = s_requestTypes
                .Where(request => contracts.Any(contract => contract.IsAssignableFrom(request)))
                .ToArray();

            // More than one candidate means the argument type speaks for several messages, and
            // picking one would be a guess. The parameter type is the honest answer then: a caller
            // that sends it still gets through, one that sends a guess is refused.
            return counterparts.Length == 1 ? counterparts[0] : parameterType;
        }

        /// <summary>
        /// Says whether a caller-written type name names <paramref name="type"/>.
        /// </summary>
        /// <param name="typeName">
        /// The name off the envelope: <c>Namespace.Type, Assembly</c>, optionally followed by version,
        /// culture and public key token, which are not compared.
        /// </param>
        /// <param name="type">The type the server decided to decode into.</param>
        /// <returns><c>true</c> when the full type name matches and the assembly, if named, is the type's own.</returns>
        /// <remarks>
        /// The type name is compared ordinally, as the runtime compares type names; the assembly
        /// name ignoring case, as the runtime compares assembly names. A name without an assembly
        /// part is accepted when the type name matches: it is still unambiguous here, because the
        /// server — not the name — picks the type.
        /// </remarks>
        public static bool IsNamedBy(string typeName, Type type)
        {
            ArgumentNullException.ThrowIfNull(typeName);
            ArgumentNullException.ThrowIfNull(type);

            var separator = FindTopLevelComma(typeName);
            var typePart = (separator < 0 ? typeName : typeName[..separator]).Trim();
            if (!string.Equals(typePart, type.FullName, StringComparison.Ordinal))
                return false;
            if (separator < 0)
                return true;

            var assemblyPart = typeName[(separator + 1)..];
            var nameEnd = assemblyPart.IndexOf(',');
            var assemblyName = (nameEnd < 0 ? assemblyPart : assemblyPart[..nameEnd]).Trim();
            return string.Equals(assemblyName, type.Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the index of the first comma outside any generic-argument brackets, or -1.
        /// </summary>
        private static int FindTopLevelComma(string text)
        {
            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        return i;
                }
            }
            return -1;
        }
    }
}
