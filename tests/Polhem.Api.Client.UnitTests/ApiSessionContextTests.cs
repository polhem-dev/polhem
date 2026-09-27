using System.ComponentModel;
using Polhem.Api.Client.Connectors;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Verifies that per-session state is no longer a process-wide static.
    /// </summary>
    /// <remarks>
    /// The defect was "two sessions share one transport key", so the point of the tests is not that a property
    /// can be stored, but that **two sessions cannot see each other**.
    /// </remarks>
    [Collection(ApiClientInfoStateCollection.Name)]
    public class ApiSessionContextTests
    {
        [Fact]
        [DisplayName("Two sessions do not overwrite each other's transport key")]
        public void TwoSessions_DoNotOverwriteEachOthersEncryptionKey()
        {
            var a = new ApiSessionContext { ApiEncryptionKey = [1, 2, 3] };
            var b = new ApiSessionContext { ApiEncryptionKey = [9, 9, 9] };

            Assert.Equal([1, 2, 3], a.ApiEncryptionKey);
            Assert.Equal([9, 9, 9], b.ApiEncryptionKey);
        }

        [Fact]
        [DisplayName("Two sessions do not overwrite each other's user time zone")]
        public void TwoSessions_DoNotOverwriteEachOthersTimeZone()
        {
            var a = new ApiSessionContext { UserTimeZoneId = "Asia/Taipei" };
            var b = new ApiSessionContext { UserTimeZoneId = "Europe/Berlin" };

            Assert.Equal("Asia/Taipei", a.UserTimeZoneId);
            Assert.Equal("Europe/Berlin", b.UserTimeZoneId);
        }

        [Fact]
        [DisplayName("A connector constructed with a session holds that session, not Ambient")]
        public void Connector_WithSession_UsesThatSession()
        {
            var session = new ApiSessionContext { UserTimeZoneId = "Asia/Taipei" };
            var connector = new SystemApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), session);

            Assert.Same(session, connector.Session);
            Assert.NotSame(ApiSessionContext.Ambient, connector.Session);
        }

        [Fact]
        [DisplayName("A connector constructed without a session falls back to Ambient (existing single-user hosts are unchanged)")]
        public void Connector_WithoutSession_FallsBackToAmbient()
        {
            var connector = new SystemApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid());

            Assert.Same(ApiSessionContext.Ambient, connector.Session);
        }

        [Fact]
        [DisplayName("Constructing a connector with a null session throws instead of silently falling back to Ambient")]
        public void Connector_NullSession_Throws()
        {
            // Silently falling back to Ambient would turn a configuration error in a multi-user host into
            // "looks like it works, but actually shares the key".
            Assert.Throws<ArgumentNullException>(
                () => new SystemApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), null!));
        }
    }
}
