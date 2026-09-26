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
        [DisplayName("IsAssemblyQualifiedNameAllowed 應接受白名單內的名稱")]
        public void IsAssemblyQualifiedNameAllowed_AllowedName_ReturnsTrue(string name)
        {
            Assert.True(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Theory]
        [InlineData("Evil.Namespace.Exploit, Evil.Assembly")]
        [InlineData("System.Diagnostics.Process, System.Diagnostics.Process")]
        [DisplayName("IsAssemblyQualifiedNameAllowed 應拒絕白名單外的名稱")]
        public void IsAssemblyQualifiedNameAllowed_DisallowedName_ReturnsFalse(string name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
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
        [DisplayName("IsAssemblyQualifiedNameAllowed 應拒絕夾帶在泛型參數或陣列元素中的型別")]
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
        [DisplayName("IsAssemblyQualifiedNameAllowed 對空值與畸形名稱應 fail-closed")]
        public void IsAssemblyQualifiedNameAllowed_MalformedName_ReturnsFalse(string? name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Fact]
        [DisplayName("IsAssemblyQualifiedNameAllowed 應拒絕過長的名稱")]
        public void IsAssemblyQualifiedNameAllowed_OverlongName_ReturnsFalse()
        {
            var name = "Polhem.Base." + new string('a', 2000);

            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        [Fact]
        [DisplayName("IsAssemblyQualifiedNameAllowed 應拒絕巢狀過深的名稱")]
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
        [DisplayName("IsRuntimeTypeAllowed 應拒絕帶有不允許泛型參數的具現型別")]
        public void IsRuntimeTypeAllowed_ConstructedGenericWithDisallowedArgument_ReturnsFalse()
        {
            // `Polhem.Base.Collections.Dictionary<T>` is an allowed outer type; the argument is not.
            var type = typeof(Polhem.Base.Collections.Dictionary<>)
                .MakeGenericType(typeof(global::System.Text.StringBuilder));

            Assert.False(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Fact]
        [DisplayName("IsRuntimeTypeAllowed 應接受泛型參數也在白名單內的具現型別")]
        public void IsRuntimeTypeAllowed_ConstructedGenericWithAllowedArgument_ReturnsTrue()
        {
            var type = typeof(Polhem.Base.Collections.Dictionary<>).MakeGenericType(typeof(string));

            Assert.True(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Theory]
        [InlineData(typeof(byte[]))]
        [InlineData(typeof(object[]))]
        [InlineData(typeof(string))]
        [InlineData(typeof(global::System.Data.DataTable))]
        [DisplayName("IsRuntimeTypeAllowed 應接受白名單內的型別")]
        public void IsRuntimeTypeAllowed_AllowedType_ReturnsTrue(Type type)
        {
            Assert.True(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Theory]
        [InlineData(typeof(global::System.Text.StringBuilder))]
        [InlineData(typeof(global::System.Text.StringBuilder[]))]
        [DisplayName("IsRuntimeTypeAllowed 應拒絕白名單外的型別與其陣列")]
        public void IsRuntimeTypeAllowed_DisallowedType_ReturnsFalse(Type type)
        {
            Assert.False(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }
    }
}
