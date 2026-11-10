using System.Reflection;
using Polhem.Expressions;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The members an expression can reach on a type, and the types worth offering by name, worded for the expression
/// editor's list and its completion.
/// </summary>
public static class ExpressionMembers
{
    // Members of `object` every type has, and nobody writes in a formula.
    private static readonly HashSet<string> s_objectNoise =
        new(StringComparer.Ordinal) { "GetType", "Equals", "GetHashCode", "ReferenceEquals", "MemberwiseClone", "Finalize" };

    // `DateTime.Now`, `Today` and `UtcNow` read the server's clock and zone. The helper functions `Now()`, `Today()` and
    // `UtcNow()` follow the user's zone and the data set's basis (ADR-032 D12), so the editor offers those instead.
    private static readonly HashSet<(Type, string)> s_steeredAway =
    [
        (typeof(DateTime), nameof(DateTime.Now)),
        (typeof(DateTime), nameof(DateTime.Today)),
        (typeof(DateTime), nameof(DateTime.UtcNow)),
    ];

    /// <summary>
    /// The type names of <paramref name="referencedTypes"/> worth offering: the ones with static members a formula uses,
    /// such as <c>Math</c> and <c>Convert</c>, and <c>string</c> rather than <c>String</c>.
    /// </summary>
    /// <remarks>
    /// The numeric and other primitive type names are left out: they are valid, but a formula almost never names one.
    /// Only names <see cref="DynamicExpressoEvaluator.ReferencedTypes"/> holds are returned, so nothing is offered that
    /// the parser rejects.
    /// </remarks>
    public static IReadOnlyList<KeyValuePair<string, Type>> FeaturedTypes(IReadOnlyDictionary<string, Type> referencedTypes)
    {
        ArgumentNullException.ThrowIfNull(referencedTypes);
        return referencedTypes
            .Where(pair => IsFeatured(pair.Key, pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsFeatured(string name, Type type)
    {
        if (type == typeof(string)) return name == "string";
        return !type.IsPrimitive && type != typeof(object) && type != typeof(decimal) && type != typeof(Enumerable);
    }

    /// <summary>
    /// The members of <paramref name="type"/> an expression can use, one suggestion per name, in name order:
    /// its static members when <paramref name="isStatic"/>, otherwise those of a value of the type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="isStatic">Whether the members are reached through the type name.</param>
    /// <param name="prefix">Text to put before each inserted name, such as <c>Math.</c>; empty for none.</param>
    public static IReadOnlyList<ExpressionSuggestion> Of(Type type, bool isStatic, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(type);
        var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        var suggestions = new List<ExpressionSuggestion>();

        foreach (var group in type.GetMethods(flags).Where(m => IsUsable(type, m)).GroupBy(m => m.Name, StringComparer.Ordinal))
        {
            var overloads = group.OrderBy(Preference).ToList();
            var parenthesis = overloads.All(m => m.GetParameters().Length == 0) ? "()" : "(";
            var more = overloads.Count > 1 ? $"  +{overloads.Count - 1}" : string.Empty;
            suggestions.Add(new ExpressionSuggestion(ExpressionSuggestionKind.Member, group.Key,
                prefix + group.Key + parenthesis, Signature(overloads[0]) + more));
        }
        foreach (var property in type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0
                     && p.GetMethod is { IsPublic: true } && !IsHidden(type, p)))
        {
            suggestions.Add(new ExpressionSuggestion(ExpressionSuggestionKind.Member, property.Name,
                prefix + property.Name, TypeName(property.PropertyType)));
        }
        foreach (var field in type.GetFields(flags).Where(f => !IsHidden(type, f)))
        {
            suggestions.Add(new ExpressionSuggestion(ExpressionSuggestionKind.Member, field.Name,
                prefix + field.Name, TypeName(field.FieldType)));
        }
        return suggestions.OrderBy(s => s.Label, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The type a member named <paramref name="name"/> of <paramref name="type"/> produces: a property's or a field's
    /// type, or the return type of the method's first usable overload; <c>null</c> when there is no such member.
    /// </summary>
    public static Type? ResultType(Type type, bool isStatic, string name, bool isCall)
    {
        ArgumentNullException.ThrowIfNull(type);
        var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        if (isCall)
        {
            return type.GetMethods(flags)
                .Where(m => string.Equals(m.Name, name, StringComparison.Ordinal) && IsUsable(type, m))
                .OrderBy(Preference)
                .Select(m => m.ReturnType)
                .FirstOrDefault();
        }
        return type.GetProperty(name, flags)?.PropertyType ?? type.GetField(name, flags)?.FieldType;
    }

    // The overload a list shows and a chain follows: the fewest parameters, then the numeric type money and quantities
    // are stored as (`decimal`), then `double`, so `Math.Round` reads as the decimal one rather than the first declared.
    private static (int, int) Preference(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var rank = parameters.Length == 0 ? 0 : parameters.Max(p => p.ParameterType == typeof(decimal) ? 0
            : p.ParameterType == typeof(double) ? 1
            : p.ParameterType == typeof(string) || p.ParameterType == typeof(int) ? 2
            : 3);
        return (parameters.Length, rank);
    }

    /// <summary>A function's parameters and result, as the list shows them: <c>(string value) → bool</c>.</summary>
    public static string Signature(ExpressionFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return Signature(function.Parameters, function.ReturnType);
    }

    private static string Signature(MethodInfo method) => Signature(method.GetParameters(), method.ReturnType);

    private static string Signature(IEnumerable<ParameterInfo> parameters, Type returnType) =>
        $"({string.Join(", ", parameters.Select(p => $"{TypeName(p.ParameterType)} {p.Name}"))}) → {TypeName(returnType)}";

    /// <summary>The name an expression writes a type with, so the list reads like the expressions themselves.</summary>
    public static string TypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type == typeof(void)) return "void";
        if (Nullable.GetUnderlyingType(type) is { } inner) return TypeName(inner) + "?";
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
        return Type.GetTypeCode(type) switch
        {
            TypeCode.String => "string",
            TypeCode.Boolean => "bool",
            TypeCode.Int32 => "int",
            TypeCode.Int64 => "long",
            TypeCode.Int16 => "short",
            TypeCode.Decimal => "decimal",
            TypeCode.Double => "double",
            TypeCode.Single => "float",
            TypeCode.Char => "char",
            TypeCode.Byte => "byte",
            TypeCode.Object when type == typeof(object) => "object",
            _ => type.IsGenericType ? type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)] : type.Name,
        };
    }

    // A method the parser can call with what an expression can write: not generic, not an accessor or operator, and
    // with no by-ref, pointer or span parameter, which an expression has no way to supply.
    private static bool IsUsable(Type type, MethodInfo method) =>
        !method.IsSpecialName
        && !method.IsGenericMethodDefinition
        && !IsHidden(type, method)
        && !method.ReturnType.IsByRefLike
        && method.GetParameters().All(p => !p.ParameterType.IsByRef && !p.ParameterType.IsPointer
                                           && !p.ParameterType.IsByRefLike && !p.IsOut);

    private static bool IsHidden(Type type, MemberInfo member) =>
        s_objectNoise.Contains(member.Name)
        || s_steeredAway.Contains((type, member.Name))
        || member.IsDefined(typeof(ObsoleteAttribute), inherit: true)
        || member.GetCustomAttribute<System.ComponentModel.EditorBrowsableAttribute>()?.State
            == System.ComponentModel.EditorBrowsableState.Never;
}
