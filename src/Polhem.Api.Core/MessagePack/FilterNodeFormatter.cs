using System.Buffers;
using Polhem.Definition.Filters;
using MessagePack;
using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// Serializes the polymorphic <see cref="FilterNode"/> hierarchy as a property-name keyed map
    /// carrying an explicit <c>Kind</c> discriminator.
    /// </summary>
    /// <remarks>
    /// Mirrors what <see cref="FilterNodeCollectionJsonConverter"/> already does on the JSON wire: read
    /// <see cref="FilterNode.Kind"/>, pick the concrete type, bind its members. Doing the same here
    /// gives the two formats one shared mental model, and lets the definition layer drop
    /// <c>[Union]</c> — the only reason it was pinned to integer keys.
    /// <para>
    /// The discriminator is written as a member rather than as a leading array element so the
    /// payload stays self-describing: a reader that logs the map sees <c>Kind</c> named, not a bare
    /// integer whose meaning lives in another file.
    /// </para>
    /// <para>
    /// WARNING: Adding a property to <see cref="FilterCondition"/> or <see cref="FilterGroup"/> —
    /// or adding a third subclass — means updating this formatter. The guard is
    /// <c>WireContractDriftTests</c> (the member lists) and <c>WireCodecParityTests</c> (the read
    /// and write code), by way of the two subtype adapters <see cref="FilterConditionFormatter"/>
    /// and <see cref="FilterGroupFormatter"/> — this formatter covers the abstract base, which has
    /// no wire members of its own to compare.
    /// </para>
    /// </remarks>
    internal sealed class FilterNodeFormatter : IMessagePackFormatter<FilterNode?>
    {
        /// <summary>
        /// Member name of the type discriminator.
        /// </summary>
        private const string KindKey = nameof(FilterNode.Kind);

        /// <summary>
        /// Wire member names written for a <see cref="FilterCondition"/>, in write order.
        /// </summary>
        /// <remarks>
        /// The discriminator is not listed: <see cref="FilterNode.Kind"/> is get-only and therefore
        /// not a wire member by the definition the drift check uses. It is written alongside these,
        /// which is why the map header adds one.
        /// </remarks>
        internal static readonly string[] ConditionWireMembers =
        [
            nameof(FilterCondition.FieldName),
            nameof(FilterCondition.Operator),
            nameof(FilterCondition.Value),
            nameof(FilterCondition.SecondValue),
            nameof(FilterCondition.IgnoreIfNull),
        ];

        /// <summary>
        /// Wire member names written for a <see cref="FilterGroup"/>, in write order. The
        /// discriminator is excluded for the reason given on <see cref="ConditionWireMembers"/>.
        /// </summary>
        internal static readonly string[] GroupWireMembers =
        [
            nameof(FilterGroup.Operator),
            nameof(FilterGroup.Nodes),
        ];

        /// <summary>
        /// Serializes the node, dispatching on its concrete type.
        /// </summary>
        /// <exception cref="MessagePackSerializationException">
        /// Thrown for a subclass this formatter does not know.
        /// </exception>
        public void Serialize(ref MessagePackWriter writer, FilterNode? value, MessagePackSerializerOptions options)
        {
            switch (value)
            {
                case null:
                    writer.WriteNil();
                    return;

                case FilterCondition condition:
                    WriteCondition(ref writer, condition, options);
                    return;

                case FilterGroup group:
                    WriteGroup(ref writer, group, options);
                    return;

                default:
                    throw new MessagePackSerializationException(
                        $"No wire mapping for filter node type '{value.GetType().FullName}'. "
                        + "A new FilterNode subclass must be added to FilterNodeFormatter.");
            }
        }

        /// <summary>
        /// Deserializes the node, choosing the concrete type from the <c>Kind</c> discriminator.
        /// </summary>
        /// <remarks>
        /// The discriminator is not guaranteed to arrive first, so the payload is buffered as a
        /// sequence of key/value pairs and bound once the kind is known. Filter trees are small —
        /// a request-scoped predicate, not a data set — so the extra pass costs nothing measurable.
        /// <para>
        /// WARNING: each buffered member is read back through a new reader, and that reader must
        /// start at this node's depth. A fresh reader starts at zero, so a group nested inside a
        /// group never accumulated depth, the security depth limit never fired, and a deeply
        /// nested filter ended in a stack overflow that takes the whole process down. The depth is
        /// carried by <see cref="Read{T}"/>; <c>MessagePackDepthLimitTests</c> pins it.
        /// </para>
        /// </remarks>
        /// <exception cref="MessagePackSerializationException">
        /// Thrown when the discriminator is missing or unknown.
        /// </exception>
        public FilterNode? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            options.Security.DepthStep(ref reader);
            try
            {
                var count = reader.ReadMapHeader();
                FilterNodeKind? kind = null;
                var buffered = new List<(string Key, ReadOnlySequence<byte> Value)>(count);

                for (var i = 0; i < count; i++)
                {
                    var key = reader.ReadString() ?? string.Empty;
                    if (key == KindKey)
                    {
                        kind = (FilterNodeKind)reader.ReadInt32();
                        continue;
                    }

                    var start = reader.Position;
                    reader.Skip();
                    buffered.Add((key, reader.Sequence.Slice(start, reader.Position)));
                }

                var depth = reader.Depth;
                return kind switch
                {
                    FilterNodeKind.Condition => ReadCondition(buffered, depth, options),
                    FilterNodeKind.Group => ReadGroup(buffered, depth, options),
                    _ => throw new MessagePackSerializationException(
                        $"Filter node payload carries no usable '{KindKey}' discriminator."),
                };
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <summary>
        /// Writes a <see cref="FilterCondition"/>.
        /// </summary>
        private static void WriteCondition(
            ref MessagePackWriter writer, FilterCondition value, MessagePackSerializerOptions options)
        {
            // +1 for the Kind discriminator, which is written but is not a wire member.
            writer.WriteMapHeader(ConditionWireMembers.Length + 1);

            writer.Write(KindKey);
            writer.Write((int)FilterNodeKind.Condition);

            writer.Write(nameof(FilterCondition.FieldName));
            MessagePackSerializer.Serialize(ref writer, value.FieldName, options);

            writer.Write(nameof(FilterCondition.Operator));
            MessagePackSerializer.Serialize(ref writer, value.Operator, options);

            writer.Write(nameof(FilterCondition.Value));
            MessagePackSerializer.Serialize<object?>(ref writer, value.Value, options);

            writer.Write(nameof(FilterCondition.SecondValue));
            MessagePackSerializer.Serialize<object?>(ref writer, value.SecondValue, options);

            writer.Write(nameof(FilterCondition.IgnoreIfNull));
            MessagePackSerializer.Serialize(ref writer, value.IgnoreIfNull, options);
        }

        /// <summary>
        /// Writes a <see cref="FilterGroup"/>.
        /// </summary>
        private static void WriteGroup(
            ref MessagePackWriter writer, FilterGroup value, MessagePackSerializerOptions options)
        {
            // +1 for the Kind discriminator, which is written but is not a wire member.
            writer.WriteMapHeader(GroupWireMembers.Length + 1);

            writer.Write(KindKey);
            writer.Write((int)FilterNodeKind.Group);

            writer.Write(nameof(FilterGroup.Operator));
            MessagePackSerializer.Serialize(ref writer, value.Operator, options);

            writer.Write(nameof(FilterGroup.Nodes));
            MessagePackSerializer.Serialize(ref writer, value.Nodes, options);
        }

        /// <summary>
        /// Binds the buffered members onto a <see cref="FilterCondition"/>.
        /// </summary>
        private static FilterCondition ReadCondition(
            List<(string Key, ReadOnlySequence<byte> Value)> buffered, int depth, MessagePackSerializerOptions options)
        {
            var result = new FilterCondition();
            foreach (var (key, value) in buffered)
            {
                switch (key)
                {
                    case nameof(FilterCondition.FieldName):
                        result.FieldName = Read<string>(value, depth, options) ?? string.Empty;
                        break;
                    case nameof(FilterCondition.Operator):
                        result.Operator = Read<ComparisonOperator>(value, depth, options);
                        break;
                    case nameof(FilterCondition.Value):
                        result.Value = Read<object?>(value, depth, options);
                        break;
                    case nameof(FilterCondition.SecondValue):
                        result.SecondValue = Read<object?>(value, depth, options);
                        break;
                    case nameof(FilterCondition.IgnoreIfNull):
                        result.IgnoreIfNull = Read<bool>(value, depth, options);
                        break;
                    default:
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Binds the buffered members onto a <see cref="FilterGroup"/>.
        /// </summary>
        private static FilterGroup ReadGroup(
            List<(string Key, ReadOnlySequence<byte> Value)> buffered, int depth, MessagePackSerializerOptions options)
        {
            var result = new FilterGroup();
            foreach (var (key, value) in buffered)
            {
                switch (key)
                {
                    case nameof(FilterGroup.Operator):
                        result.Operator = Read<LogicalOperator>(value, depth, options);
                        break;
                    case nameof(FilterGroup.Nodes):
                        result.Nodes = Read<FilterNodeCollection>(value, depth, options) ?? [];
                        break;
                    default:
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Deserializes one buffered member value.
        /// </summary>
        /// <param name="value">The buffered member.</param>
        /// <param name="depth">The depth of the node that owns the member.</param>
        /// <param name="options">The serializer options.</param>
        private static T? Read<T>(ReadOnlySequence<byte> value, int depth, MessagePackSerializerOptions options)
        {
            var reader = new MessagePackReader(value) { Depth = depth };
            return MessagePackSerializer.Deserialize<T>(ref reader, options);
        }
    }
}
