using System.Buffers;
using System.ComponentModel;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// A nil element inside a framework collection is malformed input and must fail as a deserialization error.
    /// </summary>
    /// <remarks>
    /// The formatter used to hand the nil straight to the collection's <c>Add</c>, whose
    /// <c>NullReferenceException</c> reached the caller as an internal server error rather than as a payload
    /// the server could not read. The keyed-collection formatter used to skip the nil instead, which returned
    /// a shorter collection than the peer sent without any error.
    /// </remarks>
    public class CollectionBaseFormatterNilElementTests
    {
        [Fact]
        [DisplayName("A sort field collection holding a nil element fails with a MessagePackSerializationException")]
        public void Deserialize_SortFieldCollectionWithNilElement_ThrowsSerializationException()
        {
            var bytes = ArrayWithNil();

            var exception = Record.Exception(() => MessagePackCodec.Deserialize<SortFieldCollection>(bytes));

            Assert.IsType<MessagePackSerializationException>(exception);
            Assert.DoesNotContain(Flatten(exception!), e => e is NullReferenceException);
        }

        [Fact]
        [DisplayName("A filter node collection holding a nil element fails with a MessagePackSerializationException")]
        public void Deserialize_FilterNodeCollectionWithNilElement_ThrowsSerializationException()
        {
            var bytes = ArrayWithNil();

            var exception = Record.Exception(() => MessagePackCodec.Deserialize<FilterNodeCollection>(bytes));

            Assert.IsType<MessagePackSerializationException>(exception);
            Assert.DoesNotContain(Flatten(exception!), e => e is NullReferenceException);
        }

        [Fact]
        [DisplayName("A keyed list item collection holding a nil element fails with a MessagePackSerializationException instead of dropping it")]
        public void Deserialize_ListItemCollectionWithNilElement_ThrowsSerializationException()
        {
            var bytes = ArrayWithNil();

            var exception = Record.Exception(() => MessagePackCodec.Deserialize<ListItemCollection>(bytes));

            Assert.IsType<MessagePackSerializationException>(exception);
        }

        private static byte[] ArrayWithNil()
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(1);
            writer.WriteNil();
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        private static IEnumerable<Exception> Flatten(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                yield return current;
        }
    }
}
