using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.Definition.Drafting.UnitTests
{
    /// <summary>
    /// The tool schemas the session declares stay within what a strict-tools provider accepts in one request.
    /// </summary>
    public class DraftToolSchemaTests
    {
        // The Claude API rejects a request whose strict tools have more than 24 optional parameters together:
        // "Schemas contains too many optional parameters (28) ... (limit: 24)", measured on 2026-11-16 when ask_user
        // first declared its question properties as optional.
        private const int MaxOptionalParameters = 24;

        [Fact]
        [DisplayName("All the drafting tools together declare no more optional parameters than strict tools allow")]
        public async Task Tools_OptionalParameters_StayWithinTheStrictToolLimit()
        {
            var client = new ScriptedChatClient().Then(Text("OK."));
            await new FormDraftSession(client, new FakeDraftingHost())
                .StartAsync([new DataContent(new byte[] { 1 }, "application/pdf")]);

            var tools = client.Requests[0].Options!.Tools!.OfType<AIFunctionDeclaration>().ToList();
            var perTool = tools.ToDictionary(tool => tool.Name, tool => CountOptional(tool.JsonSchema), StringComparer.Ordinal);

            Assert.Contains("ask_user", perTool.Keys);
            Assert.Equal(0, perTool["ask_user"]);
            Assert.True(perTool.Values.Sum() <= MaxOptionalParameters,
                $"{perTool.Values.Sum()} optional parameters: " + string.Join(", ", perTool.Select(p => $"{p.Key} {p.Value}")));
        }

        private static int CountOptional(JsonElement schema)
        {
            var count = 0;
            if (schema.ValueKind == JsonValueKind.Object)
            {
                if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
                {
                    var required = schema.TryGetProperty("required", out var list)
                        ? list.EnumerateArray().Select(item => item.GetString()).ToHashSet(StringComparer.Ordinal)
                        : [];
                    count += properties.EnumerateObject().Count(property => !required.Contains(property.Name));
                }
                count += schema.EnumerateObject().Sum(property => CountOptional(property.Value));
            }
            else if (schema.ValueKind == JsonValueKind.Array)
            {
                count += schema.EnumerateArray().Sum(CountOptional);
            }
            return count;
        }
    }
}
