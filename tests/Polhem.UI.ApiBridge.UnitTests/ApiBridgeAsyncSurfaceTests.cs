using System.ComponentModel;
using System.Reflection;
using Polhem.Tests.Shared;

namespace Polhem.UI.ApiBridge.UnitTests
{
    /// <summary>
    /// Every public or protected asynchronous member of <c>Polhem.UI.ApiBridge</c> ends with a
    /// <see cref="CancellationToken"/> parameter, the rule the UI and client packages' surface tests enforce.
    /// </summary>
    public class ApiBridgeAsyncSurfaceTests
    {
        private static readonly Assembly s_assembly = typeof(ClientInfo).Assembly;

        public static TheoryData<string> AsyncMembers()
        {
            var data = new TheoryData<string>();
            foreach (var method in AsyncSurface.Methods(s_assembly))
                data.Add(AsyncSurface.Describe(method));
            return data;
        }

        [Theory]
        [MemberData(nameof(AsyncMembers))]
        [DisplayName("Every public async member of the UI.ApiBridge package takes a trailing CancellationToken")]
        public void AsyncMember_TakesTrailingCancellationToken(string member)
        {
            var method = AsyncSurface.Methods(s_assembly).Single(m => AsyncSurface.Describe(m) == member);

            Assert.True(AsyncSurface.EndsWithCancellationToken(method), $"{member} does not end with a CancellationToken parameter.");
        }

        [Fact]
        [DisplayName("The UI.ApiBridge async surface scan finds members, so an empty filter cannot pass vacuously")]
        public void AsyncSurface_FindsMembers()
        {
            Assert.Contains(AsyncSurface.Methods(s_assembly), m => m.DeclaringType == typeof(ClientInfo));
        }
    }
}
