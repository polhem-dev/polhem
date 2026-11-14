using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Polhem.AI.Claude
{
    /// <summary>
    /// A tool declaration the SDK adapter sends with <c>strict: true</c>, so the API checks every tool call's input
    /// against the tool's schema before it reaches the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The adapter reads <c>Strict</c> from <see cref="AITool.AdditionalProperties"/>; the wrapped declaration is left
    /// as it is.
    /// </para>
    /// <para>
    /// <c>eager_input_streaming</c> is deliberately not set. It streams a tool's input as it is generated, but turns the
    /// API's input validation off, and the callers here read whole responses with small tool inputs: nothing would be
    /// gained, and the guarantee strict gives would be lost.
    /// </para>
    /// </remarks>
    internal sealed class StrictToolDeclaration : AIFunctionDeclaration
    {
        private readonly AIFunctionDeclaration _inner;
        private readonly Dictionary<string, object?> _additionalProperties;

        public StrictToolDeclaration(AIFunctionDeclaration inner)
        {
            _inner = inner;
            _additionalProperties = new Dictionary<string, object?>(inner.AdditionalProperties, StringComparer.Ordinal)
            {
                [nameof(Anthropic.Models.Beta.Messages.BetaTool.Strict)] = true,
            };
        }

        public override string Name => _inner.Name;

        public override string Description => _inner.Description;

        public override JsonElement JsonSchema => _inner.JsonSchema;

        public override JsonElement? ReturnJsonSchema => _inner.ReturnJsonSchema;

        public override IReadOnlyDictionary<string, object?> AdditionalProperties => _additionalProperties;

        public override object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : _inner.GetService(serviceType, serviceKey);
    }
}
