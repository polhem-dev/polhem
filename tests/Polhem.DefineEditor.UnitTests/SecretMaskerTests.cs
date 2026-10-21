using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Hiding password values in the command line and output DefineEditor shows.
    /// </summary>
    public class SecretMaskerTests
    {
        [Theory]
        [DisplayName("The value of every password key is hidden, whatever its spelling")]
        [InlineData("Server=db;User Id=sa;Password=Secret1!;Encrypt=false", "Server=db;User Id=sa;Password=********;Encrypt=false")]
        [InlineData("Host=db;PWD=Secret1!", "Host=db;PWD=********")]
        [InlineData("Data Source=x;User Password=Secret1!", "Data Source=x;User Password=********")]
        [InlineData("Failed: login failed, password = Secret1!", "Failed: login failed, password = ********")]
        [InlineData("Password=\"a;b c\";Server=db", "Password=********;Server=db")]
        [InlineData("--add-source https://feed:Secret1!@pkgs.example.com/v3/index.json", "--add-source https://feed:********@pkgs.example.com/v3/index.json")]
        public void Hide_PasswordValue_IsMasked(string text, string expected)
        {
            Assert.Equal(expected, SecretMasker.Hide(text));
            Assert.DoesNotContain("Secret1!", SecretMasker.Hide(text));
        }

        [Theory]
        [DisplayName("Text without a password value is left as it is")]
        [InlineData("  company.ft_customer: created")]
        [InlineData("db seed-admin reads the password from POLHEM_ADMIN_PASSWORD")]
        [InlineData("Password=;Server=db")]
        public void Hide_NoPasswordValue_Unchanged(string text)
        {
            Assert.Equal(text, SecretMasker.Hide(text));
        }
    }
}
