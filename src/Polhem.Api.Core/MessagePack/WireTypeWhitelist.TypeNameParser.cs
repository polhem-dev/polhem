using System.Text;
using Polhem.Core;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// The parser behind <see cref="IsAssemblyQualifiedNameAllowed"/>: it walks an assembly-qualified type name
    /// (generic arguments, array ranks, assembly specifications), rejects an assembly specification outside the
    /// trusted assemblies, and collects the plain type names it contains, so each can be checked against the
    /// allow-list.
    /// </summary>
    internal static partial class WireTypeWhitelist
    {
        /// <summary>
        /// Parses one type specification, appending the plain type names it contains to
        /// <paramref name="names"/>. Advances <paramref name="position"/> past what it consumed.
        /// </summary>
        private static bool TryParseTypeSpec(string text, ref int position, List<string> names, int depth)
        {
            if (depth > MaxNestingDepth)
                return false;

            SkipWhitespace(text, ref position);

            var start = position;
            while (position < text.Length && !IsTypeSpecTerminator(text[position]))
            {
                position++;
            }

            var name = new StringBuilder(text[start..position].Trim());
            if (name.Length == 0)
                return false;

            // Pointers and by-ref types never appear on this wire; reject rather than strip.
            if (position < text.Length && (text[position] == '*' || text[position] == '&'))
                return false;

            if (!TryParseNameSuffixes(text, ref position, name, names, depth))
                return false;

            names.Add(name.ToString());
            return true;
        }

        /// <summary>
        /// Consumes the bracket groups that follow a type name. An array rank binds to
        /// <paramref name="name"/>; a generic-argument list contributes its own names to
        /// <paramref name="names"/>.
        /// </summary>
        private static bool TryParseNameSuffixes(
            string text, ref int position, StringBuilder name, List<string> names, int depth)
        {
            var sawGenericArguments = false;
            while (position < text.Length && text[position] == '[')
            {
                var close = FindMatchingBracket(text, position);
                if (close < 0)
                    return false;

                var inner = text[(position + 1)..close];
                if (IsArrayRank(inner))
                {
                    // An array rank binds to the name: `System.Byte` + `[]` is the whitelisted
                    // `System.Byte[]`, which is a different entry from `System.Byte`.
                    name.Append('[').Append(inner).Append(']');
                }
                else
                {
                    // Only one generic-argument list may follow a name; a second non-rank bracket
                    // group is malformed.
                    if (sawGenericArguments)
                        return false;
                    if (!TryParseGenericArguments(text, position + 1, close, names, depth + 1))
                        return false;
                    sawGenericArguments = true;
                }

                position = close + 1;
            }

            return true;
        }

        /// <summary>
        /// Parses the comma-separated argument list between a generic type's brackets. Each
        /// argument is either bracketed (assembly-qualified) or bare.
        /// </summary>
        private static bool TryParseGenericArguments(string text, int start, int end, List<string> names, int depth)
        {
            if (depth > MaxNestingDepth)
                return false;

            var position = start;
            while (position < end)
            {
                if (!TryParseGenericArgument(text, ref position, end, names, depth))
                    return false;

                SkipWhitespace(text, ref position);
                if (position < end)
                {
                    if (text[position] != ',')
                        return false;
                    position++;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses one generic argument, which is either bracketed (and therefore
        /// assembly-qualified) or bare.
        /// </summary>
        private static bool TryParseGenericArgument(
            string text, ref int position, int end, List<string> names, int depth)
        {
            SkipWhitespace(text, ref position);
            if (position >= end)
                return false;

            if (text[position] != '[')
                return TryParseTypeSpec(text, ref position, names, depth);

            var close = FindMatchingBracket(text, position);
            if (close < 0 || close > end)
                return false;

            var inner = position + 1;
            if (!TryParseTypeSpec(text, ref inner, names, depth))
                return false;

            // Whatever remains inside the brackets is the argument's assembly name.
            SkipWhitespace(text, ref inner);
            if (inner < close)
            {
                if (text[inner] != ',')
                    return false;
                if (!IsAssemblySpecAllowed(text[(inner + 1)..close]))
                    return false;
            }

            position = close + 1;
            return true;
        }

        /// <summary>
        /// Validates an assembly display name: an allowed simple name, optionally followed by
        /// version, culture and public key token.
        /// </summary>
        /// <param name="spec">The text after the comma that separates it from the type name.</param>
        private static bool IsAssemblySpecAllowed(string spec)
        {
            var parts = spec.Split(',');
            if (!IsAssemblyNameAllowed(parts[0].Trim()))
                return false;

            for (var i = 1; i < parts.Length; i++)
            {
                var pair = parts[i].Split('=');
                if (pair.Length != 2)
                    return false;
                var key = pair[0].Trim();
                var value = pair[1].Trim();
                if (!s_assemblyNameAttributes.Contains(key) || value.Length == 0 || !value.All(IsAssemblyNameChar))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates an assembly simple name against the trusted runtime assemblies and the allowed
        /// namespaces.
        /// </summary>
        private static bool IsAssemblyNameAllowed(string name)
        {
            if (name.Length == 0 || !name.All(IsAssemblyNameChar))
                return false;
            if (s_trustedAssemblies.Contains(name))
                return true;

            return SysInfo.AllowedTypeNamespaces.Any(ns =>
                string.Equals(name, ns, StringComparison.Ordinal)
                || name.StartsWith(ns + ".", StringComparison.Ordinal));
        }

        /// <summary>
        /// The characters an assembly simple name or attribute value may contain here. Excludes
        /// path separators, quotes and anything else that could turn a name into a location.
        /// </summary>
        private static bool IsAssemblyNameChar(char c)
            => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-';

        /// <summary>
        /// Determines whether a bracket group is an array rank (empty, or commas only) rather than
        /// a generic-argument list.
        /// </summary>
        private static bool IsArrayRank(string inner)
            => inner.All(c => c == ',' || char.IsWhiteSpace(c));

        /// <summary>
        /// Returns the index of the bracket closing the one at <paramref name="open"/>, or -1.
        /// </summary>
        private static int FindMatchingBracket(string text, int open)
        {
            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    depth++;
                }
                else if (text[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static bool IsTypeSpecTerminator(char c)
            => c is '[' or ']' or ',' or '*' or '&';

        private static void SkipWhitespace(string text, ref int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }
    }
}
