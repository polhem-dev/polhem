using System.Collections;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Generates TypeScript declarations from the message types, for clients in another language.
    /// </summary>
    /// <remarks>
    /// A hand-copied type table on the TS side would be a second authoritative source for the same API contract: when
    /// the server renames a field, the copy does not know, and the symptom of the drift is a field that silently
    /// disappears. Generated here, the type table becomes a derived artifact.
    /// <para>
    /// It generates the <b>wire shape</b>, not the CLR shape: <c>Guid</c> and <c>DateTime</c> are both strings in
    /// JSON, enums are string literal unions (<c>JsonStringEnumConverter</c>), and <c>object</c> members are the
    /// discriminated envelope. The reader wants to know what the JSON looks like, not how C# declares it.
    /// </para>
    /// </remarks>
    internal static class WireContractGenerator
    {
        /// <summary>The namespace prefix of the message types.</summary>
        private const string MessageNamespace = "Polhem.Api.Core.Messages";

        /// <summary>
        /// The hand-written preamble: the wire shape of these types is decided by custom converters, which reflection cannot see.
        /// </summary>
        private const string Preamble = """
            // Generated from the Polhem message types — do not edit by hand.
            //
            // These describe the JSON shape on the wire, not the CLR declarations: a Guid and a
            // DateTime are both strings here, enums are string literal unions (the server writes
            // them with JsonStringEnumConverter), and an object-typed member is the discriminated
            // envelope this package calls a wire value.
            //
            // An optional member may be absent, and absent means the CLR default: the JSON wires
            // leave out null and default values (0, false, the first member of an enum, an empty
            // Guid, 0001-01-01T00:00:00). A value-typed member is required only where the server
            // always writes it, because its initial value in .NET is not the CLR default.

            /**
             * An object-typed member as it appears on the wire: `[code, value]`, or null when the
             * member is absent.
             *
             * Named for the envelope rather than the value on purpose — a client decodes this into
             * whatever the code says, and that decoded value is a different type with a different
             * name. Calling both `WireValue` would collide in any package that re-exports the two.
             */
            export type WireValueEnvelope = [number, unknown] | null;

            /** A column's shape inside a serialized DataTable. */
            export interface DataColumnShape {
              name: string;
              type: string;
              allowNull: boolean;
              readOnly: boolean;
              maxLength: number;
              caption: string;
              defaultValue: unknown;
            }

            /**
             * A row, carrying its state and the versions that state implies.
             *
             * A cell carries no discriminator: its type comes from the matching `DataColumnShape.type`
             * in the same document. Two of those types do not arrive as JSON numbers —
             * **`Decimal` and `Int64` (and `UInt64`) are JSON strings**, for the same reason the
             * object envelope quotes them: a JSON number is a double to every JavaScript reader,
             * which holds neither a decimal's precision nor an integer past 2^53, and `JSON.parse`
             * has already lost it before your code runs. Read those cells by the column's `type`,
             * not by `typeof`.
             */
            export interface DataRowShape {
              state: 'Unchanged' | 'Added' | 'Modified' | 'Deleted';
              current?: Record<string, unknown>;
              original?: Record<string, unknown>;
            }

            export interface DataTable {
              tableName: string;
              columns: DataColumnShape[];
              primaryKeys: string[];
              rows: DataRowShape[];
            }

            export interface DataRelationShape {
              name: string;
              parentTable: string;
              childTable: string;
              parentColumns: string[];
              childColumns: string[];
            }

            export interface DataSet {
              dataSetName: string;
              tables: DataTable[];
              relations: DataRelationShape[];
            }
            """;

        /// <summary>
        /// Generates the complete <c>.d.ts</c> content.
        /// </summary>
        public static string Generate()
        {
            var roots = typeof(Messages.ApiMessageBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
                .Where(t => t.Namespace?.StartsWith(MessageNamespace, StringComparison.Ordinal) == true)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            var interfaces = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var enums = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var pending = new Queue<Type>(roots);
            var seen = new HashSet<Type>();

            while (pending.Count > 0)
            {
                var type = pending.Dequeue();
                if (!seen.Add(type)) continue;

                if (type.IsEnum)
                {
                    enums[type.Name] = RenderEnum(type);
                    continue;
                }

                interfaces[type.Name] = type.IsAbstract
                    ? RenderUnion(type, pending)
                    : RenderInterface(type, pending);
            }

            var builder = new StringBuilder();
            builder.AppendLine(Preamble);
            builder.AppendLine();
            foreach (var body in enums.Values) builder.AppendLine(body);
            foreach (var body in interfaces.Values) builder.AppendLine(body);
            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        /// <summary>
        /// Generates the map from type name to assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// An encoded payload must name its type in the envelope, and the server resolves it and checks it against an
        /// allow-list. The name contains the namespace and the assembly name, so a hand copy in another repo is bound
        /// to drift: move a message type to another namespace once and the copy points to a type that cannot be
        /// resolved. The symptom is a rejection at run time, not a compile error.
        /// </remarks>
        public static string GenerateTypeNames()
        {
            var types = typeof(Messages.ApiMessageBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
                .Where(t => t.Namespace?.StartsWith(MessageNamespace, StringComparison.Ordinal) == true)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine("// Generated from the Polhem message types — do not edit by hand.");
            builder.AppendLine("//");
            builder.AppendLine("// An encoded payload must name its type in the envelope; the server resolves it from this");
            builder.AppendLine("// string and screens it against an allow-list first.");
            builder.AppendLine();
            builder.AppendLine("export const WireTypeNames = {");

            foreach (var type in types)
            {
                var qualified = $"{type.FullName}, {type.Assembly.GetName().Name}";
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {type.Name}: '{qualified}',");
            }

            builder.AppendLine("} as const;");
            builder.AppendLine();
            builder.AppendLine("export type WireTypeName = keyof typeof WireTypeNames;");
            return builder.ToString();
        }

        private static string RenderEnum(Type type)
        {
            var members = Enum.GetNames(type).Select(n => $"'{n}'");
            return $"export type {type.Name} = {string.Join(" | ", members)};{Environment.NewLine}";
        }

        /// <summary>
        /// Renders an abstract wire type as the union of its concrete subtypes.
        /// </summary>
        /// <remarks>
        /// A polymorphic member such as <c>GetListRequest.Filter</c> is declared as the abstract base, which has no
        /// wire members of its own: rendered as an interface it came out as <c>{}</c>, and the concrete shapes never
        /// appeared in the contract at all.
        /// </remarks>
        private static string RenderUnion(Type type, Queue<Type> pending)
        {
            var subtypes = ConcreteSubtypes(type);
            foreach (var subtype in subtypes) pending.Enqueue(subtype);

            var members = subtypes.Count == 0 ? "never" : string.Join(" | ", subtypes.Select(t => t.Name));
            return $"export type {type.Name} = {members};{Environment.NewLine}";
        }

        private static string RenderInterface(Type type, Queue<Type> pending)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CultureInfo.InvariantCulture, $"export interface {type.Name} {{");

            var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, line) in DiscriminatorLines(type)) lines[name] = line;

            foreach (var property in WireClosure.Members(type))
            {
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                var optional = IsOptional(property);
                lines[name] = $"  {name}{(optional ? "?" : "")}: {MapType(property.PropertyType, pending)};";
            }

            foreach (var line in lines.Values) builder.AppendLine(line);

            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Whether a member may be absent from the JSON wire.
        /// </summary>
        /// <remarks>
        /// The JSON body codec and <c>Plain</c> leave out any member equal to its CLR default, value types included,
        /// so only a member the server always writes is required. That is a value-typed member carrying
        /// <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c>; <c>WireDefaultOmissionTests</c> requires it on
        /// every member whose initialiser is not the CLR default.
        /// </remarks>
        private static bool IsOptional(PropertyInfo property)
        {
            var type = property.PropertyType;
            var isNonNullableValue = type.IsValueType && Nullable.GetUnderlyingType(type) == null;
            return !(isNonNullableValue && WireClosure.IsAlwaysWritten(property));
        }

        /// <summary>
        /// The discriminator of a concrete subtype of an abstract wire type, as a literal member.
        /// </summary>
        /// <remarks>
        /// The discriminator is a get-only enum property the subtype overrides, so it is not a wire member under
        /// <see cref="WireClosure.Members"/>, but System.Text.Json writes it. It follows the same omission rule as
        /// any other member: the subtype whose value is the enum's default travels without it, which is also what
        /// the reader assumes when it is missing.
        /// </remarks>
        private static IEnumerable<(string Name, string Line)> DiscriminatorLines(Type type)
        {
            var baseType = type.BaseType;
            if (baseType == null || !baseType.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
                yield break;

            var instance = Activator.CreateInstance(type)!;
            var discriminators = baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsEnum && p.SetMethod == null)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always });

            foreach (var property in discriminators)
            {
                var value = property.GetValue(instance)!;
                var optional = Equals(value, Activator.CreateInstance(property.PropertyType));
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                yield return (name, $"  {name}{(optional ? "?" : "")}: '{value}';");
            }
        }

        /// <summary>
        /// The concrete subtypes of an abstract wire type, found in the assembly that declares it.
        /// </summary>
        private static List<Type> ConcreteSubtypes(Type type) =>
            type.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && t.IsSubclassOf(type))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Maps a member's CLR type to its shape on the wire. Whether the member may be absent is decided separately,
        /// by <see cref="IsOptional"/>.
        /// </summary>
        private static string MapType(Type type, Queue<Type> pending)
            => MapNonNullable(Nullable.GetUnderlyingType(type) ?? type, pending);

        private static string MapNonNullable(Type type, Queue<Type> pending)
        {
            if (type == typeof(string) || type == typeof(Guid)) return "string";
            if (type == typeof(bool)) return "boolean";
            // A DateTime is an ISO 8601 string on the wire; keeping it as `string` says so.
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "string";
            if (type == typeof(TimeSpan) || type == typeof(DateOnly)) return "string";
            if (type == typeof(byte[])) return "string"; // base64
            if (type == typeof(object)) return "WireValueEnvelope";
            if (type == typeof(DataSet)) return "DataSet";
            if (type == typeof(DataTable)) return "DataTable";

            if (type.IsPrimitive || type == typeof(decimal)) return "number";

            if (type.IsEnum)
            {
                pending.Enqueue(type);
                return type.Name;
            }

            if (type.IsArray)
            {
                var element = type.GetElementType()!;
                return $"{MapNonNullable(element, pending)}[]";
            }

            // A dictionary is a JSON object keyed by strings. It used to fall through to the collection branch below,
            // which took the key type as the element and rendered `Dictionary<string, int>` as `string[]`.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var value = type.GetGenericArguments()[1];
                return $"Record<string, {MapType(value, pending)}>";
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var element = ElementTypeOf(type);
                return element == null ? "unknown[]" : $"{MapNonNullable(element, pending)}[]";
            }

            if (type.IsClass && type.Namespace?.StartsWith("Polhem.", StringComparison.Ordinal) == true)
            {
                pending.Enqueue(type);
                return type.Name;
            }

            return "unknown";
        }

        /// <summary>
        /// Gets a collection's element type: the generic argument first, then the base type's argument for custom collections such as <c>KeyCollectionBase</c>.
        /// </summary>
        private static Type? ElementTypeOf(Type type)
        {
            if (type.IsGenericType) return type.GetGenericArguments().FirstOrDefault();

            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.IsGenericType) return baseType.GetGenericArguments().FirstOrDefault();
            }

            return null;
        }
    }
}
