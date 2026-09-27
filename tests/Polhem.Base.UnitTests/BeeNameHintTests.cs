using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    public class BeeNameHintTests
    {
        [Theory]
        [InlineData("Bee.Business.AuditLog.LogBusinessObject, Bee.Business")]
        [InlineData("Bee.Business.dll")]
        [InlineData("MyApp.Orders.OrderBo, Bee.Business")]
        [InlineData("Polhem.Base.KeyCollectionBase`1[[Bee.Definition.Forms.FormField, Bee.Definition]], Polhem.Base")]
        [DisplayName("Matches recognizes a Bee.* type, assembly or generic argument")]
        public void Matches_BeeName_ReturnsTrue(string name)
        {
            Assert.True(BeeNameHint.Matches(name));
        }

        [Theory]
        [InlineData("Polhem.Business.AuditLog.LogBusinessObject, Polhem.Business")]
        [InlineData("MyApp.Beekeeping.HiveBo, MyApp")]
        [InlineData("Beetle.Types.X, Beetle")]
        [InlineData("")]
        [InlineData(null)]
        [DisplayName("Matches ignores names that are not Bee.NET names")]
        public void Matches_OtherName_ReturnsFalse(string? name)
        {
            Assert.False(BeeNameHint.Matches(name));
        }

        [Fact]
        [DisplayName("AppendTo adds the rename hint for a Bee.NET name and names the README section")]
        public void AppendTo_BeeName_AddsHint()
        {
            string message = BeeNameHint.AppendTo("Type not found.", "Bee.Business.X, Bee.Business");

            Assert.StartsWith("Type not found.", message, StringComparison.Ordinal);
            Assert.Contains("Bee.NET", message, StringComparison.Ordinal);
            Assert.Contains("Polhem.*", message, StringComparison.Ordinal);
            Assert.Contains("Migrating from Bee.NET", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("AppendTo leaves the message unchanged for any other name")]
        public void AppendTo_OtherName_ReturnsMessage()
        {
            Assert.Equal("Type not found.", BeeNameHint.AppendTo("Type not found.", "MyApp.X, MyApp"));
        }

        [Fact]
        [DisplayName("LoadAssembly for a missing Bee.* assembly says it looks like a Bee.NET name")]
        public void LoadAssembly_MissingBeeAssembly_MessageHasHint()
        {
            var ex = Assert.Throws<FileNotFoundException>(() => AssemblyLoader.LoadAssembly("Bee.NoSuchAssemblyXyz.dll"));

            Assert.Contains("Bee.NET", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("LoadAssembly for any other missing assembly carries no Bee.NET hint")]
        public void LoadAssembly_MissingOtherAssembly_MessageHasNoHint()
        {
            var ex = Assert.Throws<FileNotFoundException>(() => AssemblyLoader.LoadAssembly("Nowhere.NoSuchAssemblyXyz.dll"));

            Assert.DoesNotContain("Bee.NET", ex.Message, StringComparison.Ordinal);
        }
    }
}
