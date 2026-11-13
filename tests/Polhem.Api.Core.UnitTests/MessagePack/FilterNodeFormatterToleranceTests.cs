using System.Buffers;
using System.ComponentModel;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// How the MessagePack filter node formatter treats a node type it has no mapping for, a payload without a usable
    /// discriminator, unknown keys and nil members.
    /// </summary>
    public class FilterNodeFormatterToleranceTests
    {
        private delegate void WriteBody(ref MessagePackWriter writer);

        private static byte[] Build(WriteBody write)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            write(ref writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        /// <summary>A filter node subclass the formatter has no wire mapping for.</summary>
        private sealed class UnmappedNode : FilterNode
        {
            public override FilterNodeKind Kind => (FilterNodeKind)99;
        }

        [Fact]
        [DisplayName("Serializing a filter node subclass with no wire mapping is refused")]
        public void Serialize_UnmappedSubclass_Throws()
        {
            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Serialize<FilterNode>(new UnmappedNode()));
        }

        [Fact]
        [DisplayName("A null filter node serializes as nil and reads back as null")]
        public void Serialize_Null_RoundTripsAsNil()
        {
            var bytes = MessagePackCodec.Serialize<FilterNode?>(null);

            Assert.Null(MessagePackCodec.Deserialize<FilterNode?>(bytes));
        }

        [Fact]
        [DisplayName("A filter node payload without a Kind discriminator is refused")]
        public void Deserialize_MissingKind_Throws()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteMapHeader(1);
                w.Write(nameof(FilterCondition.FieldName));
                w.Write("name");
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<FilterNode>(bytes));
        }

        [Fact]
        [DisplayName("A filter node payload with an unknown Kind value is refused")]
        public void Deserialize_UnknownKind_Throws()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteMapHeader(1);
                w.Write(nameof(FilterNode.Kind));
                w.Write(99);
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<FilterNode>(bytes));
        }

        [Fact]
        [DisplayName("A condition with a nil field name, a nil key and an unknown key reads with an empty field name")]
        public void Deserialize_ConditionWithNilAndUnknownKeys_ReadsDefaults()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteMapHeader(4);
                w.Write(nameof(FilterNode.Kind));
                w.Write((int)FilterNodeKind.Condition);
                w.Write(nameof(FilterCondition.FieldName));
                w.WriteNil();
                w.WriteNil();
                w.Write("ignored");
                w.Write("AddedInAFutureVersion");
                w.Write(1);
            });

            var condition = Assert.IsType<FilterCondition>(MessagePackCodec.Deserialize<FilterNode>(bytes));

            Assert.Equal(string.Empty, condition.FieldName);
        }

        [Fact]
        [DisplayName("A group with nil nodes and an unknown key reads with an empty node collection")]
        public void Deserialize_GroupWithNilNodes_ReadsEmptyCollection()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteMapHeader(3);
                w.Write(nameof(FilterGroup.Nodes));
                w.WriteNil();
                w.Write("AddedInAFutureVersion");
                w.Write(1);
                w.Write(nameof(FilterNode.Kind));
                w.Write((int)FilterNodeKind.Group);
            });

            var group = Assert.IsType<FilterGroup>(MessagePackCodec.Deserialize<FilterNode>(bytes));

            Assert.Empty(group.Nodes);
        }
    }
}
