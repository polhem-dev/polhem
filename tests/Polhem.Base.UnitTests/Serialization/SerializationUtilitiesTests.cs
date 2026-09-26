using System.Collections;
using System.ComponentModel;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests.Serialization
{
    /// <summary>
    /// Tests for <see cref="SerializationUtilities.IsSerializeEmpty"/> covering all
    /// serialization-state branches and value-emptiness paths.
    /// </summary>
    public class SerializationUtilitiesTests
    {
        private sealed class EmptySerializeObject : IObjectSerializeEmpty
        {
            public bool IsSerializeEmpty { get; set; }
        }

        private sealed class PureEnumerable : IEnumerable
        {
            private readonly object[] _items;
            public PureEnumerable(params object[] items) => _items = items;
            public IEnumerator GetEnumerator() => _items.GetEnumerator();
        }

        [Fact]
        [DisplayName("IsSerializeEmpty returns false when the state is None")]
        public void IsSerializeEmpty_StateNone_ReturnsFalse()
        {
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.None, null!));
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.None, new List<int>()));
        }

        [Fact]
        [DisplayName("IsSerializeEmpty returns true for a null value in the Serialize state")]
        public void IsSerializeEmpty_SerializeAndNull_ReturnsTrue()
        {
            Assert.True(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, null!));
        }

        [Fact]
        [DisplayName("IsSerializeEmpty honors the state reported by IObjectSerializeEmpty")]
        public void IsSerializeEmpty_ObjectSerializeEmpty_ReflectsProperty()
        {
            var emptyObj = new EmptySerializeObject { IsSerializeEmpty = true };
            Assert.True(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, emptyObj));

            var notEmptyObj = new EmptySerializeObject { IsSerializeEmpty = false };
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, notEmptyObj));
        }

        [Fact]
        [DisplayName("IsSerializeEmpty returns true for an empty IList and false for a non-empty one")]
        public void IsSerializeEmpty_IList_ReflectsEmptiness()
        {
            Assert.True(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, new List<int>()));
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, new List<int> { 1 }));
        }

        [Fact]
        [DisplayName("IsSerializeEmpty treats an IEnumerable as empty when it yields no items")]
        public void IsSerializeEmpty_IEnumerable_ReflectsEmptiness()
        {
            Assert.True(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, new PureEnumerable()));
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, new PureEnumerable(1, 2)));
        }

        [Fact]
        [DisplayName("IsSerializeEmpty returns false for other types through the default branch")]
        public void IsSerializeEmpty_DefaultBranch_ReturnsFalse()
        {
            // Primitives such as int and string match no case, so the default branch returns false.
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, 123));
            Assert.False(SerializationUtilities.IsSerializeEmpty(SerializeState.Serialize, "abc"));
        }
    }
}
