using System.ComponentModel;
using Polhem.Business.Providers;
using Polhem.Definition.Identity;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for <see cref="CacheDataSourceProvider"/>.
    /// </summary>
    public class CacheDataSourceProviderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public CacheDataSourceProviderTests(SharedDbFixture fx) { _fx = fx; }

        private CacheDataSourceProvider CreateProvider()
        {
            return new CacheDataSourceProvider(
                _fx.GetRequiredService<IRepositoryFactory>(), _fx.Provider);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetSessionInfo returns null for a token that does not exist")]
        public void GetSessionInfo_UnknownToken_ReturnsNull()
        {
            var provider = CreateProvider();

            var result = provider.GetSessionInfo(Guid.NewGuid());

            Assert.Null(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetSessionInfo returns null for a stored seed that names no user")]
        public void GetSessionInfo_SeedWithoutUserId_ReturnsNull()
        {
            var token = Guid.NewGuid();
            var sessions = _fx.GetRequiredService<IRepositoryFactory>().Create<ISessionRepository>();
            sessions.InsertSession(new SessionUser
            {
                AccessToken = token,
                UserId = string.Empty,
                UserName = "nobody",
                EndTime = DateTime.UtcNow.AddMinutes(10),
            });
            try
            {
                Assert.Null(CreateProvider().GetSessionInfo(token));
            }
            finally
            {
                sessions.DeleteSession(token);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetCompanyInfo returns null for a company ID that does not exist")]
        public void GetCompanyInfo_UnknownCompany_ReturnsNull()
        {
            var provider = CreateProvider();

            var result = provider.GetCompanyInfo("no_such_company");

            Assert.Null(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetCompanyRolePermissions returns null for a company ID that does not exist")]
        public void GetCompanyRolePermissions_UnknownCompany_ReturnsNull()
        {
            var provider = CreateProvider();

            var result = provider.GetCompanyRolePermissions("no_such_company");

            Assert.Null(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetDepartmentTree returns null for a company ID that does not exist")]
        public void GetDepartmentTree_UnknownCompany_ReturnsNull()
        {
            var provider = CreateProvider();

            var result = provider.GetDepartmentTree("no_such_company");

            Assert.Null(result);
        }

        [Fact]
        [DisplayName("The CacheDataSourceProvider constructor throws ArgumentNullException for null")]
        public void Constructor_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CacheDataSourceProvider(null!, _fx.Provider));
        }
    }
}
