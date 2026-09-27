using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Coverage tests for the protected properties of <see cref="BusinessObject"/>.
    /// </summary>
    public class BusinessObjectPropertyTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public BusinessObjectPropertyTests(PolhemTestFixture fx) { _fx = fx; }

        /// <summary>
        /// Exposes the protected properties and methods as public so the tests can verify them.
        /// </summary>
        private sealed class ExposedBusinessObject : BusinessObject
        {
            public ExposedBusinessObject(IBusinessObjectContext ctx, Guid accessToken)
                : base(ctx, accessToken, "TestProg") { }

            public IDefineAccess ExposedDefineAccess => DefineAccess;
            public ISessionInfoService ExposedSessionInfoService => SessionInfoService;
            public IBusinessObjectFactory ExposedBoFactory => BoFactory;
            public IServiceProvider ExposedServices => Services;
            public string InvokeResolveDatabaseId(DbScope scope) => ResolveDatabaseId(scope);
        }

        [Fact]
        [DisplayName("The DefineAccess property forwards the IDefineAccess instance from the context")]
        public void DefineAccess_Property_ForwardsContextDefineAccess()
        {
            var ctx = TestBusinessObjectContext.Create(_fx);
            var bo = new ExposedBusinessObject(ctx, Guid.NewGuid());
            Assert.Same(ctx.DefineAccess, bo.ExposedDefineAccess);
        }

        [Fact]
        [DisplayName("The SessionInfoService property forwards the ISessionInfoService instance from the context")]
        public void SessionInfoService_Property_ForwardsContextSessionInfoService()
        {
            var ctx = TestBusinessObjectContext.Create(_fx);
            var bo = new ExposedBusinessObject(ctx, Guid.NewGuid());
            Assert.Same(ctx.SessionInfoService, bo.ExposedSessionInfoService);
        }

        [Fact]
        [DisplayName("The BoFactory property forwards the IBusinessObjectFactory instance from the context")]
        public void BoFactory_Property_ForwardsContextBoFactory()
        {
            var ctx = TestBusinessObjectContext.Create(_fx);
            var bo = new ExposedBusinessObject(ctx, Guid.NewGuid());
            Assert.Same(ctx.BoFactory, bo.ExposedBoFactory);
        }

        [Fact]
        [DisplayName("The Services property forwards the IServiceProvider instance from the context")]
        public void Services_Property_ForwardsContextServices()
        {
            var ctx = TestBusinessObjectContext.Create(_fx);
            var bo = new ExposedBusinessObject(ctx, Guid.NewGuid());
            Assert.Same(ctx.Services, bo.ExposedServices);
        }

        [Fact]
        [DisplayName("ResolveDatabaseId(Common) returns a non-empty databaseId string")]
        public void ResolveDatabaseId_CommonScope_ReturnsNonEmptyDatabaseId()
        {
            var bo = new ExposedBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid());
            var databaseId = bo.InvokeResolveDatabaseId(DbScope.Common);
            Assert.NotEmpty(databaseId);
        }
    }
}
