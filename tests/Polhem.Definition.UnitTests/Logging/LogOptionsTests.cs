using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;

namespace Polhem.Definition.UnitTests.Logging
{
    public class LogOptionsTests
    {
        [Fact]
        [DisplayName("LogOptions.ToString returns the class name")]
        public void ToString_DefaultInstance_ReturnsTypeName()
        {
            var options = new LogOptions();
            Assert.Equal("LogOptions", options.ToString());
        }

        [Fact]
        [DisplayName("LogOptions default constructor initializes the DbAccess sub-options")]
        public void DefaultConstructor_InitializesDbAccess()
        {
            var first = new LogOptions();
            var second = new LogOptions();
            Assert.NotNull(first.DbAccess);
            // Each instance owns its sub-options; a shared default would let one host's change leak into another.
            Assert.NotSame(first.DbAccess, second.DbAccess);
        }

        [Fact]
        [DisplayName("DbAccessAnomalyLogOptions.ToString returns the class name")]
        public void DbAccessAnomalyLogOptions_ToString_ReturnsTypeName()
        {
            var options = new DbAccessAnomalyLogOptions();
            Assert.Equal("DbAccessAnomalyLogOptions", options.ToString());
        }

        [Fact]
        [DisplayName("DbAccessAnomalyLogOptions default property values match the specification")]
        public void DbAccessAnomalyLogOptions_DefaultValues_MatchSpecification()
        {
            var options = new DbAccessAnomalyLogOptions();
            Assert.Equal(DbAccessAnomalyLogLevel.Warning, options.Level);
            Assert.Equal(10000, options.AffectedRowThreshold);
            Assert.Equal(10000, options.ResultRowThreshold);
            Assert.Equal(300, options.ExecutionTimeThreshold);
        }
    }
}
