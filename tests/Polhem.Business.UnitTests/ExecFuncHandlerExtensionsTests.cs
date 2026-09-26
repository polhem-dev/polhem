using System.ComponentModel;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Security;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the InvokeExecFunc overloads of <see cref="ExecFuncHandlerExtensions"/>.
    /// </summary>
    public class ExecFuncHandlerExtensionsTests
    {
        [Fact]
        [DisplayName("InvokeExecFunc calling a method that does not exist throws MissingMethodException")]
        public void InvokeExecFunc_MethodNotFound_ThrowsMissingMethodException()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs("DoesNotExist");
            var result = new ExecFuncResult();

            Assert.Throws<MissingMethodException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, args, result));
        }

        [Fact]
        [DisplayName("InvokeExecFunc calling an authenticated method anonymously throws UnauthorizedAccessException")]
        public void InvokeExecFunc_AnonymousCallsAuthenticated_ThrowsUnauthorized()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.Authenticated));
            var result = new ExecFuncResult();

            Assert.Throws<UnauthorizedAccessException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Anonymous, args, result));
        }

        [Fact]
        [DisplayName("InvokeExecFunc calling an anonymous method anonymously succeeds and fills the result")]
        public void InvokeExecFunc_AnonymousCallsAnonymous_Succeeds()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.Anonymous));
            var result = new ExecFuncResult();

            handler.InvokeExecFunc(ApiAccessRequirement.Anonymous, args, result);

            Assert.Equal("Anonymous", result.Parameters.GetValue<string>("Called"));
            Assert.Equal(nameof(FakeExecFuncHandler.Anonymous), result.Parameters.GetValue<string>("FuncId"));
        }

        [Fact]
        [DisplayName("InvokeExecFunc calling an authenticated method as an authenticated caller succeeds")]
        public void InvokeExecFunc_AuthenticatedCallsAuthenticated_Succeeds()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.Authenticated));
            var result = new ExecFuncResult();

            handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, args, result);

            Assert.Equal("Authenticated", result.Parameters.GetValue<string>("Called"));
        }

        [Fact]
        [DisplayName("InvokeExecFunc calling an anonymous method as an authenticated caller succeeds (sufficient permission)")]
        public void InvokeExecFunc_AuthenticatedCallsAnonymous_Succeeds()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.Anonymous));
            var result = new ExecFuncResult();

            handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, args, result);

            Assert.Equal("Anonymous", result.Parameters.GetValue<string>("Called"));
        }

        [Fact]
        [DisplayName("InvokeExecFunc rejects an anonymous call to a method without the attribute")]
        public void InvokeExecFunc_NoAttributeAnonymous_ThrowsUnauthorized()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.NoAttribute));
            var result = new ExecFuncResult();

            Assert.Throws<UnauthorizedAccessException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Anonymous, args, result));
        }

        [Fact]
        [DisplayName("InvokeExecFunc rejects a method without the attribute even for an authenticated caller (fail-closed)")]
        public void InvokeExecFunc_NoAttributeAuthenticated_ThrowsUnauthorized()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.NoAttribute));
            var result = new ExecFuncResult();

            var ex = Assert.Throws<UnauthorizedAccessException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, args, result));
            Assert.Contains("does not declare", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("InvokeExecFunc rejects a remote call to a LocalOnly method")]
        public void InvokeExecFunc_LocalOnlyRemoteCall_ThrowsUnauthorized()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.LocalOnly));
            var result = new ExecFuncResult();

            var ex = Assert.Throws<UnauthorizedAccessException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, isLocalCall: false, args, result));
            Assert.Contains("local calls only", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("InvokeExecFunc allows a local call to a LocalOnly method")]
        public void InvokeExecFunc_LocalOnlyLocalCall_Succeeds()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.LocalOnly));
            var result = new ExecFuncResult();

            handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, isLocalCall: true, args, result);

            Assert.Equal("LocalOnly", result.Parameters.GetValue<string>("Called"));
        }

        [Fact]
        [DisplayName("The legacy InvokeExecFunc overload treats the call as remote and rejects a LocalOnly method")]
        public void InvokeExecFunc_LegacyOverload_TreatsCallAsRemote()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.LocalOnly));
            var result = new ExecFuncResult();

            Assert.Throws<UnauthorizedAccessException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, args, result));
        }

        [Fact]
        [DisplayName("InvokeExecFunc unwraps an exception thrown by the target method and keeps the original type")]
        public void InvokeExecFunc_TargetThrows_UnwrapsToOriginalException()
        {
            var handler = new FakeExecFuncHandler();
            var args = new ExecFuncArgs(nameof(FakeExecFuncHandler.Throws));
            var result = new ExecFuncResult();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                handler.InvokeExecFunc(ApiAccessRequirement.Anonymous, args, result));
            Assert.Equal("fake-inner-exception", ex.Message);
        }
    }
}
