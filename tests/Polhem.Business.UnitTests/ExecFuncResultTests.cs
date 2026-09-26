using Polhem.Definition.Collections;
using System.ComponentModel;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the behavior <see cref="ExecFuncResult"/> inherits from <see cref="BusinessResult"/>.
    /// </summary>
    public class ExecFuncResultTests
    {
        [Fact]
        [DisplayName("ExecFuncResult initializes Parameters lazily after construction")]
        public void DefaultConstructor_ParametersLazyInitialized()
        {
            var result = new ExecFuncResult();

            Assert.NotNull(result.Parameters);
            result.Parameters.Add("key", "value");
            Assert.Equal("value", result.Parameters.GetValue<string>("key"));
        }

        [Fact]
        [DisplayName("ExecFuncResult is a subclass of BusinessResult")]
        public void ExecFuncResult_IsBusinessResult()
        {
            var result = new ExecFuncResult();
            Assert.IsType<BusinessResult>(result, exactMatch: false);
        }
    }
}
