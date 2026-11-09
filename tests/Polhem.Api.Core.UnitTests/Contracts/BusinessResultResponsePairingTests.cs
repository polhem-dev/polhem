using System.ComponentModel;
using Polhem.Api.Core.Conversion;
using Polhem.Business;

namespace Polhem.Api.Core.UnitTests.Contracts
{
    /// <summary>
    /// Guards the outbound naming convention: every public, non-abstract <see cref="BusinessResult"/> subtype in
    /// <c>Polhem.Business</c> named <c>{Action}Result</c> must have a non-abstract <c>{Action}Response</c> class in
    /// <c>Polhem.Api.Core</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this needs a gate.</b> <see cref="ApiOutputConverter"/> finds the response type for a BO result by name
    /// alone: it strips the <c>Result</c> suffix, appends <c>Response</c> and looks for a non-abstract type of that
    /// name in the <c>Polhem.Api.Core</c> assembly. When no such type exists it does not throw; it returns the BO
    /// object unchanged, and the BO type is serialized onto the wire as is. A missing or misspelled response class
    /// therefore only shows up at run time, on the client.
    /// </para>
    /// <para>
    /// <c>BusinessContractPairingTests</c> (BO side), <see cref="ApiContractPairingTests"/> (wire side) and
    /// <c>ActionSurfaceTests</c> check the contract interfaces and the action surface, but none of them checks that
    /// the response class this lookup needs exists. This test closes that gap. The matching rule here mirrors
    /// <c>ApiOutputConverter.ResolveResponseType</c>; change both together.
    /// </para>
    /// </remarks>
    public class BusinessResultResponsePairingTests
    {
        private const string ResultSuffix = "Result";
        private const string ResponseSuffix = "Response";

        /// <summary>
        /// Returns every BO result type that needs a matching API response class.
        /// </summary>
        public static TheoryData<Type> BusinessResultTypes()
        {
            var data = new TheoryData<Type>();
            foreach (var type in typeof(BusinessResult).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                .Where(t => typeof(BusinessResult).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                data.Add(type);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(BusinessResultTypes))]
        [DisplayName("Every BO result type has a non-abstract response class of the matching name in Polhem.Api.Core")]
        public void BusinessResultType_HasMatchingResponseType(Type resultType)
        {
            Assert.True(resultType.Name.EndsWith(ResultSuffix, StringComparison.Ordinal),
                $"{resultType.FullName} does not end with '{ResultSuffix}', so ApiOutputConverter cannot map it " +
                "to a response type and the BO object would be serialized as is.");

            var responseName = resultType.Name[..^ResultSuffix.Length] + ResponseSuffix;

            var found = typeof(ApiOutputConverter).Assembly.GetTypes()
                .Any(t => t.Name == responseName && !t.IsAbstract);

            Assert.True(found,
                $"{resultType.FullName} has no non-abstract response class named {responseName} in Polhem.Api.Core. " +
                "Without it ApiOutputConverter returns the BO object unchanged and it reaches the wire as is.");
        }

        [Fact]
        [DisplayName("The enumeration of BO result types is not empty")]
        public void BusinessResultTypes_IsNotEmpty()
        {
            // Keeps the theory above from passing with zero cases when the reflection filter is wrong.
            Assert.NotEmpty(BusinessResultTypes());
        }
    }
}
