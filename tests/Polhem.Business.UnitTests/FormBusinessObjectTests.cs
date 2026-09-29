using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Tests.Shared;
using Polhem.Core.Exceptions;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="FormBusinessObject"/>, verifying reflection dispatch through the internal <c>FormExecFuncHandler.Hello</c>.
    /// </summary>
    public class FormBusinessObjectTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectTests(SharedDbFixture fx) { _fx = fx; }
        [Fact]
        [DisplayName("The constructor sets AccessToken, ProgId and IsLocalCall")]
        public void Constructor_SetsProperties()
        {
            var token = Guid.NewGuid();

            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), token, "prog01", isLocalCall: false);

            Assert.Equal(token, bo.AccessToken);
            Assert.Equal("prog01", bo.ProgId);
            Assert.False(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("ExecFunc Hello fills in the default message")]
        public void ExecFunc_Hello_FillsExpectedMessage()
        {
            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(), "prog01");

            var result = bo.ExecFunc(new ExecFuncArgs("Hello"));

            Assert.Equal("Hello form-level BusinessObject", result.Parameters.GetValue<string>("Hello"));
        }

        [Fact]
        [DisplayName("ExecFuncAnonymous calling a method that requires authentication throws AuthenticationRequiredException")]
        public void ExecFuncAnonymous_HelloRequiresAuthentication_ThrowsUnauthorized()
        {
            // `FormExecFuncHandler.Hello` is marked Authenticated, so `InvokeExecFunc` must block an anonymous call.
            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(), "prog01");

            Assert.Throws<AuthenticationRequiredException>(() =>
                bo.ExecFuncAnonymous(new ExecFuncArgs("Hello")));
        }

        [Fact]
        [DisplayName("ExecFunc calling a method that does not exist throws MissingMethodException")]
        public void ExecFunc_UnknownMethod_ThrowsMissingMethod()
        {
            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(), "prog01");

            Assert.Throws<MissingMethodException>(() =>
                bo.ExecFunc(new ExecFuncArgs("DoesNotExist")));
        }
    }
}
