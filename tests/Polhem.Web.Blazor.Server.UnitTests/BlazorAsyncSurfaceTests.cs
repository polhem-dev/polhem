using System.ComponentModel;
using Polhem.Tests.Shared;
using System.Reflection;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Every public or protected asynchronous member of the Blazor packages ends with a
    /// <see cref="CancellationToken"/> parameter.
    /// </summary>
    /// <remarks>
    /// The same rule the client and desktop UI packages enforce. Overrides of <c>ComponentBase</c> members are left
    /// out by <see cref="AsyncSurface"/>: their signatures belong to ASP.NET Core.
    /// </remarks>
    public class BlazorAsyncSurfaceTests
    {
        // The shared layer, the host and the UI packages are one async surface to a caller. `FormDataObject` is in
        // `Polhem.UI.Core`, whose surface UIAsyncSurfaceTests in Polhem.UI.Avalonia.UnitTests scans.
        private static IEnumerable<MethodInfo> Methods()
            => AsyncSurface.Methods(typeof(Polhem.Web.Blazor.Components.FormPageBase).Assembly)
                .Concat(AsyncSurface.Methods(typeof(Polhem.Web.Blazor.Server.DependencyInjection.PolhemBlazorOptions).Assembly))
                .Concat(AsyncSurface.Methods(typeof(Polhem.Web.Blazor.Mud.Controls.GridControl).Assembly))
                .Concat(AsyncSurface.Methods(typeof(Polhem.Web.Blazor.Fluent.Controls.GridControl).Assembly));

        public static TheoryData<string> AsyncMembers()
        {
            var data = new TheoryData<string>();
            foreach (var method in Methods())
                data.Add(AsyncSurface.Describe(method));
            return data;
        }

        [Theory]
        [MemberData(nameof(AsyncMembers))]
        [DisplayName("Every public async member of the Blazor package takes a trailing CancellationToken")]
        public void AsyncMember_TakesTrailingCancellationToken(string member)
        {
            var method = Methods().Single(m => AsyncSurface.Describe(m) == member);

            Assert.True(AsyncSurface.EndsWithCancellationToken(method), $"{member} does not end with a CancellationToken parameter.");
        }

        [Fact]
        [DisplayName("The Blazor async surface scan finds members, so an empty filter cannot pass vacuously")]
        public void AsyncSurface_FindsMembers()
        {
            Assert.Contains(Methods(), m => m.DeclaringType?.Assembly == typeof(Polhem.Web.Blazor.Components.FormPageBase).Assembly);
        }
    }
}
