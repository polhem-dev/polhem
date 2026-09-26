using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for the factory methods of <see cref="BusinessObjectFactory"/>.
    /// The DI-injected factory instance is resolved through the per-class <see cref="SharedDbFixture"/>.
    /// </summary>
    public class BusinessObjectFactoryTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public BusinessObjectFactoryTests(PolhemTestFixture fx) { _fx = fx; }
        private IBusinessObjectFactory Factory => _fx.GetRequiredService<IBusinessObjectFactory>();

        [Fact]
        [DisplayName("CreateBusinessObject returns a SystemBusinessObject and keeps the AccessToken")]
        public void CreateBusinessObject_System_ReturnsSystemBusinessObject()
        {
            var token = TestSessionFactory.CreateAccessToken(_fx);

            var obj = Factory.CreateBusinessObject(token, SysProgIds.System, isLocalCall: true);

            var bo = Assert.IsType<SystemBusinessObject>(obj);
            Assert.Equal(token, bo.AccessToken);
            Assert.True(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("CreateBusinessObject keeps isLocalCall=false for the system BO")]
        public void CreateBusinessObject_System_WithIsLocalCallFalse_PreservesFlag()
        {
            var obj = Factory.CreateBusinessObject(TestSessionFactory.CreateAccessToken(_fx), SysProgIds.System, isLocalCall: false);

            var bo = Assert.IsType<SystemBusinessObject>(obj);
            Assert.False(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("CreateBusinessObject returns a FormBusinessObject and keeps the ProgId")]
        public void CreateBusinessObject_Form_ReturnsFormBusinessObject()
        {
            var token = TestSessionFactory.CreateAccessToken(_fx);

            var obj = Factory.CreateBusinessObject(token, "prog01", isLocalCall: true);

            var bo = Assert.IsType<FormBusinessObject>(obj);
            Assert.Equal(token, bo.AccessToken);
            Assert.Equal("prog01", bo.ProgId);
            Assert.True(bo.IsLocalCall);
        }

        [Fact]
        [DisplayName("CreateBusinessObject keeps isLocalCall=false for a form BO")]
        public void CreateBusinessObject_Form_WithIsLocalCallFalse_PreservesFlag()
        {
            var obj = Factory.CreateBusinessObject(TestSessionFactory.CreateAccessToken(_fx), "prog01", isLocalCall: false);

            var bo = Assert.IsType<FormBusinessObject>(obj);
            Assert.False(bo.IsLocalCall);
        }
    }
}
