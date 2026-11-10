using System.Reflection;
using System.Reflection.Emit;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Reads which members a method's IL refers to, for gates that check what compiled code calls.
    /// </summary>
    /// <remarks>
    /// A scan of the IL sees a call wherever it is, including inside compiler-generated closures and async state
    /// machines when the caller walks the nested types too (<see cref="MethodsOf"/> does). It sees no call that only
    /// a comment or a <c>cref</c> mentions.
    /// </remarks>
    public static class IlMemberReferences
    {
        private static readonly Dictionary<short, OpCode> s_opCodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => op.Value);

        /// <summary>
        /// Returns the methods and constructors <paramref name="type"/> declares, and those of its nested types,
        /// compiler-generated ones included.
        /// </summary>
        /// <param name="type">The type to walk.</param>
        public static IEnumerable<MethodBase> MethodsOf(Type type)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all))
                yield return method;
            foreach (var constructor in type.GetConstructors(all))
                yield return constructor;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var method in MethodsOf(nested))
                    yield return method;
            }
        }

        /// <summary>
        /// Returns the outermost type that declares <paramref name="method"/>, so a call made inside a closure or a
        /// state machine is attributed to the source type it was written in.
        /// </summary>
        /// <param name="method">The method.</param>
        public static Type? OuterType(MethodBase method)
        {
            var type = method.DeclaringType;
            while (type?.DeclaringType != null) { type = type.DeclaringType; }
            return type;
        }

        /// <summary>Returns every member the IL of <paramref name="method"/> refers to through a metadata token.</summary>
        /// <param name="method">The method to read.</param>
        public static IEnumerable<MemberInfo> Of(MethodBase method)
        {
            ArgumentNullException.ThrowIfNull(method);
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il is null) yield break;

            var position = 0;
            while (position < il.Length)
            {
                short value = il[position++];
                if (value == 0xFE) value = (short)(0xFE00 | il[position++]);
                var opCode = s_opCodes[value];

                switch (opCode.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineTok:
                        var member = Resolve(method, BitConverter.ToInt32(il, position));
                        if (member is not null) yield return member;
                        position += 4;
                        break;
                    case OperandType.InlineSwitch:
                        position += 4 + (4 * BitConverter.ToInt32(il, position));
                        break;
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        position += 1;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        position += 8;
                        break;
                    default:
                        position += 4;
                        break;
                }
            }
        }

        private static MemberInfo? Resolve(MethodBase method, int token)
        {
            var typeArguments = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericArguments()
                : null;
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            try
            {
                return method.Module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (ArgumentException)
            {
                // A token for a type or a signature, not a member.
                return null;
            }
        }
    }
}
