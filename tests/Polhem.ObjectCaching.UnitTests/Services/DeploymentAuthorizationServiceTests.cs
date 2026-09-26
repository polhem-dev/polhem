using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.ObjectCaching.Services;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.System;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// Checks the decisions of <see cref="DeploymentAuthorizationService"/>: the flag decides everything, and it holds
    /// without a company context. That is exactly what separates it from the company-level
    /// <c>CompanyAuthorizationService</c>.
    /// </summary>
    public class DeploymentAuthorizationServiceTests
    {
        private static readonly Guid s_token = Guid.NewGuid();

        private static DeploymentAuthorizationService Create(SessionInfo? session, bool isAdmin, bool repositoryThrows = false)
            => new DeploymentAuthorizationService(
                new FakeSessionInfoService(session),
                new FakeRepositoryFactory(new FakeUserRepository(isAdmin, repositoryThrows)));

        private static SessionInfo NewSession(string userId, string? companyId = null)
            => new SessionInfo { AccessToken = s_token, UserId = userId, CompanyId = companyId };

        [Fact]
        [DisplayName("A user whose flag is true is authorized")]
        public void Can_DeploymentAdmin_ReturnsTrue()
        {
            var service = Create(NewSession("001"), isAdmin: true);

            Assert.True(service.Can(s_token, DeploymentAction.ManageApiKey));
        }

        [Fact]
        [DisplayName("A signed-in user whose flag is false is denied")]
        public void Can_AuthenticatedNonAdmin_ReturnsFalse()
        {
            var service = Create(NewSession("001"), isAdmin: false);

            Assert.False(service.Can(s_token, DeploymentAction.ManageApiKey));
        }

        [Fact]
        [DisplayName("Not having entered a company does not affect the decision (deployment permissions are not tied to a company)")]
        public void Can_WithoutCompanyContext_StillAuthorizes()
        {
            var service = Create(NewSession("001", companyId: null), isAdmin: true);

            Assert.True(service.Can(s_token, DeploymentAction.ManageApiKey));
        }

        [Fact]
        [DisplayName("An unknown session is denied")]
        public void Can_UnknownToken_ReturnsFalse()
        {
            var service = Create(session: null, isAdmin: true);

            Assert.False(service.Can(s_token, DeploymentAction.ManageApiKey));
        }

        [Fact]
        [DisplayName("A session without a UserId is denied without querying the database")]
        public void Can_SessionWithoutUserId_ReturnsFalse()
        {
            // `repositoryThrows` is true, so a real query would throw.
            // This proves the path does not query the database.
            var service = Create(NewSession(string.Empty), isAdmin: true, repositoryThrows: true);

            Assert.False(service.Can(s_token, DeploymentAction.ManageApiKey));
        }

        private sealed class FakeSessionInfoService : ISessionInfoService
        {
            private readonly SessionInfo? _session;
            public FakeSessionInfoService(SessionInfo? session) { _session = session; }
            public SessionInfo Get(Guid accessToken) => _session!;
            public void Set(SessionInfo sessionInfo) => throw new NotSupportedException();
            public void Remove(Guid accessToken) => throw new NotSupportedException();
        }

        private sealed class FakeRepositoryFactory : IRepositoryFactory
        {
            private readonly IUserRepository _userRepository;
            public FakeRepositoryFactory(IUserRepository userRepository) { _userRepository = userRepository; }

            public T Create<T>(Guid accessToken = default) where T : class
                => typeof(T) == typeof(IUserRepository)
                    ? (T)_userRepository
                    : throw new NotSupportedException(typeof(T).FullName);

            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => throw new NotSupportedException();
        }

        private sealed class FakeUserRepository : IUserRepository
        {
            private readonly bool _isAdmin;
            private readonly bool _throws;
            public FakeUserRepository(bool isAdmin, bool throws) { _isAdmin = isAdmin; _throws = throws; }
            public Guid GetRowIdBySysId(string userId) => throw new NotSupportedException();
            public bool VerifyPassword(string userId, string password) => throw new NotSupportedException();
            public UserLocale GetLocale(string userId) => throw new NotSupportedException();
            public string? GetName(string userId) => throw new NotSupportedException();
            public bool IsDeploymentAdmin(string userId)
                => _throws ? throw new InvalidOperationException("should not query") : _isAdmin;
            public bool SetDeploymentAdmin(string userId, bool isDeploymentAdmin) => throw new NotSupportedException();
        }
    }
}
