using System.ComponentModel;
using Polhem.Base.Exceptions;

namespace Polhem.Base.UnitTests.Exceptions
{
    /// <summary>
    /// Tests for <see cref="UserMessageException"/> covering constructor overloads,
    /// inheritance behaviour, and catch semantics.
    /// </summary>
    public class UserMessageExceptionTests
    {
        [Fact]
        [DisplayName("The single-parameter constructor sets Message and leaves InnerException null")]
        public void Ctor_WithMessage_SetsMessage()
        {
            var ex = new UserMessageException("test message");

            Assert.Equal("test message", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        [DisplayName("The two-parameter constructor sets both Message and InnerException")]
        public void Ctor_WithMessageAndInner_SetsBoth()
        {
            var inner = new InvalidOperationException("inner cause");
            var ex = new UserMessageException("test message", inner);

            Assert.Equal("test message", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        [DisplayName("UserMessageException can be caught by catch (Exception)")]
        public void Throw_CanBeCaughtAsException()
        {
            Exception? caught = null;
            try
            {
                throw new UserMessageException("test");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.IsType<UserMessageException>(caught);
        }

        [Fact]
        [DisplayName("UserMessageException is not caught by catch (InvalidOperationException) (the types are independent)")]
        public void Throw_NotCaughtAsInvalidOperationException()
        {
            UserMessageException? rethrown = null;
            try
            {
                try
                {
                    throw new UserMessageException("test");
                }
                catch (InvalidOperationException)
                {
                    // Should not reach here.
                    Assert.Fail("UserMessageException should not be caught as InvalidOperationException.");
                }
            }
            catch (UserMessageException ex)
            {
                rethrown = ex;
            }

            Assert.NotNull(rethrown);
        }

        [Fact]
        [DisplayName("UserMessageException is not caught by catch (ArgumentException) (the types are independent)")]
        public void Throw_NotCaughtAsArgumentException()
        {
            UserMessageException? rethrown = null;
            try
            {
                try
                {
                    throw new UserMessageException("test");
                }
                catch (ArgumentException)
                {
                    // Should not reach here.
                    Assert.Fail("UserMessageException should not be caught as ArgumentException.");
                }
            }
            catch (UserMessageException ex)
            {
                rethrown = ex;
            }

            Assert.NotNull(rethrown);
        }
    }
}
