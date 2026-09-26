using System.ComponentModel;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Every public class under `Polhem.Api.Core.Messages` must be a wire message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The TypeScript contract generator publishes **every public class in that namespace** as a wire type
    /// (<c>WireContractGenerator.Generate</c> selects by namespace prefix). So putting a class in this namespace
    /// means publishing it as a shape clients can instantiate. That is a decision nobody declares, yet it is real.
    /// </para>
    /// <para>
    /// There was no guard for this before, and the cost was concrete: <c>ApiCallContext</c> (the input to
    /// authorization, carrying the <c>IsLocalCall</c> flag that several second lines of defense rely on) never goes on
    /// the wire, yet it was published into <c>messages.d.ts</c>. It also dragged <c>PayloadFormat</c> into the TS
    /// contract as a string union (<c>'Plain' | 'Encoded' | 'Encrypted'</c>), while **the wire actually carries a
    /// number**, so the same field contradicted itself between contract and wire.
    /// </para>
    /// <para>
    /// The existing <c>RegisteredContracts_AreReachableFromTheClosure</c> cannot catch it by construction: that gate
    /// compares namespace strings, and "being in the Messages namespace" is exactly the premise questioned here, not a
    /// fact that can serve as the criterion.
    /// </para>
    /// </remarks>
    public class MessagesNamespaceGateTests
    {
        private const string MessageNamespace = "Polhem.Api.Core.Messages";

        [Fact]
        [DisplayName("Every public class in the Messages namespace derives from ApiMessageBase")]
        public void PublicTypesInMessagesNamespace_AreAllWireMessages()
        {
            var candidates = typeof(ApiMessageBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
                .Where(t => t.Namespace?.StartsWith(MessageNamespace, StringComparison.Ordinal) == true)
                .ToArray();

            // Guards against a vacuous pass: with a wrong namespace prefix, the check below would always hold on an empty set.
            Assert.True(candidates.Length > 20,
                $"Only {candidates.Length} public classes were found under {MessageNamespace}, so this gate checks nothing.");

            var offenders = candidates
                .Where(t => !typeof(ApiMessageBase).IsAssignableFrom(t))
                .Select(t => t.FullName!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"The following types are in {MessageNamespace} but are not wire messages, so the TypeScript contract generator " +
                $"publishes them as shapes clients can instantiate: {string.Join(", ", offenders)}. " +
                "If a type does not go on the wire, move it to the namespace it really belongs to.");
        }
    }
}
