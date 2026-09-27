using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client.Connectors;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Every public or protected asynchronous member of <c>Polhem.Api.Client</c> ends with a
    /// <see cref="CancellationToken"/> parameter.
    /// </summary>
    /// <remarks>
    /// Adding the token after the package ships is blocked: an optional parameter pins its method as the overload
    /// with the most parameters (RS0027), and changing an existing signature breaks callers. Checking the whole
    /// assembly, rather than a list of types, is what catches the next connector or accessor someone adds.
    /// </remarks>
    public class ClientAsyncSurfaceTests
    {
        public static TheoryData<string> AsyncMembers()
        {
            var data = new TheoryData<string>();
            foreach (var method in PublicAsyncMethods())
            {
                data.Add($"{method.DeclaringType!.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(AsyncMembers))]
        [DisplayName("Every public async member of the client package takes a trailing CancellationToken")]
        public void AsyncMember_TakesTrailingCancellationToken(string member)
        {
            var method = PublicAsyncMethods().Single(m =>
                $"{m.DeclaringType!.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})" == member);

            var parameters = method.GetParameters();
            Assert.True(parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken),
                $"{member} does not end with a CancellationToken parameter.");
        }

        [Fact]
        [DisplayName("The client async surface scan finds members, so an empty filter cannot pass vacuously")]
        public void PublicAsyncMethods_AreFound()
        {
            Assert.Contains(PublicAsyncMethods(), m => m.DeclaringType == typeof(FormApiConnector));
        }

        private static IEnumerable<MethodInfo> PublicAsyncMethods()
            => typeof(ApiConnector).Assembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(method => (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)
                    && !method.IsSpecialName
                    && IsAwaitable(method.ReturnType));

        private static bool IsAwaitable(Type type)
            => type == typeof(Task) || type == typeof(ValueTask)
                || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>)
                    || type.GetGenericTypeDefinition() == typeof(ValueTask<>)));
    }
}
