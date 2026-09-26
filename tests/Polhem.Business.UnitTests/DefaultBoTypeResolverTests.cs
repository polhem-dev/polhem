using System.ComponentModel;
using Polhem.Business.Form;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Additional coverage for <see cref="DefaultBoTypeResolver"/>.
    /// This resolver is a minimal implementation that returns <see cref="FormBusinessObject"/> for any progId.
    /// </summary>
    public class DefaultBoTypeResolverTests
    {
        [Fact]
        [DisplayName("DefaultBoTypeResolver.Resolve returns the FormBusinessObject type for any progId")]
        public void Resolve_AnyProgId_ReturnsFormBusinessObjectType()
        {
            var resolver = new DefaultBoTypeResolver();

            Assert.Equal(typeof(FormBusinessObject), resolver.Resolve("AnyProgId"));
        }

        [Fact]
        [DisplayName("DefaultBoTypeResolver.Resolve returns the FormBusinessObject type for an empty progId")]
        public void Resolve_EmptyProgId_ReturnsFormBusinessObjectType()
        {
            var resolver = new DefaultBoTypeResolver();

            Assert.Equal(typeof(FormBusinessObject), resolver.Resolve(string.Empty));
        }

        [Fact]
        [DisplayName("The default IBoTypeResolver.Resolve(customizeId, progId) delegates to Resolve(progId) and ignores customizeId")]
        public void Resolve_WithCustomizeIdAndProgId_DefaultDelegatesToBaseResolve()
        {
            IBoTypeResolver resolver = new DefaultBoTypeResolver();

            var result = resolver.Resolve("acme", "AnyProgId");

            Assert.Equal(typeof(FormBusinessObject), result);
        }
    }
}
