using System.ComponentModel;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;

namespace Polhem.Definition.UnitTests.Attributes
{
    /// <summary>
    /// Tests for the ApiAccessControlAttribute constructor and properties.
    /// </summary>
    public class ApiAccessControlAttributeTests
    {
        [Theory]
        [InlineData(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
        [InlineData(ApiProtectionLevel.Encoded, ApiAccessRequirement.Authenticated)]
        [InlineData(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated)]
        [DisplayName("Constructor stores ProtectionLevel and AccessRequirement as given")]
        public void Constructor_SetsProperties(ApiProtectionLevel level, ApiAccessRequirement requirement)
        {
            // Act
            var attr = new ApiAccessControlAttribute(level, requirement);

            // Assert
            Assert.Equal(level, attr.ProtectionLevel);
            Assert.Equal(requirement, attr.AccessRequirement);
        }

        [Fact]
        [DisplayName("Constructor defaults AccessRequirement to Authenticated when it is omitted")]
        public void Constructor_DefaultAccessRequirement_IsAuthenticated()
        {
            // Act
            var attr = new ApiAccessControlAttribute(ApiProtectionLevel.Encrypted);

            // Assert
            Assert.Equal(ApiAccessRequirement.Authenticated, attr.AccessRequirement);
        }
    }
}
