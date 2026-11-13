using System.ComponentModel;
using Polhem.Core.Collections;

namespace Polhem.Core.UnitTests.Collections
{
    public class DictionaryExtensionsTests
    {
        [Fact]
        [DisplayName("WithIgnoreCaseKeys returns a dictionary that already ignores case as it is")]
        public void WithIgnoreCaseKeys_AlreadyIgnoresCase_ReturnsSameInstance()
        {
            var dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Qty"] = 1 };

            Assert.Same(dictionary, dictionary.WithIgnoreCaseKeys());
        }

        [Fact]
        [DisplayName("WithIgnoreCaseKeys copies a case-sensitive dictionary into one that matches keys in any case")]
        public void WithIgnoreCaseKeys_CaseSensitive_CopiesIgnoringCase()
        {
            var dictionary = new Dictionary<string, int>(StringComparer.Ordinal) { ["Qty"] = 1 };

            var result = dictionary.WithIgnoreCaseKeys();

            Assert.NotSame(dictionary, result);
            Assert.Equal(1, result["QTY"]);
        }

        [Fact]
        [DisplayName("WithIgnoreCaseKeys turns null into an empty dictionary that ignores case")]
        public void WithIgnoreCaseKeys_Null_ReturnsEmpty()
        {
            var result = ((Dictionary<string, string>?)null).WithIgnoreCaseKeys();

            Assert.Empty(result);
            Assert.Same(StringComparer.OrdinalIgnoreCase, result.Comparer);
        }
    }
}
