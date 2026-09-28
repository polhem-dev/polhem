using System.ComponentModel;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Every public or protected asynchronous member of <c>Polhem.Web.Blazor.Server</c> ends with a
    /// <see cref="CancellationToken"/> parameter.
    /// </summary>
    /// <remarks>
    /// The same rule the client and desktop UI packages enforce. Overrides of <c>ComponentBase</c> members are left
    /// out by <see cref="AsyncSurface"/>: their signatures belong to ASP.NET Core.
    /// </remarks>
    public class BlazorAsyncSurfaceTests
    {
        public static TheoryData<string> AsyncMembers()
        {
            var data = new TheoryData<string>();
            foreach (var method in AsyncSurface.Methods(typeof(FormDataObject).Assembly))
                data.Add(AsyncSurface.Describe(method));
            return data;
        }

        [Theory]
        [MemberData(nameof(AsyncMembers))]
        [DisplayName("Every public async member of the Blazor package takes a trailing CancellationToken")]
        public void AsyncMember_TakesTrailingCancellationToken(string member)
        {
            var method = AsyncSurface.Methods(typeof(FormDataObject).Assembly).Single(m => AsyncSurface.Describe(m) == member);

            Assert.True(AsyncSurface.EndsWithCancellationToken(method), $"{member} does not end with a CancellationToken parameter.");
        }

        [Fact]
        [DisplayName("The Blazor async surface scan finds members, so an empty filter cannot pass vacuously")]
        public void AsyncSurface_FindsMembers()
        {
            Assert.Contains(AsyncSurface.Methods(typeof(FormDataObject).Assembly), m => m.DeclaringType == typeof(FormDataObject));
        }
    }
}
