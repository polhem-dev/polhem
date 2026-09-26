using System.ComponentModel;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests.Contracts
{
    /// <summary>
    /// Guards the symmetry of the contracts axis: every wire type under <c>Polhem.Api.Core.Messages.*</c>
    /// (subtypes of <see cref="ApiRequest"/> / <see cref="ApiResponse"/>) must implement the <c>I*</c> contract
    /// interface of the same name in <c>Polhem.Api.Contracts</c>.
    /// </summary>
    /// <remarks>
    /// This symmetry used to be kept by hand. The 2026-07-28 health check found <c>GetDepartmentTreeRequest</c>
    /// missing its contract interface, although the previous health check had declared this axis "100% aligned".
    /// It was broken for two months without anyone noticing, because no test guarded it. This test closes that gap.
    /// </remarks>
    public class ApiContractPairingTests
    {
        /// <summary>
        /// Returns every wire type that needs a matching contract interface.
        /// </summary>
        public static TheoryData<Type> WireMessageTypes()
        {
            var data = new TheoryData<Type>();
            foreach (var type in typeof(ApiRequest).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                .Where(t => typeof(ApiRequest).IsAssignableFrom(t) || typeof(ApiResponse).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                data.Add(type);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(WireMessageTypes))]
        [DisplayName("Every wire message type implements the I* contract interface of the same name")]
        public void WireMessageType_ImplementsMatchingContractInterface(Type wireType)
        {
            var expectedName = "I" + wireType.Name;

            var implemented = wireType.GetInterfaces()
                .Any(i => string.Equals(i.Name, expectedName, StringComparison.Ordinal));

            Assert.True(implemented,
                $"{wireType.FullName} does not implement the contract interface {expectedName}. " +
                "Every wire message type needs a matching contract interface (see Polhem.Api.Contracts).");
        }

        [Fact]
        [DisplayName("The enumeration of wire message types is not empty")]
        public void WireMessageTypes_IsNotEmpty()
        {
            // Keeps the theory above from passing with zero cases when the reflection filter is wrong.
            Assert.NotEmpty(WireMessageTypes());
        }
    }
}
