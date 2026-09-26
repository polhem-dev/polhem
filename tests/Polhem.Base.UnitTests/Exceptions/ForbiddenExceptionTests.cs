using System.ComponentModel;
using Polhem.Base.Exceptions;

namespace Polhem.Base.UnitTests.Exceptions
{
    public class ForbiddenExceptionTests
    {
        [Fact]
        [DisplayName("The single-parameter constructor sets Message and leaves InnerException null")]
        public void Ctor_WithMessage_SetsMessage()
        {
            var ex = new ForbiddenException("permission denied");
            Assert.Equal("permission denied", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        [DisplayName("The two-parameter constructor sets both Message and InnerException")]
        public void Ctor_WithMessageAndInner_SetsBoth()
        {
            var inner = new InvalidOperationException("root cause");
            var ex = new ForbiddenException("permission denied", inner);
            Assert.Equal("permission denied", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        [DisplayName("ForbiddenException can be caught by catch (Exception)")]
        public void Throw_CanBeCaughtAsException()
        {
            Exception? caught = null;
            try
            {
                throw new ForbiddenException("test");
            }
            catch (Exception ex)
            {
                caught = ex;
            }
            Assert.NotNull(caught);
            Assert.IsType<ForbiddenException>(caught);
        }
    }
}
