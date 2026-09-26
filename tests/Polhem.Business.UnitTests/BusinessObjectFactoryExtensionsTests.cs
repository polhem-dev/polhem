using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the <see cref="BusinessObjectFactoryExtensions"/> extension methods,
    /// verifying that the typed wrappers return the interface directly so callers do not repeat the cast.
    /// </summary>
    public class BusinessObjectFactoryExtensionsTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public BusinessObjectFactoryExtensionsTests(PolhemTestFixture fx) { _fx = fx; }

        private IBusinessObjectFactory Factory => _fx.GetRequiredService<IBusinessObjectFactory>();

        [Fact]
        [DisplayName("CreateFormBO returns an IFormBusinessObject instance")]
        public void CreateFormBO_ReturnsFormBusinessObjectInterface()
        {
            var token = TestSessionFactory.CreateAccessToken(_fx);

            IFormBusinessObject bo = Factory.CreateFormBO(token, "prog01", isLocalCall: true);

            Assert.NotNull(bo);
            Assert.IsType<FormBusinessObject>(bo);
        }

        [Fact]
        [DisplayName("CreateFormBO keeps isLocalCall=false")]
        public void CreateFormBO_WithIsLocalCallFalse_PreservesFlag()
        {
            var bo = (FormBusinessObject)Factory.CreateFormBO(TestSessionFactory.CreateAccessToken(_fx), "prog01", isLocalCall: false);

            Assert.False(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("CreateSystemBO returns an ISystemBusinessObject instance")]
        public void CreateSystemBO_ReturnsSystemBusinessObjectInterface()
        {
            var token = TestSessionFactory.CreateAccessToken(_fx);

            ISystemBusinessObject bo = Factory.CreateSystemBO(token, isLocalCall: true);

            Assert.NotNull(bo);
            Assert.IsType<SystemBusinessObject>(bo);
        }

        [Fact]
        [DisplayName("CreateSystemBO keeps isLocalCall=false")]
        public void CreateSystemBO_WithIsLocalCallFalse_PreservesFlag()
        {
            var bo = (SystemBusinessObject)Factory.CreateSystemBO(TestSessionFactory.CreateAccessToken(_fx), isLocalCall: false);

            Assert.False(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("CreateFormBO throws ArgumentNullException for a null factory")]
        public void CreateFormBO_NullFactory_Throws()
        {
            IBusinessObjectFactory? factory = null;
            Assert.Throws<ArgumentNullException>(() => factory!.CreateFormBO(Guid.NewGuid(), "prog01", isLocalCall: true));
        }

        [Fact]
        [DisplayName("CreateSystemBO throws ArgumentNullException for a null factory")]
        public void CreateSystemBO_NullFactory_Throws()
        {
            IBusinessObjectFactory? factory = null;
            Assert.Throws<ArgumentNullException>(() => factory!.CreateSystemBO(Guid.NewGuid(), isLocalCall: true));
        }
    }
}
