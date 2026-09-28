using System.ComponentModel;
using Polhem.Definition.Identity;

namespace Polhem.Definition.UnitTests.Identity
{
    /// <summary>
    /// Tests of <see cref="SessionCompanyScope"/> and of <see cref="SessionInfo.CompanyScope"/>, the single
    /// reference a session's company-scoped values are published through.
    /// </summary>
    public class SessionCompanyScopeTests
    {
        private static readonly Guid s_userRow = Guid.NewGuid();

        [Fact]
        [DisplayName("SessionCompanyScope copies the roles, so changing the source list afterwards does not change the scope")]
        public void Constructor_RolesSourceChangedLater_ScopeUnchanged()
        {
            var roles = new List<string> { "Buyer" };

            var scope = new SessionCompanyScope("C001", string.Empty, roles, Guid.Empty, Guid.Empty, Guid.Empty);
            roles.Add("Manager");

            Assert.Equal(["Buyer"], scope.Roles);
        }

        [Fact]
        [DisplayName("SessionCompanyScope.Roles cannot be modified through a mutable collection interface")]
        public void Roles_CastToMutableList_RejectsChanges()
        {
            var scope = new SessionCompanyScope("C001", string.Empty, ["Buyer"], Guid.Empty, Guid.Empty, Guid.Empty);

            var list = Assert.IsType<IList<string>>(scope.Roles, exactMatch: false);

            Assert.Throws<NotSupportedException>(() => list.Add("Manager"));
        }

        [Fact]
        [DisplayName("A new SessionInfo has the empty company scope")]
        public void NewSession_HasNoneScope()
        {
            var session = new SessionInfo();

            Assert.Same(SessionCompanyScope.None, session.CompanyScope);
            Assert.Null(session.CompanyId);
            Assert.Empty(session.Roles);
        }

        [Fact]
        [DisplayName("Values given in an object initializer combine into one company scope")]
        public void ObjectInitializer_CombinesValuesIntoScope()
        {
            var session = new SessionInfo { CompanyId = "C001", CustomizeId = "acme", Roles = ["Buyer"], UserRowId = s_userRow };

            var scope = session.CompanyScope;

            Assert.Equal("C001", scope.CompanyId);
            Assert.Equal("acme", scope.CustomizeId);
            Assert.Equal(["Buyer"], scope.Roles);
            Assert.Equal(s_userRow, scope.UserRowId);
        }

        [Fact]
        [DisplayName("A snapshot taken before a company switch keeps the old company's values after the switch")]
        public void CompanyScope_SnapshotBeforeSwitch_IsUnaffectedBySwitch()
        {
            var session = new SessionInfo { CompanyId = "C001", Roles = ["Buyer"] };
            var before = session.CompanyScope;

            session.CompanyScope = new SessionCompanyScope("C002", string.Empty, ["Clerk"], Guid.Empty, Guid.Empty, Guid.Empty);

            Assert.Equal("C001", before.CompanyId);
            Assert.Equal(["Buyer"], before.Roles);
            Assert.Equal("C002", session.CompanyId);
        }

        [Fact]
        [DisplayName("Readers of a session switching between companies never see one company's id with another's roles")]
        public async Task CompanyScope_ConcurrentSwitches_ReadersSeeConsistentPairs()
        {
            var c1 = new SessionCompanyScope("C001", string.Empty, ["R-C001"], Guid.Empty, Guid.Empty, Guid.Empty);
            var c2 = new SessionCompanyScope("C002", string.Empty, ["R-C002"], Guid.Empty, Guid.Empty, Guid.Empty);
            var session = new SessionInfo { CompanyScope = c1 };
            int mismatches = 0;
            using var start = new ManualResetEventSlim();

            var writer = Task.Run(() =>
            {
                start.Wait();
                for (int i = 0; i < 200_000; i++)
                    session.CompanyScope = (i & 1) == 0 ? c2 : c1;
            });
            var reader = Task.Run(() =>
            {
                start.Wait();
                for (int i = 0; i < 200_000; i++)
                {
                    var scope = session.CompanyScope;
                    if (scope.Roles[0] != "R-" + scope.CompanyId)
                        Interlocked.Increment(ref mismatches);
                }
            });

            start.Set();
            await Task.WhenAll(writer, reader);

            Assert.Equal(0, mismatches);
        }
    }
}
