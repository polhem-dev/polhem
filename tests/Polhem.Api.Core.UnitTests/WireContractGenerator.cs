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

                interfaces[type.Name] = RenderInterface(type, pending);
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

        private static string RenderInterface(Type type, Queue<Type> pending)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CultureInfo.InvariantCulture, $"export interface {type.Name} {{");

            foreach (var property in WireProperties(type))
            {
                var (tsType, optional) = MapType(property.PropertyType, pending);
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {name}{(optional ? "?" : "")}: {tsType};");
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// The properties that go on the wire: public, readable and writable, and not excluded by <c>[JsonIgnore]</c>.
        /// </summary>
        private static IEnumerable<PropertyInfo> WireProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(p => p.Name, StringComparer.Ordinal);
        }

        /// <summary>
        /// Maps a CLR type to its shape on the wire.
        /// </summary>
        /// <returns>The TypeScript type, and whether the member is optional.</returns>
        private static (string TsType, bool Optional) MapType(Type type, Queue<Type> pending)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            var optional = underlying != null || !type.IsValueType;
            var actual = underlying ?? type;

            return (MapNonNullable(actual, pending), optional);
        }

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
