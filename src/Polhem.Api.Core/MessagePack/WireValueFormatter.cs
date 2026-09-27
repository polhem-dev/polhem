using Polhem.Base;
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using Polhem.Api.Core.Wire;
using Polhem.Definition.Collections;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// Reads one wire value off the reader.
    /// </summary>
    internal delegate object? WireValueReader(ref MessagePackReader reader, MessagePackSerializerOptions options);

    /// <summary>
    /// Writes one wire value to the writer.
    /// </summary>
    internal delegate void WireValueWriter(object value, ref MessagePackWriter writer, MessagePackSerializerOptions options);

    /// <summary>
    /// Serializes an <see cref="object"/>-typed wire member (a filter value, a parameter value, a
    /// table cell) as a two-element envelope: a discriminator followed by the value.
    /// </summary>
    /// <remarks>
    /// WARNING: This replaces <c>TypelessFormatter</c> on the framework's own value types for a
    /// reason. <c>TypelessFormatter</c> resolves the value's formatter through
    /// <c>MessagePackSerializer.NonGeneric</c>, which needs <c>Reflection.Emit</c> to pass the
    /// `ref struct` writer — so on iOS every non-primitive value (<c>Guid</c>, <c>DateTime</c>,
    /// <c>Decimal</c>, <c>DateOnly</c>) failed while <c>String</c> and <c>Int32</c> came through
    /// on its primitive fast path. Here each known type has a closed generic serializer built at
    /// class-initialisation time, so the whole path stays generic.
    /// <para>
    /// The discriminator is an <c>int</c> for the known set and a <c>string</c> type name for
    /// anything else. Behind the string branch sit three kinds of payload. Enums are written as
    /// their underlying integer and read back with <see cref="Enum.ToObject(Type, long)"/>, and the
    /// types in the named table (<see cref="ParameterCollection"/>) have closed generic delegates
    /// like the known set; neither needs dynamic code. Everything else is the escape hatch for the
    /// application-configured namespaces (<see cref="Polhem.Base.SysInfo.AllowedTypeNamespaces"/>):
    /// it goes through the non-generic overload, so it only works where dynamic code does, and
    /// elsewhere fails with a <see cref="NotSupportedException"/> that names the type. That is a
    /// deliberate trade — it keeps the existing extensibility on the server without holding the
    /// framework's own value types hostage to it.
    /// </para>
    /// <para>
    /// The enum and named-table payloads are the bytes the non-generic overload writes for those
    /// types, so peers that still take the escape hatch for them read them unchanged.
    /// <c>WireValueFormatterNamedTypeTests</c> pins both the golden bytes and the equivalence.
    /// </para>
    /// <para>
    /// NOTE: This envelope replaced the ext-type-100 framing <c>TypelessFormatter</c> emits, which
    /// carried a full assembly-qualified type name for every value. The wire is therefore not
    /// compatible with releases before this change; it is also smaller and no longer names CLR
    /// assemblies on the wire for the known set.
    /// </para>
    /// </remarks>
    internal sealed class WireValueFormatter : IMessagePackFormatter<object?>
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly WireValueFormatter Instance = new WireValueFormatter();

        private static readonly Dictionary<Type, int> s_codes = [];
        private static readonly WireValueWriter?[] s_writers = new WireValueWriter?[WireValueCode.Count];
        private static readonly WireValueReader?[] s_readers = new WireValueReader?[WireValueCode.Count];
        private static readonly Dictionary<Type, (WireValueWriter Write, WireValueReader Read)> s_namedTypes = [];

        private WireValueFormatter() { }

        static WireValueFormatter()
        {
            Add(WireValueCode.Boolean, static v => (bool)v);
            Add(WireValueCode.Byte, static v => (byte)v);
            Add(WireValueCode.SByte, static v => (sbyte)v);
            Add(WireValueCode.Int16, static v => (short)v);
            Add(WireValueCode.UInt16, static v => (ushort)v);
            Add(WireValueCode.Int32, static v => (int)v);
            Add(WireValueCode.UInt32, static v => (uint)v);
            Add(WireValueCode.Int64, static v => (long)v);
            Add(WireValueCode.UInt64, static v => (ulong)v);
            Add(WireValueCode.Single, static v => (float)v);
            Add(WireValueCode.Double, static v => (double)v);
            Add(WireValueCode.Decimal, static v => (decimal)v);
            Add(WireValueCode.String, static v => (string)v);
            Add(WireValueCode.DateTime, static v => (DateTime)v);
            Add(WireValueCode.DateTimeOffset, static v => (DateTimeOffset)v);
            Add(WireValueCode.TimeSpan, static v => (TimeSpan)v);
            Add(WireValueCode.DateOnly, static v => (DateOnly)v);
            Add(WireValueCode.Guid, static v => (Guid)v);
            Add(WireValueCode.ByteArray, static v => (byte[])v);
            Add(WireValueCode.DataTable, static v => (DataTable)v);

            // DBNull and object[] are not plain values: the first carries no payload, the second
            // recurses through this very formatter rather than through an array formatter, which
            // the resolver would otherwise have to build with MakeGenericType.
            AddCustom(
                WireValueCode.DBNull,
                typeof(DBNull),
                static (object _, ref MessagePackWriter writer, MessagePackSerializerOptions _) => writer.WriteNil(),
                static (ref MessagePackReader reader, MessagePackSerializerOptions _) =>
                {
                    reader.Skip();
                    return System.DBNull.Value;
                });

            AddCustom(
                WireValueCode.ObjectArray,
                typeof(object[]),
                static (object value, ref MessagePackWriter writer, MessagePackSerializerOptions options) =>
                {
                    var array = (object?[])value;
                    writer.WriteArrayHeader(array.Length);
                    foreach (var element in array)
                        Instance.Serialize(ref writer, element, options);
                },
                static (ref MessagePackReader reader, MessagePackSerializerOptions options) =>
                {
                    var count = reader.ReadArrayHeader();
                    var array = new object?[count];
                    for (var i = 0; i < count; i++)
                        array[i] = Instance.Deserialize(ref reader, options);
                    return array;
                });

            // Types that travel under their type name, with a formatter the resolver already registers.
            AddNamed<ParameterCollection>();
        }

        /// <summary>
        /// Registers a value type whose payload is whatever its own formatter writes.
        /// <typeparamref name="TValue"/> is fixed at compile time, which is the point: the
        /// serializer call below is a closed generic and needs no dynamic code.
        /// </summary>
        private static void Add<TValue>(int code, Func<object, TValue> cast)
        {
            AddCustom(
                code,
                typeof(TValue),
                (object value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
                    => MessagePackSerializer.Serialize(ref writer, cast(value), options),
                (ref MessagePackReader reader, MessagePackSerializerOptions options)
                    => MessagePackSerializer.Deserialize<TValue>(ref reader, options));
        }

        /// <summary>
        /// Registers a type for the named branch: it keeps the type-name discriminator, but its payload
        /// goes through a closed generic serializer instead of the non-generic overload.
        /// </summary>
        private static void AddNamed<TValue>()
        {
            s_namedTypes.Add(
                typeof(TValue),
                ((object value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
                    => MessagePackSerializer.Serialize(ref writer, (TValue)value, options),
                 (ref MessagePackReader reader, MessagePackSerializerOptions options)
                    => MessagePackSerializer.Deserialize<TValue>(ref reader, options)));
        }

        /// <summary>
        /// Registers a value type with hand-written framing.
        /// </summary>
        private static void AddCustom(int code, Type type, WireValueWriter writer, WireValueReader reader)
        {
            s_codes.Add(type, code);
            s_writers[code] = writer;
            s_readers[code] = reader;
        }

        /// <summary>
        /// Serializes the value.
        /// </summary>
        public void Serialize(ref MessagePackWriter writer, object? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(2);

            var type = value.GetType();
            if (s_codes.TryGetValue(type, out var code))
            {
                writer.Write(code);
                s_writers[code]!(value, ref writer, options);
                return;
            }

            // NOTE: Both ends of this escape hatch must screen the same shape, and this check is the
            // reader's own pair of checks run in advance: see `WireTypeWhitelist.IsNamedValueTypeAllowed`.
            var fullName = type.FullName
                ?? throw new InvalidOperationException("Cannot serialize a type with no FullName.");
            if (!WireTypeWhitelist.IsNamedValueTypeAllowed(type))
            {
                throw new InvalidOperationException(
                    $"MessagePack serialization blocked: type '{fullName}' is not in the allowed type whitelist.");
            }

            writer.Write(type.AssemblyQualifiedName);
            WriteNamedPayload(type, value, ref writer, options);
        }

        /// <summary>
        /// Writes the payload that follows a type-name discriminator.
        /// </summary>
        private static void WriteNamedPayload(Type type, object value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
        {
            if (s_namedTypes.TryGetValue(type, out var named))
            {
                named.Write(value, ref writer, options);
            }
            else if (type.IsEnum)
            {
                // MessagePack writes the smallest encoding for a value whatever the declared width, so writing
                // through a 64-bit overload produces the same bytes as an enum formatter writing the underlying type.
                if (IsUnsigned(Enum.GetUnderlyingType(type)))
                    writer.Write(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                else
                    writer.Write(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }
            else
            {
                EnsureDynamicCode(type, RuntimeFeature.IsDynamicCodeSupported);
                MessagePackSerializer.Serialize(type, ref writer, value, options);
            }
        }

        /// <summary>
        /// Reads the payload that follows a type-name discriminator.
        /// </summary>
        private static object? ReadNamedPayload(Type type, ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (s_namedTypes.TryGetValue(type, out var named))
                return named.Read(ref reader, options);

            if (type.IsEnum)
            {
                return IsUnsigned(Enum.GetUnderlyingType(type))
                    ? Enum.ToObject(type, reader.ReadUInt64())
                    : Enum.ToObject(type, reader.ReadInt64());
            }

            EnsureDynamicCode(type, RuntimeFeature.IsDynamicCodeSupported);
            return MessagePackSerializer.Deserialize(type, ref reader, options);
        }

        /// <summary>
        /// Throws when the non-generic overload cannot run, naming the type so the failure points at the value
        /// rather than at MessagePack's internals.
        /// </summary>
        /// <param name="type">The value type about to take the non-generic path.</param>
        /// <param name="isDynamicCodeSupported">Whether the runtime can generate code, from <see cref="RuntimeFeature.IsDynamicCodeSupported"/>.</param>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="isDynamicCodeSupported"/> is <c>false</c>.</exception>
        internal static void EnsureDynamicCode(Type type, bool isDynamicCodeSupported)
        {
            if (isDynamicCodeSupported)
                return;

            throw new NotSupportedException(
                $"Wire value type '{type.FullName}' has no formatter that works without dynamic code, and this runtime "
                + "cannot generate one (iOS and Mac Catalyst run without dynamic code). Send one of the framework's "
                + "closed value types, an enum or a ParameterCollection instead, or declare the JSON body codec for "
                + "this request.");
        }

        private static bool IsUnsigned(Type underlyingType)
            => underlyingType == typeof(byte) || underlyingType == typeof(ushort)
                || underlyingType == typeof(uint) || underlyingType == typeof(ulong);

        /// <summary>
        /// Deserializes the value.
        /// </summary>
        public object? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            options.Security.DepthStep(ref reader);
            try
            {
                var count = reader.ReadArrayHeader();
                if (count != 2)
                    throw new MessagePackSerializationException($"Unexpected wire value envelope length {count}.");

                if (reader.NextMessagePackType == MessagePackType.Integer)
                {
                    var code = reader.ReadInt32();
                    var read = code >= 0 && code < s_readers.Length ? s_readers[code] : null;
                    if (read == null)
                        throw new MessagePackSerializationException($"Unknown wire value code {code}.");
                    return read(ref reader, options);
                }

                var typeName = reader.ReadString()
                    ?? throw new MessagePackSerializationException("Wire value envelope has no type name.");

                // WARNING: The name is screened before `Type.GetType` resolves it, so a disallowed
                // type is never even loaded. Do not reorder these two — the whole point of the
                // whitelist is that it runs ahead of anything the payload can influence.
                // The screen covers generic arguments as well; screening only the text before the
                // first comma leaves them unchecked, because a generic argument's own comma comes
                // first (see `WireTypeWhitelist.IsAssemblyQualifiedNameAllowed`).
                if (!WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(typeName))
                {
                    throw new InvalidOperationException(
                        BeeNameHint.AppendTo($"MessagePack deserialization blocked: type '{typeName}' is not in the allowed type whitelist.", typeName));
                }

                var type = Type.GetType(typeName)
                    ?? throw new InvalidOperationException($"MessagePack deserialization blocked: unknown type '{typeName}'.");
                options.ThrowIfDeserializingTypeIsDisallowed(type);
                return ReadNamedPayload(type, ref reader, options);
            }
            finally
            {
                reader.Depth--;
            }
        }

    }
}
