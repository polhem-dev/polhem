using System.ComponentModel;

namespace Polhem.Business.UnitTests.Contracts
{
    /// <summary>
    /// Guards the symmetry of the contract axis on the <b>BO side</b>: every <c>BusinessArgs</c> /
    /// <c>BusinessResult</c> subtype under <c>Polhem.Business</c> must implement the matching <c>I*</c> contract interface in <c>Polhem.Api.Contracts</c>
    /// (<c>XxxArgs</c> → <c>IXxxRequest</c>, <c>XxxResult</c> → <c>IXxxResponse</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this needs a gate.</b> Conversion in both directions between API types and BO types is done by
    /// <c>ApiInputConverter.Convert</c> (called by <c>JsonRpcExecutor</c> inbound and by
    /// <c>ApiOutputConverter</c> outbound), and it <b>copies by matching property names through reflection</b>.
    /// A name that does not match is skipped silently: no exception and no warning, so the call looks successful but the field is empty.
    /// </para>
    /// <para>
    /// The contract interface is the only mechanism that makes this copy complete: <c>LoginRequest</c> and <c>LoginArgs</c>
    /// both implement <c>ILoginRequest</c>, so the compiler forces both sides to carry the same members. Without the interface, nothing
    /// guards the property copy on that path.
    /// </para>
    /// <para>
    /// The wire side has long had the matching <c>ApiContractPairingTests</c>; the BO side did not, so
    /// <c>GetDepartmentTreeArgs</c> missed its contract interface without anyone noticing. This test closes that gap.
    /// </para>
    /// </remarks>
    public class BusinessContractPairingTests
    {
        /// <summary>
        /// Returns every BO args / result type that needs a paired contract interface.
        /// </summary>
        public static TheoryData<Type> BusinessDtoTypes()
        {
            var data = new TheoryData<Type>();
            foreach (var type in typeof(BusinessArgs).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                .Where(t => typeof(BusinessArgs).IsAssignableFrom(t) || typeof(BusinessResult).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                data.Add(type);
            }
            return data;
        }

        /// <summary>
        /// Derives the contract interface name from the BO type name. <c>Args</c> maps to <c>Request</c> and <c>Result</c> to
        /// <c>Response</c>, because the contract describes wire messages while the BO uses its own vocabulary.
        /// </summary>
        private static string? ExpectedContractName(Type boType)
        {
            if (typeof(BusinessArgs).IsAssignableFrom(boType) && boType.Name.EndsWith("Args", StringComparison.Ordinal))
                return "I" + boType.Name[..^"Args".Length] + "Request";
            if (typeof(BusinessResult).IsAssignableFrom(boType) && boType.Name.EndsWith("Result", StringComparison.Ordinal))
                return "I" + boType.Name[..^"Result".Length] + "Response";
            return null;
        }

        [Theory]
        [MemberData(nameof(BusinessDtoTypes))]
        [DisplayName("Every BusinessArgs / BusinessResult implements the matching I* contract interface")]
        public void BusinessDto_ImplementsMatchingContractInterface(Type boType)
        {
            var expected = ExpectedContractName(boType);
            Assert.True(expected != null,
                $"{boType.Name} does not follow the XxxArgs / XxxResult naming convention, so the contract interface name cannot be derived.");

            var implemented = boType.GetInterfaces().Any(i => i.Name == expected);
            Assert.True(implemented,
                $"{boType.FullName} should implement {expected}. Property copying between API and BO matches by name, " +
                "so a mismatch silently drops fields, and the contract interface is the only mechanism that stops this at compile time.");
        }

        [Fact]
        [DisplayName("The list of BO args / result types is not empty (guards against a vacuous pass)")]
        public void BusinessDtoTypes_IsNotEmpty()
        {
            // The theory above is driven by a reflection enumeration. If `BusinessArgs` moves to another assembly or a condition such as IsPublic is wrong,
            // it returns zero items, an xUnit theory with zero cases passes, and the whole gate turns permanently green without any sign.
            Assert.NotEmpty(BusinessDtoTypes());
        }
    }
}
