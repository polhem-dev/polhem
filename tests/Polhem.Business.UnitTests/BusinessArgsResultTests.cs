using System.ComponentModel;
using Polhem.Definition.Collections;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the lazy initialization and setter of <c>Parameters</c> on <see cref="BusinessArgs"/> and <see cref="BusinessResult"/>
    /// (verified through the <see cref="ExecFuncArgs"/> and <see cref="ExecFuncResult"/> subclasses).
    /// </summary>
    public class BusinessArgsResultTests
    {
        [Fact]
        [DisplayName("BusinessArgs.Parameters lazily creates a non-null collection on first access")]
        public void BusinessArgs_Parameters_LazyInitialized()
        {
            var args = new ExecFuncArgs();
            var parameters = args.Parameters;

            Assert.NotNull(parameters);
            Assert.Empty(parameters);
        }

        [Fact]
        [DisplayName("The BusinessArgs.Parameters setter replaces the existing collection")]
        public void BusinessArgs_Parameters_Setter_Overrides()
        {
            var args = new ExecFuncArgs();
            args.Parameters.Add("old", "value");

            var replacement = new ParameterCollection
            {
                { "new", "value" }
            };
            args.Parameters = replacement;

            Assert.Same(replacement, args.Parameters);
            Assert.False(args.Parameters.Contains("old"));
            Assert.True(args.Parameters.Contains("new"));
        }

        [Fact]
        [DisplayName("BusinessResult.Parameters behaves the same as BusinessArgs")]
        public void BusinessResult_Parameters_LazyAndSettable()
        {
            var result = new ExecFuncResult();

            var parameters = result.Parameters;
            Assert.NotNull(parameters);

            var replacement = new ParameterCollection
            {
                { "k", 42 }
            };
            result.Parameters = replacement;

            Assert.Same(replacement, result.Parameters);
            Assert.Equal(42, result.Parameters.GetValue<int>("k"));
        }
    }
}
