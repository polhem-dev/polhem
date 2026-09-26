using System.ComponentModel;
using Polhem.Db.Dml;

namespace Polhem.Db.UnitTests
{
    public class DefaultParameterCollectorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("The constructor throws ArgumentException for a null or empty prefix")]
        public void Constructor_NullOrEmptyPrefix_Throws(string? prefix)
        {
            Assert.Throws<ArgumentException>(() => new DefaultParameterCollector(prefix!));
        }

        [Fact]
        [DisplayName("The constructor records the Prefix")]
        public void Constructor_ValidPrefix_StoresPrefix()
        {
            var collector = new DefaultParameterCollector("@");

            Assert.Equal("@", collector.Prefix);
        }

        [Fact]
        [DisplayName("Consecutive Add calls produce @p0, @p1 and @p2 and return the matching names")]
        public void Add_SequentialCalls_GeneratesIncrementingNames()
        {
            var collector = new DefaultParameterCollector("@");

            string n0 = collector.Add(1);
            string n1 = collector.Add("x");
            string n2 = collector.Add(3.14);

            Assert.Equal("@p0", n0);
            Assert.Equal("@p1", n1);
            Assert.Equal("@p2", n2);
        }

        [Fact]
        [DisplayName("GetAll returns every added parameter key/value pair")]
        public void GetAll_ReturnsAllAddedParameters()
        {
            var collector = new DefaultParameterCollector("@");
            collector.Add(10);
            collector.Add("abc");

            var all = collector.GetAll();

            Assert.Equal(2, all.Count);
            Assert.Equal(10, all["@p0"]);
            Assert.Equal("abc", all["@p1"]);
        }

        [Fact]
        [DisplayName("Add accepts other prefixes (such as Oracle's ':')")]
        public void Add_OraclePrefix_GeneratesColonNames()
        {
            var collector = new DefaultParameterCollector(":");

            string n0 = collector.Add(1);

            Assert.Equal(":p0", n0);
        }
    }
}
