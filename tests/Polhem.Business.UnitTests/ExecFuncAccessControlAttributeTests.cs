using System.ComponentModel;
using Polhem.Business.Attributes;
using Polhem.Definition.Security;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Constructor and property tests for <see cref="ExecFuncAccessControlAttribute"/>.
    /// </summary>
    public class ExecFuncAccessControlAttributeTests
    {
        [Fact]
        [DisplayName("The default constructor sets AccessRequirement to Authenticated")]
        public void DefaultConstructor_ReturnsAuthenticated()
        {
            var attr = new ExecFuncAccessControlAttribute();
            Assert.Equal(ApiAccessRequirement.Authenticated, attr.AccessRequirement);
        }

        [Theory]
        [InlineData(ApiAccessRequirement.Anonymous)]
        [InlineData(ApiAccessRequirement.Authenticated)]
        [DisplayName("The constructor keeps the AccessRequirement passed in")]
        public void Constructor_PreservesAccessRequirement(ApiAccessRequirement requirement)
        {
            var attr = new ExecFuncAccessControlAttribute(requirement);
            Assert.Equal(requirement, attr.AccessRequirement);
        }
    }
}
