using System.ComponentModel;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the base behavior of <see cref="BusinessObject"/>.
    /// </summary>
    public class BusinessObjectTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public BusinessObjectTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("The constructor sets AccessToken and IsLocalCall")]
        public void Constructor_SetsProperties()
        {
            var token = Guid.NewGuid();
            var bo = new TestableBusinessObject(TestBusinessObjectContext.Create(_fx), token, isLocalCall: false);

            Assert.Equal(token, bo.AccessToken);
            Assert.False(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("The TestableBusinessObject fake defaults IsLocalCall to true")]
        public void Constructor_DefaultIsLocalCall_IsTrue()
        {
            var bo = new TestableBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid());
            Assert.True(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("ExecFunc delegates to the DoExecFunc override")]
        public void ExecFunc_DelegatesToDoExecFunc()
        {
            var bo = new TestableBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid());
            var args = new ExecFuncArgs("Hello");

            var result = bo.ExecFunc(args);

            Assert.Equal(1, bo.ExecFuncCallCount);
            Assert.Equal(0, bo.ExecFuncAnonymousCallCount);
            Assert.Same(args, bo.LastArgs);
            Assert.Equal("DoExecFunc", result.Parameters.GetValue<string>("Marker"));
        }

        [Fact]
        [DisplayName("ExecFuncAnonymous delegates to the DoExecFuncAnonymous override")]
        public void ExecFuncAnonymous_DelegatesToDoExecFuncAnonymous()
        {
            var bo = new TestableBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid());
            var args = new ExecFuncArgs("Hi");

            var result = bo.ExecFuncAnonymous(args);

            Assert.Equal(0, bo.ExecFuncCallCount);
            Assert.Equal(1, bo.ExecFuncAnonymousCallCount);
            Assert.Same(args, bo.LastArgs);
            Assert.Equal("DoExecFuncAnonymous", result.Parameters.GetValue<string>("Marker"));
        }

        [Fact]
        [DisplayName("ExecFunc returns an empty result without throwing when DoExecFunc is not overridden")]
        public void ExecFunc_WithoutOverride_ReturnsEmptyResult()
        {
            var bo = new BareBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid());

            var result = bo.ExecFunc(new ExecFuncArgs("Anything"));

            Assert.NotNull(result);
            Assert.Empty(result.Parameters);
        }

        [Fact]
        [DisplayName("ExecFuncAnonymous returns an empty result without throwing when DoExecFuncAnonymous is not overridden")]
        public void ExecFuncAnonymous_WithoutOverride_ReturnsEmptyResult()
        {
            var bo = new BareBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid());

            var result = bo.ExecFuncAnonymous(new ExecFuncArgs("Anything"));

            Assert.NotNull(result);
            Assert.Empty(result.Parameters);
        }
    }
}
