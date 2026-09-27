using System.ComponentModel;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Covers the assembly-qualified name screen that guards every wire path resolving a type
    /// from a caller-supplied name.
    /// </summary>
    /// <remarks>
    /// The shape under test is generic-argument smuggling: an allowed outer type carrying a
    /// disallowed argument. A screen that splits on the first comma accepts it, because the
    /// argument's comma sits inside <c>[[...]]</c> and therefore comes first.
    /// </remarks>
    public class WireTypeWhitelistTests
    {
        [Theory]
        [InlineData("System.String")]
        [InlineData("System.Int32")]
        [InlineData("System.Byte[]")]
        [InlineData("System.Object[]")]
        [InlineData("System.Data.DataTable")]
        [InlineData("Polhem.Definition.Collections.ParameterCollection, Polhem.Definition")]
        [InlineData("Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core")]
        [InlineData("Polhem.Base.Something, Polhem.Base, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
        [DisplayName("IsAssemblyQualifiedNameAllowed accepts names on the whitelist")]
        public void IsAssemblyQualifiedNameAllowed_AllowedName_ReturnsTrue(string name)
        {
            Assert.True(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Theory]
        [InlineData("Evil.Namespace.Exploit, Evil.Assembly")]
        [InlineData("System.Diagnostics.Process, System.Diagnostics.Process")]
        [DisplayName("IsAssemblyQualifiedNameAllowed rejects names not on the whitelist")]
        public void IsAssemblyQualifiedNameAllowed_DisallowedName_ReturnsFalse(string name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Theory]
        // An allowed type name paired with an assembly outside the allowed set: Type.GetType loads the
        // assembly before it looks for the type, so the name alone is not enough.
        [InlineData("Polhem.Api.Core.Messages.System.LoginRequest, Evil.Assembly")]
        [InlineData("System.Int32, Evil.Assembly")]
        // The same, inside a generic argument.
        [InlineData("Polhem.Base.Holder`1[[Polhem.Base.Ok, Evil.Assembly]], Polhem.Base")]
        // A prefix that only looks like an allowed namespace.
        [InlineData("Polhem.Base.Ok, Polhem.BaseEvil")]
        // Attributes that name a location rather than a version, and characters that make a path.
        [InlineData("Polhem.Base.Ok, Polhem.Base, CodeBase=file:///tmp/evil.dll")]
        [InlineData("Polhem.Base.Ok, ../Polhem.Base")]
        [InlineData("Polhem.Base.Ok, Polhem.Base, Version=")]
        [InlineData("Polhem.Base.Ok, ")]
        [DisplayName("IsAssemblyQualifiedNameAllowed rejects an allowed type name paired with an assembly name outside the allowed set")]
        public void IsAssemblyQualifiedNameAllowed_DisallowedAssembly_ReturnsFalse(string name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Theory]
        [InlineData("System.Int32, System.Private.CoreLib")]
        [InlineData("System.Int32, mscorlib")]
        [InlineData("System.Data.DataTable, System.Data.Common")]
        [InlineData("Polhem.Api.Core.UnitTests.Something, Polhem.Api.Core.UnitTests")]
        [InlineData("Polhem.Base.Holder`1[[System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]], Polhem.Base, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
        [DisplayName("IsAssemblyQualifiedNameAllowed accepts the runtime assemblies and assemblies named after an allowed namespace, with version attributes")]
        public void IsAssemblyQualifiedNameAllowed_AllowedAssembly_ReturnsTrue(string name)
        {
            Assert.True(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Fact]
        [DisplayName("IsAssemblyQualifiedNameAllowed accepts the assembly-qualified name the runtime writes for a whitelisted generic")]
        public void IsAssemblyQualifiedNameAllowed_RuntimeWrittenName_ReturnsTrue()
        {
            // What the escape hatch of the wire value formatters actually writes.
            var name = typeof(Polhem.Base.Collections.KeyCollectionBase<Polhem.Definition.Collections.Parameter>).AssemblyQualifiedName;

            Assert.True(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Theory]
        // The regression this screen exists for: allowed outer type, disallowed generic argument.
        [InlineData("Polhem.Base.Collections.Dictionary`1[[System.Diagnostics.Process, System.Diagnostics.Process]], Polhem.Base")]
        [InlineData("Polhem.Definition.Wrapper`1[[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Definition")]
        // Two allowed arguments plus one disallowed.
        [InlineData("Polhem.Base.Pair`2[[System.String],[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Base")]
        // Disallowed type buried one level deeper.
        [InlineData("Polhem.Base.A`1[[Polhem.Base.B`1[[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Base]], Polhem.Base")]
        // Array forms.
        [InlineData("Evil.Namespace.Exploit[], Evil.Assembly")]
        [InlineData("Polhem.Base.Holder`1[[Evil.Namespace.Exploit[], Evil.Assembly]], Polhem.Base")]
        [DisplayName("IsAssemblyQualifiedNameAllowed rejects types smuggled in as generic arguments or array elements")]
        public void IsAssemblyQualifiedNameAllowed_SmuggledArgument_ReturnsFalse(string name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        // Unbalanced brackets, empty segments and stray separators must fail closed rather than
        // be handed to `Type.GetType`.
        [InlineData("Polhem.Base.Broken`1[[Polhem.Base.Ok, Polhem.Base], Polhem.Base")]
        [InlineData("Polhem.Base.Broken`1[[]], Polhem.Base")]
        [InlineData("Polhem.Base.Broken`1[, Polhem.Base")]
        [InlineData("[[Polhem.Base.Ok, Polhem.Base]]")]
        [InlineData(", Polhem.Base")]
        // Pointer and by-ref forms never appear on this wire.
        [InlineData("Polhem.Base.Ok*, Polhem.Base")]
        [InlineData("Polhem.Base.Ok&, Polhem.Base")]
        [DisplayName("IsAssemblyQualifiedNameAllowed fails closed for empty and malformed names")]
        public void IsAssemblyQualifiedNameAllowed_MalformedName_ReturnsFalse(string? name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Fact]
        [DisplayName("IsAssemblyQualifiedNameAllowed rejects overlong names")]
        public void IsAssemblyQualifiedNameAllowed_OverlongName_ReturnsFalse()
        {
            var name = "Polhem.Base." + new string('a', 2000);

            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Fact]
        [DisplayName("IsAssemblyQualifiedNameAllowed rejects names nested too deeply")]
        public void IsAssemblyQualifiedNameAllowed_ExcessiveNesting_ReturnsFalse()
        {
            // Ten levels of nesting, every named type allowed. Depth alone must reject it, so a
            // crafted payload cannot drive unbounded recursion.
            var name = "Polhem.Base.Ok, Polhem.Base";
            for (var i = 0; i < 10; i++)
            {
                name = $"Polhem.Base.Wrap`1[[{name}]], Polhem.Base";
            }

            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Fact]
        [DisplayName("IsRuntimeTypeAllowed rejects a constructed generic type with a disallowed generic argument")]
        public void IsRuntimeTypeAllowed_ConstructedGenericWithDisallowedArgument_ReturnsFalse()
        {
            // `LanguageResourceStringLocalizer<T>` is an allowed outer type (an unconstrained generic in an allowed namespace); the argument is not.
            var type = typeof(Polhem.Definition.Language.LanguageResourceStringLocalizer<>)
                .MakeGenericType(typeof(global::System.Text.StringBuilder));

            Assert.False(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Fact]
        [DisplayName("IsRuntimeTypeAllowed accepts a constructed generic type whose generic argument is also on the whitelist")]
        public void IsRuntimeTypeAllowed_ConstructedGenericWithAllowedArgument_ReturnsTrue()
        {
            var type = typeof(Polhem.Definition.Language.LanguageResourceStringLocalizer<>).MakeGenericType(typeof(string));

            Assert.True(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Theory]
        [InlineData(typeof(byte[]))]
        [InlineData(typeof(object[]))]
        [InlineData(typeof(string))]
        [InlineData(typeof(global::System.Data.DataTable))]
        [DisplayName("IsRuntimeTypeAllowed accepts types on the whitelist")]
        public void IsRuntimeTypeAllowed_AllowedType_ReturnsTrue(Type type)
        {
            Assert.True(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Theory]
        [InlineData(typeof(global::System.Text.StringBuilder))]
        [InlineData(typeof(global::System.Text.StringBuilder[]))]
        [DisplayName("IsRuntimeTypeAllowed rejects types not on the whitelist and their arrays")]
        public void IsRuntimeTypeAllowed_DisallowedType_ReturnsFalse(Type type)
        {
            Assert.False(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }
    }
}
