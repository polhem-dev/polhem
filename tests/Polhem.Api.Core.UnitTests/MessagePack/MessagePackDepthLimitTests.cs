using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Transformers;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Pins the object graph depth limit on both body codecs, and that a nested filter cannot slip under it.
    /// </summary>
    /// <remarks>
    /// The filter formatter reads each buffered member back through a new reader. A fresh reader starts at depth
    /// zero, so a group nested in a group never accumulated depth, the limit never fired, and a deep enough payload
    /// ended in a stack overflow that takes the process down — reachable by an anonymous caller. The payloads here
    /// are built iteratively, never by recursion, so building them cannot itself exhaust the stack, and they are
    /// kept shallow enough that without the fix the decode simply succeeds and the test fails instead of crashing
    /// the run.
    /// </remarks>
    public class MessagePackDepthLimitTests
    {
        /// <summary>
        /// Deeper than the limit even counting one level per group, yet shallow enough to decode without the fix.
        /// </summary>
        private const int NestedGroups = 200;

        [Fact]
        [DisplayName("The MessagePack options treat payloads as untrusted data with the shared depth limit")]
        public void SerializerOptions_UseUntrustedDataWithDepthLimit()
        {
            var security = MessagePackCodec.SerializerOptions.Security;

            Assert.True(security.HashCollisionResistant);
            Assert.Equal(MessagePackCodec.MaxObjectGraphDepth, security.MaximumObjectGraphDepth);
        }

        [Fact]
        [DisplayName("The options still enforce the type whitelist after the security settings are applied")]
        public void SerializerOptions_KeepTheWhitelistingOptionsType()
        {
            // WithSecurity clones the options; the clone must stay the whitelisting subclass.
            Assert.IsType<SafeMessagePackSerializerOptions>(MessagePackCodec.SerializerOptions);
        }

        [Fact]
        [DisplayName("A filter nested beyond the depth limit fails with a catchable deserialization error")]
        public void Deserialize_FilterNestedBeyondLimit_ThrowsSerializationError()
        {
            var bytes = BuildNestedGroups(NestedGroups);

            var ex = Assert.Throws<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<FilterNode>(bytes));

            Assert.IsType<InsufficientExecutionStackException>(ex.GetBaseException());
        }

        [Fact]
        [DisplayName("A filter nested within the depth limit still round-trips")]
        public void Deserialize_FilterNestedWithinLimit_RoundTrips()
        {
            // Each group costs two levels: the node and its node list.
            const int groups = 20;
            var bytes = BuildNestedGroups(groups);

            var node = MessagePackCodec.Deserialize<FilterNode>(bytes);

            var depth = 0;
            while (node is FilterGroup group && group.Nodes.Count > 0)
            {
                depth++;
                node = group.Nodes[0];
            }
            Assert.Equal(groups - 1, depth);
        }

        [Fact]
        [DisplayName("A filter nested beyond the limit fails the same way on the JSON body codec")]
        public void JsonCodec_FilterNestedBeyondLimit_ThrowsJsonException()
        {
            var json = new StringBuilder();
            for (var i = 0; i < NestedGroups; i++)
                json.Append("{\"kind\":\"Group\",\"nodes\":[");
            json.Append("{\"kind\":\"Condition\",\"fieldName\":\"a\"}");
            for (var i = 0; i < NestedGroups; i++)
                json.Append("]}");

            var serializer = new JsonPayloadSerializer();

            Assert.ThrowsAny<JsonException>(() =>
                serializer.Deserialize(Encoding.UTF8.GetBytes(json.ToString()), typeof(FilterGroup)));
        }

        /// <summary>
        /// Writes <paramref name="groups"/> groups, each holding the next, around an empty innermost group.
        /// MessagePack headers carry their lengths up front and nothing closes them, so the prefixes can simply be
        /// written one after another.
        /// </summary>
        private static byte[] BuildNestedGroups(int groups)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            for (var i = 0; i < groups; i++)
            {
                var innermost = i == groups - 1;
                writer.WriteMapHeader(2);
                writer.Write(nameof(FilterNode.Kind));
                writer.Write((int)FilterNodeKind.Group);
                writer.Write(nameof(FilterGroup.Nodes));
                writer.WriteArrayHeader(innermost ? 0 : 1);
            }
            writer.Flush();
            return buffer.WrittenMemory.ToArray();
        }
    }
}
