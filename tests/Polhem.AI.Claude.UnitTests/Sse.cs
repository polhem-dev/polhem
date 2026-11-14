using System.Text.Json;

namespace Polhem.AI.Claude.UnitTests
{
    /// <summary>
    /// Builds the server-sent events of a streamed Messages API response.
    /// </summary>
    internal static class Sse
    {
        public static string Event(object data)
        {
            var json = JsonSerializer.Serialize(data);
            var type = JsonDocument.Parse(json).RootElement.GetProperty("type").GetString();
            return $"event: {type}\ndata: {json}\n\n";
        }

        public static string MessageStart(string model = "claude-opus-5-5") => Event(new
        {
            type = "message_start",
            message = new
            {
                id = "msg_1",
                type = "message",
                role = "assistant",
                model,
                content = Array.Empty<object>(),
                stop_reason = (string?)null,
                stop_sequence = (string?)null,
                usage = new { input_tokens = 10, output_tokens = 0 },
            },
        });

        public static string Text(int index, string text) => string.Concat(
            Event(new { type = "content_block_start", index, content_block = new { type = "text", text = "" } }),
            Event(new { type = "content_block_delta", index, delta = new { type = "text_delta", text } }),
            Stop(index));

        public static string Thinking(int index, string thinking, string signature) => string.Concat(
            Event(new { type = "content_block_start", index, content_block = new { type = "thinking", thinking = "", signature = "" } }),
            Event(new { type = "content_block_delta", index, delta = new { type = "thinking_delta", thinking } }),
            Event(new { type = "content_block_delta", index, delta = new { type = "signature_delta", signature } }),
            Stop(index));

        public static string ToolUse(int index, string id, string name, string inputJson) => string.Concat(
            Event(new
            {
                type = "content_block_start",
                index,
                content_block = new { type = "tool_use", id, name, input = new { } },
            }),
            Event(new { type = "content_block_delta", index, delta = new { type = "input_json_delta", partial_json = inputJson } }),
            Stop(index));

        public static string Fallback(int index, string from, string to) => string.Concat(
            Event(new
            {
                type = "content_block_start",
                index,
                content_block = new
                {
                    type = "fallback",
                    from = new { model = from },
                    to = new { model = to },
                    trigger = new { type = "refusal", category = "cyber" },
                },
            }),
            Stop(index));

        public static string MessageEnd(string stopReason, string? refusalCategory = null) => string.Concat(
            Event(new
            {
                type = "message_delta",
                delta = new
                {
                    stop_reason = stopReason,
                    stop_sequence = (string?)null,
                    stop_details = refusalCategory is null
                        ? null
                        : new { type = "refusal", category = refusalCategory, explanation = (string?)null },
                },
                usage = new { output_tokens = 5 },
            }),
            Event(new { type = "message_stop" }));

        private static string Stop(int index) => Event(new { type = "content_block_stop", index });
    }
}
