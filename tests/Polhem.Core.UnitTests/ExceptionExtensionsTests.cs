using System.ComponentModel;
using System.Reflection;
using Polhem.Core.Exceptions;

namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// Tests for <see cref="Polhem.Core.Exceptions.ExceptionExtensions.Unwrap(System.Exception)"/> covering plain
    /// exceptions, <see cref="AggregateException"/>, <see cref="TargetInvocationException"/>,
    /// and recursive unwrap scenarios.
    /// </summary>
    public class ExceptionExtensionsTests
    {
        [Fact]
        [DisplayName("Unwrap returns the exception itself for a plain exception")]
        public void Unwrap_PlainException_ReturnsSelf()
        {
            var ex = new InvalidOperationException("plain");
            Assert.Same(ex, ex.Unwrap());
        }

        [Fact]
        [DisplayName("Unwrap returns the first inner exception of an AggregateException")]
        public void Unwrap_AggregateException_ReturnsFirstInner()
        {
            var inner1 = new InvalidOperationException("inner1");
            var inner2 = new NotSupportedException("inner2");
            var agg = new AggregateException(inner1, inner2);

            var result = agg.Unwrap();

            Assert.Same(inner1, result);
        }

        [Fact]
        [DisplayName("Unwrap returns the inner exception of a TargetInvocationException")]
        public void Unwrap_TargetInvocation_ReturnsInner()
        {
            var inner = new InvalidOperationException("inner");
            var wrapper = new TargetInvocationException(inner);

            var result = wrapper.Unwrap();

            Assert.Same(inner, result);
        }

        [Fact]
        [DisplayName("Unwrap recursively unwraps nested wrappers")]
        public void Unwrap_NestedWrappers_UnwrapsFully()
        {
            var core = new InvalidOperationException("core");
            var target = new TargetInvocationException(core);
            var agg = new AggregateException(target);

            var result = agg.Unwrap();

            Assert.Same(core, result);
        }

        [Fact]
        [DisplayName("Unwrap throws ArgumentNullException for null")]
        public void Unwrap_NullException_Throws()
        {
            Exception? nullEx = null;
            Assert.Throws<ArgumentNullException>(() => nullEx!.Unwrap());
        }
    }
}
