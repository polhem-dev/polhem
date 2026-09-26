using System.ComponentModel;

namespace Polhem.ObjectCaching.UnitTests
{
    public class CacheItemPolicyTests
    {
        [Fact]
        [DisplayName("The default constructor leaves the properties at their default values")]
        public void DefaultConstructor_PropertiesAreDefault()
        {
            var policy = new CacheItemPolicy();

            Assert.Equal(DateTimeOffset.MaxValue, policy.AbsoluteExpiration);
            Assert.Equal(TimeSpan.Zero, policy.SlidingExpiration);
            Assert.Null(policy.ChangeMonitorFilePaths);
        }

        [Fact]
        [DisplayName("Constructing with SlidingTime sets only SlidingExpiration")]
        public void Constructor_SlidingTime_SetsSlidingExpirationOnly()
        {
            var policy = new CacheItemPolicy(CacheTimeKind.SlidingTime, 15);

            Assert.Equal(TimeSpan.FromMinutes(15), policy.SlidingExpiration);
            Assert.Equal(DateTimeOffset.MaxValue, policy.AbsoluteExpiration);
        }

        [Fact]
        [DisplayName("Constructing with AbsoluteTime sets only AbsoluteExpiration")]
        public void Constructor_AbsoluteTime_SetsAbsoluteExpirationOnly()
        {
            var before = DateTimeOffset.UtcNow.AddMinutes(10);
            var policy = new CacheItemPolicy(CacheTimeKind.AbsoluteTime, 10);
            var after = DateTimeOffset.UtcNow.AddMinutes(10);

            Assert.True(policy.AbsoluteExpiration >= before && policy.AbsoluteExpiration <= after);
            Assert.Equal(TimeSpan.Zero, policy.SlidingExpiration);
        }

        [Fact]
        [DisplayName("ChangeMonitorFilePaths can be assigned")]
        public void ChangeMonitorFilePaths_IsAssignable()
        {
            var policy = new CacheItemPolicy
            {
                ChangeMonitorFilePaths = new[] { "a.txt", "b.txt" }
            };

            Assert.Equal(2, policy.ChangeMonitorFilePaths!.Length);
        }

        [Theory]
        [InlineData(CacheTimeKind.SlidingTime)]
        [InlineData(CacheTimeKind.AbsoluteTime)]
        [DisplayName("CacheTimeKind values are defined members")]
        public void CacheTimeKind_DefinedValues_AreValid(CacheTimeKind kind)
        {
            Assert.True(Enum.IsDefined(kind));
        }
    }
}
