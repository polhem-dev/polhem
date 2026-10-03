using System.ComponentModel;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.Messages.System;

namespace Polhem.Api.Core.UnitTests.Payload
{
    /// <summary>
    /// The <c>type</c> member of a payload names a type the client resolves; <see cref="PolhemPayloadTypeResolver"/>
    /// resolves only names that pass the wire type allow-list, and never loads one that does not.
    /// </summary>
    public class PolhemPayloadTypeResolverTests
    {
        [Fact]
        [DisplayName("A framework message type is named FullName, AssemblyName and resolves back to itself")]
        public void GetTypeName_FrameworkMessage_RoundTrips()
        {
            var resolver = PolhemPayloadTypeResolver.Instance;

            var name = resolver.GetTypeName(typeof(LoginRequest));

            Assert.Equal("Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core", name);
            Assert.True(resolver.TryResolveType(name, out var type));
            Assert.Equal(typeof(LoginRequest), type);
            Assert.True(resolver.IsNameOf(name, typeof(LoginRequest)));
        }

        [Theory]
        [InlineData("System.Diagnostics.Process, System.Diagnostics.Process")]
        [InlineData("System.IO.FileInfo, System.IO.FileSystem")]
        [InlineData("Evil.Namespace.Exploit, Evil.Assembly")]
        [InlineData("System.Runtime.Serialization.Formatters.Binary.BinaryFormatter, mscorlib")]
        // A disallowed type smuggled in as a generic argument of an allowed outer type. The argument's own comma comes
        // before the assembly separator, so splitting on the first comma yields a fragment that still starts with
        // `Polhem.Core.` and the argument goes unscreened.
        [InlineData("Polhem.Core.Collections.Dictionary`1[[System.Diagnostics.Process, System.Diagnostics.Process]], Polhem.Core")]
        [InlineData("Polhem.Definition.Something`1[[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Definition")]
        // Nested one level deeper: the outer two types are allowed, the innermost is not.
        [InlineData("Polhem.Core.A`1[[Polhem.Core.B`1[[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Core]], Polhem.Core")]
        // An array of a disallowed element type.
        [InlineData("Evil.Namespace.Exploit[], Evil.Assembly")]
        // Malformed names must fail closed rather than fall through to `Type.GetType`.
        [InlineData("Polhem.Core.Broken`1[[Evil.Namespace.Exploit, Evil.Assembly], Polhem.Core")]
        [DisplayName("A name outside the allow list is neither resolved nor accepted as the name of a type")]
        public void DisallowedName_IsRefused(string typeName)
        {
            var resolver = PolhemPayloadTypeResolver.Instance;

            Assert.False(resolver.TryResolveType(typeName, out var type));
            Assert.Null(type);
            Assert.False(resolver.IsNameOf(typeName, typeof(LoginRequest)));
        }

        [Fact]
        [DisplayName("The name of an allowed type does not name another allowed type")]
        public void IsNameOf_OtherType_ReturnsFalse()
        {
            var resolver = PolhemPayloadTypeResolver.Instance;

            Assert.False(resolver.IsNameOf(resolver.GetTypeName(typeof(PingRequest)), typeof(LoginRequest)));
        }
    }
}
