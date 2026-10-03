using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.MessagePack;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Names payload types the way Polhem always has, <c>FullName, AssemblyName</c>, and accepts a name only when it
    /// passes the wire type allow-list.
    /// </summary>
    internal sealed class PolhemPayloadTypeResolver : IPayloadTypeResolver
    {
        public static PolhemPayloadTypeResolver Instance { get; } = new();

        // `Assembly.GetName()` builds a new `AssemblyName` on every call, and this runs on every encoded payload. The keys
        // are the wire types, a set fixed by the compiled code.
        private readonly ConcurrentDictionary<Type, string> _names = new();

        private PolhemPayloadTypeResolver() { }

        public string GetTypeName(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return _names.GetOrAdd(type, static t => t.FullName + ", " + t.Assembly.GetName().Name);
        }

        public bool TryResolveType(string typeName, [NotNullWhen(true)] out Type? type)
        {
            ArgumentNullException.ThrowIfNull(typeName);
            // WARNING: Screen the whole assembly-qualified name, generic arguments included, before loading anything.
            // For a generic type the first comma sits inside `[[...]]`, so screening the part before it would let an
            // argument reach `Type.GetType` unscreened.
            type = WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(typeName) ? Type.GetType(typeName) : null;
            return type != null;
        }

        public bool IsNameOf(string typeName, Type type)
        {
            ArgumentNullException.ThrowIfNull(typeName);
            ArgumentNullException.ThrowIfNull(type);
            return WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(typeName) && ActionPayloadType.IsNamedBy(typeName, type);
        }
    }
}
