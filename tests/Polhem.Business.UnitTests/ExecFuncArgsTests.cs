using System.ComponentModel;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the constructors and properties of <see cref="ExecFuncArgs"/> and the lazy initialization of <see cref="BusinessArgs.Parameters"/>.
    /// </summary>
    public class ExecFuncArgsTests
    {
        [Fact]
        [DisplayName("The default constructor sets FuncId to an empty string")]
        public void DefaultConstructor_FuncIdIsEmpty()
        {
            var args = new ExecFuncArgs();
            Assert.Equal(string.Empty, args.FuncId);
        }

        [Fact]
        [DisplayName("The constructor that takes funcID sets FuncId")]
        public void Constructor_WithFuncId_SetsFuncId()
        {
            var args = new ExecFuncArgs("SayHello");
            Assert.Equal("SayHello", args.FuncId);
        }

        [Fact]
        [DisplayName("FuncId can be set again")]
        public void FuncId_IsSettable()
        {
            var args = new ExecFuncArgs("First");
            args.FuncId = "Second";
            Assert.Equal("Second", args.FuncId);
        }

        [Fact]
        [DisplayName("Parameters is lazily initialized and returns the same instance on repeated access")]
        public void Parameters_LazyInitialized_ReturnsSameInstance()
        {
            var args = new ExecFuncArgs();

            var first = args.Parameters;
            var second = args.Parameters;

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
    }
}
