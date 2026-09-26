using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Verifies the scope of <see cref="ProtectedFields"/>. It is a defense against privilege escalation: too broad and it
    /// blocks ordinary fields, too narrow and a maintenance form built by a deployment can write privileged columns.
    /// </summary>
    public class ProtectedFieldsTests
    {
        [Fact]
        [DisplayName("st_user.deployment_admin is a protected column")]
        public void IsProtected_DeploymentAdmin_ReturnsTrue()
        {
            Assert.True(ProtectedFields.IsProtected("st_user", ProtectedFields.DeploymentAdmin));
        }

        /// <summary>
        /// <c>st_user.password</c> is a protected column.
        /// </summary>
        /// <remarks>
        /// Before 2026-09-04 this was written the other way round (`password` was listed among the unprotected cases), which
        /// wrote a privilege escalation path into the specification: a deployment only had to build a user maintenance form on
        /// <c>st_user</c> for the FormSchema data path to write this column, changing someone else's password to a value the
        /// verifier accepts unconditionally.
        /// </remarks>
        [Fact]
        [DisplayName("st_user.password is a protected column")]
        public void IsProtected_Password_ReturnsTrue()
        {
            Assert.True(ProtectedFields.IsProtected("st_user", ProtectedFields.Password));
        }

        [Fact]
        [DisplayName("The protected column check ignores case (identifier comparison)")]
        public void IsProtected_IgnoresCase()
        {
            Assert.True(ProtectedFields.IsProtected("ST_USER", "Deployment_Admin"));
        }

        [Theory]
        [InlineData("st_user", "sys_id")]
        [InlineData("st_user", "sys_name")]
        [InlineData("ft_order", "deployment_admin")]
        [InlineData("ft_order", "password")]
        [DisplayName("Other columns, and columns with the same name in other tables, are not protected")]
        public void IsProtected_OtherColumns_ReturnFalse(string tableName, string fieldName)
        {
            Assert.False(ProtectedFields.IsProtected(tableName, fieldName));
        }

        [Theory]
        [InlineData("", "deployment_admin")]
        [InlineData("st_user", "")]
        [DisplayName("A blank table or column name returns false instead of throwing")]
        public void IsProtected_BlankInput_ReturnsFalse(string tableName, string fieldName)
        {
            Assert.False(ProtectedFields.IsProtected(tableName, fieldName));
        }
    }
}
