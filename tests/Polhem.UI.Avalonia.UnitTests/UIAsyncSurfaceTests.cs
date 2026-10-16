using System.ComponentModel;
using System.Reflection;
using Polhem.Tests.Shared;
using Polhem.UI.Avalonia.Views;
using Polhem.UI.Core;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.Avalonia.UnitTests
{
    /// <summary>
    /// Every public or protected asynchronous member of <c>Polhem.UI.Core</c> and <c>Polhem.UI.Avalonia</c> ends
    /// with a <see cref="CancellationToken"/> parameter.
    /// </summary>
    /// <remarks>
    /// The same rule the client package's surface test enforces, for the same reason: once a package ships, adding
    /// the token breaks callers and overriders, and an optional parameter pins its method as the overload with the
    /// most parameters (RS0027). Both assemblies are scanned whole, so the next async member added to either is
    /// caught. <c>Polhem.UI.Core</c> is scanned from here because this project already references it;
    /// <c>Polhem.UI.ApiBridge</c> is scanned by its own test project.
    /// </remarks>
    public class UIAsyncSurfaceTests
    {
        private static readonly Assembly[] s_assemblies = [typeof(UIServices).Assembly, typeof(FormView).Assembly];

        public static TheoryData<string> AsyncMembers()
        {
            var data = new TheoryData<string>();
            foreach (var method in s_assemblies.SelectMany(AsyncSurface.Methods))
                data.Add(AsyncSurface.Describe(method));
            return data;
        }

        [Theory]
        [MemberData(nameof(AsyncMembers))]
        [DisplayName("Every public async member of the UI.Core and UI.Avalonia packages takes a trailing CancellationToken")]
        public void AsyncMember_TakesTrailingCancellationToken(string member)
        {
            var method = s_assemblies.SelectMany(AsyncSurface.Methods).Single(m => AsyncSurface.Describe(m) == member);

            Assert.True(AsyncSurface.EndsWithCancellationToken(method), $"{member} does not end with a CancellationToken parameter.");
        }

        [Fact]
        [DisplayName("The UI async surface scan finds members in both assemblies, so an empty filter cannot pass vacuously")]
        public void AsyncSurface_FindsMembersInBothAssemblies()
        {
            Assert.Contains(AsyncSurface.Methods(typeof(UIServices).Assembly), m => m.DeclaringType == typeof(IFormDefinitionSource));
            Assert.Contains(AsyncSurface.Methods(typeof(FormView).Assembly), m => m.DeclaringType == typeof(FormView));
        }
    }
}
