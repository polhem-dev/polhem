using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using MessagePack.Resolvers;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// SafeMessagePackSerializerOptions tests: verifies that ThrowIfDeserializingTypeIsDisallowed blocks types not on
    /// the allow-list, and that Clone returns a new instance of the same type.
    /// </summary>
    public class SafeMessagePackSerializerOptionsTests
    {
        private static SafeMessagePackSerializerOptions Create()
            => new SafeMessagePackSerializerOptions(StandardResolver.Instance);

        [Fact]
        [DisplayName("A type not on the allow-list throws InvalidOperationException")]
        public void ThrowIfDisallowed_TypeNotInWhitelist_Throws()
        {
            var options = Create();

            var ex = Assert.Throws<InvalidOperationException>(
                () => options.ThrowIfDeserializingTypeIsDisallowed(typeof(Random)));

            Assert.Contains("not in the allowed type whitelist", ex.Message);
            Assert.Contains("System.Random", ex.Message);
        }

        [Fact]
        [DisplayName("A primitive type on the allow-list does not throw")]
        public void ThrowIfDisallowed_AllowedPrimitive_DoesNotThrow()
        {
            var options = Create();

            // `System.String` is a member of `AllowedPrimitiveTypes`.
            var ex = Record.Exception(() => options.ThrowIfDeserializingTypeIsDisallowed(typeof(string)));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("The copy made by WithResolver is a SafeMessagePackSerializerOptions")]
        public void Clone_ViaWithResolver_ReturnsSafeOptions()
        {
            var options = Create();

            // `WithResolver` calls `Clone` internally to create a new instance of the same type.
            var cloned = options.WithResolver(ContractlessStandardResolver.Instance);

            Assert.IsType<SafeMessagePackSerializerOptions>(cloned);
            Assert.NotSame(options, cloned);
        }
    }
}
