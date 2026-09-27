using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using Polhem.Base.Data;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// Writes and reads the cells of one <see cref="System.Data.DataColumn"/>, typed by the column rather than by
    /// each value.
    /// </summary>
    /// <remarks>
    /// A cell carries no discriminator: its type is the CLR type the reading side rebuilds the column with,
    /// <see cref="DbTypeConverter.ToType(FieldDbType)"/> of the column's <see cref="FieldDbType"/>, which travels
    /// once in the column header. <see cref="System.DBNull"/> is MessagePack nil; a <see cref="System.Data.DataRow"/>
    /// never holds a null reference, so nil is unambiguous.
    /// <para>
    /// The payload of a cell is exactly what the resolver's own formatter writes for that CLR type — the same
    /// formatter the <c>[code, value]</c> envelope of <see cref="WireValueFormatter"/> delegates to — so a value
    /// round-trips the same way it did inside the envelope. Each formatter is resolved as a closed generic, which
    /// keeps the path free of dynamic code.
    /// </para>
    /// </remarks>
    internal abstract class DataColumnCodec
    {
        /// <summary>
        /// Gets the CLR type of the cells, and the <see cref="System.Data.DataColumn.DataType"/> the reading side
        /// rebuilds the column with.
        /// </summary>
        public abstract Type ClrType { get; }

        /// <summary>
        /// Writes one cell.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="value">The cell value; <see cref="System.DBNull"/> and <c>null</c> write nil.</param>
        /// <param name="options">The serializer options whose resolver supplies the formatter.</param>
        public abstract void Write(ref MessagePackWriter writer, object? value, MessagePackSerializerOptions options);

        /// <summary>
        /// Reads one cell.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="options">The serializer options whose resolver supplies the formatter.</param>
        /// <returns>The value, or <see cref="System.DBNull.Value"/> for nil.</returns>
        public abstract object Read(ref MessagePackReader reader, MessagePackSerializerOptions options);

        /// <summary>
        /// Gets the codec for a column of the specified field type.
        /// </summary>
        /// <param name="fieldDbType">The column's field type.</param>
        /// <param name="options">The serializer options whose resolver supplies the formatter.</param>
        /// <exception cref="InvalidOperationException">
        /// The field type has no CLR type (<see cref="FieldDbType.Unknown"/>), or maps to a CLR type this codec does
        /// not know.
        /// </exception>
        public static DataColumnCodec For(FieldDbType fieldDbType, MessagePackSerializerOptions options)
        {
            var type = DbTypeConverter.ToType(fieldDbType);
            if (type == typeof(string)) return new Typed<string>(options);
            if (type == typeof(bool)) return new Typed<bool>(options);
            if (type == typeof(short)) return new Typed<short>(options);
            if (type == typeof(int)) return new Typed<int>(options);
            if (type == typeof(long)) return new Typed<long>(options);
            if (type == typeof(decimal)) return new Typed<decimal>(options);
            if (type == typeof(DateTime)) return new Typed<DateTime>(options);
            if (type == typeof(Guid)) return new Typed<Guid>(options);
            if (type == typeof(byte[])) return new Typed<byte[]>(options);

            // NOTE: Reached only if `DbTypeConverter.ToType` gains a CLR type without a case above.
            // `DataTableFormatterTests` round-trips every `FieldDbType`, so that change turns it red.
            throw new InvalidOperationException(
                $"No DataTable column codec for field type '{fieldDbType}' (CLR type '{type.FullName}').");
        }

        private sealed class Typed<T> : DataColumnCodec
        {
            private readonly IMessagePackFormatter<T> _formatter;

            public Typed(MessagePackSerializerOptions options)
            {
                _formatter = options.Resolver.GetFormatterWithVerify<T>();
            }

            public override Type ClrType => typeof(T);

            public override void Write(ref MessagePackWriter writer, object? value, MessagePackSerializerOptions options)
            {
                if (value is null or DBNull)
                {
                    writer.WriteNil();
                    return;
                }

                _formatter.Serialize(ref writer, value is T typed ? typed : Coerce(value), options);
            }

            public override object Read(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil())
                    return DBNull.Value;

                return _formatter.Deserialize(ref reader, options)!;
            }

            /// <summary>
            /// Converts a value whose type differs from the column's rebuilt type: a <see cref="double"/> in a column
            /// the wire declares <see cref="FieldDbType.Decimal"/>, or the <see cref="string"/> a SQLite reader returns
            /// for a column declared <see cref="FieldDbType.Guid"/>.
            /// </summary>
            /// <remarks>
            /// These are the conversions the reading side's <see cref="System.Data.DataColumn"/> applies when such a
            /// value is assigned to it (an <see cref="IConvertible"/> conversion, and a GUID parsed from its string),
            /// so converting before writing stores the same value while keeping the cell typed by its column. A
            /// value neither covers throws <see cref="InvalidCastException"/> here instead of on the reading side.
            /// </remarks>
            private static T Coerce(object value)
            {
                if (typeof(T) == typeof(Guid) && value is string text)
                    return (T)(object)new Guid(text);
                if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(typeof(T)))
                    return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                return (T)value;
            }
        }
    }
}
