using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests.Serialization
{
    /// <summary>
    /// After a failed serialization, the object's <see cref="SerializeState"/> must be reset to <see cref="SerializeState.None"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both codecs used to be written straight through as "<c>NotifyBefore</c> → serialize → <c>NotifyAfter</c>".
    /// When serialization threw, the last step never ran and the object stayed in the <c>Serialize</c> state **for good**.
    /// </para>
    /// <para>
    /// Why "for good" matters: the serialized object is often a process-wide cached definition instance. After one
    /// failure, every empty-collection getter on that instance returned <c>null</c> from then on (<c>IsSerializeEmpty</c>
    /// holds only in the <c>Serialize</c> state), and several callers dereference them with <c>!</c>, since an empty
    /// collection is a perfectly normal state. A transient failure became a permanent one.
    /// </para>
    /// </remarks>
    public class SerializeStateResetTests
    {
        /// <summary>
        /// A property getter throws, which makes serialization fail partway through.
        /// </summary>
        public class ExplodingValue : IObjectSerialize
        {
            private SerializeState _state;

            /// <summary>A property that throws whenever it is serialized.</summary>
            /// <remarks>
            /// It must be read-write. <c>XmlSerializer</c> skips read-only properties (except collections), so the
            /// getter of a read-only version would never be called and the test would check nothing.
            /// </remarks>
            public string Boom
            {
                get => throw new InvalidOperationException($"serialization blew up in state {_state}");
                set => _lastSet = value;
            }

            private string _lastSet = string.Empty;

            /// <inheritdoc/>
            [XmlIgnore]
            public SerializeState SerializeState => _state;

            /// <inheritdoc/>
            public void SetSerializeState(SerializeState serializeState) => _state = serializeState;

            /// <inheritdoc/>
            public bool IsSerializeEmpty() => _lastSet.Length < 0;
        }

        [Fact]
        [DisplayName("After XmlCodec fails to serialize, the object's SerializeState is reset to None")]
        public void XmlCodec_SerializeThrows_StateIsReset()
        {
            var value = new ExplodingValue();

            Assert.ThrowsAny<Exception>(() => XmlCodec.Serialize(value));

            Assert.Equal(SerializeState.None, value.SerializeState);
        }

        [Fact]
        [DisplayName("After JsonCodec fails to serialize, the object's SerializeState is reset to None")]
        public void JsonCodec_SerializeThrows_StateIsReset()
        {
            var value = new ExplodingValue();

            Assert.ThrowsAny<Exception>(() => JsonCodec.Serialize(value));

            Assert.Equal(SerializeState.None, value.SerializeState);
        }

        [Fact]
        [DisplayName("Control: after a successful serialization SerializeState is also None, not because it was never set")]
        public void Serialize_Succeeds_StateIsAlsoNone()
        {
            // Without this test, never calling `NotifyBefore` would also satisfy the two tests above.
            // That would make `IsSerializeEmpty` never hold and silently change the output.
            var value = new WellBehavedValue();

            var xml = XmlCodec.Serialize(value);

            Assert.Contains("well-behaved", xml, StringComparison.Ordinal);
            Assert.Equal(SerializeState.None, value.SerializeState);
            Assert.True(value.SawSerializeState, "The value never entered the Serialize state during serialization, so the pairing never happened.");
        }

        /// <summary>
        /// Serializes successfully and records whether it was actually marked <see cref="SerializeState.Serialize"/> along the way.
        /// </summary>
        public class WellBehavedValue : IObjectSerialize
        {
            private SerializeState _state;

            /// <summary>Content that serializes normally.</summary>
            public string Name { get; set; } = "well-behaved";

            /// <summary>Whether the value entered <see cref="SerializeState.Serialize"/> during serialization.</summary>
            [XmlIgnore]
            public bool SawSerializeState { get; private set; }

            /// <inheritdoc/>
            [XmlIgnore]
            public SerializeState SerializeState => _state;

            /// <inheritdoc/>
            public void SetSerializeState(SerializeState serializeState)
            {
                if (serializeState == SerializeState.Serialize) { SawSerializeState = true; }
                _state = serializeState;
            }

            /// <inheritdoc/>
            public bool IsSerializeEmpty() => _state == SerializeState.None && false;
        }
    }
}
