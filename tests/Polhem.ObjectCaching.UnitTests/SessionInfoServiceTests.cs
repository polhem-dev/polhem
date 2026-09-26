using System.ComponentModel;
using Polhem.Definition;
using Polhem.ObjectCaching.Services;
using Polhem.Definition.Identity;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Behavior tests of <see cref="SessionInfoService"/>. Each test builds its own
    /// <see cref="CacheContainerService"/> and does not share the process-wide cache.
    /// </summary>
    public class SessionInfoServiceTests
    {
        private static SessionInfoService NewService()
        {
            var paths = new PathOptions { DefinePath = Path.GetTempPath() };
            var storage = new Polhem.Definition.Storage.FileDefineStorage(paths);
            return new SessionInfoService(new CacheContainerService(storage, paths));
        }

        [Fact]
        [DisplayName("Set, Get and Remove operate on the session cache correctly")]
        public void Set_Get_Remove_Flow_Works()
        {
            var service = NewService();
            var token = Guid.NewGuid();
            var info = new SessionInfo
            {
                AccessToken = token,
                UserId = "svc_user",
                UserName = "Service User"
            };

            service.Set(info);
            var loaded = service.Get(token);
            Assert.NotNull(loaded);
            Assert.Equal(token, loaded.AccessToken);
            Assert.Equal("svc_user", loaded.UserId);

            service.Remove(token);
            Assert.Null(service.Get(token));
        }

        [Fact]
        [DisplayName("Get returns null for a token that does not exist")]
        public void Get_MissingToken_ReturnsNull()
        {
            var service = NewService();
            Assert.Null(service.Get(Guid.NewGuid()));
        }
    }
}
