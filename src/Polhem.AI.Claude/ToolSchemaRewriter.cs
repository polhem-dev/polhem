using System.Text.Json;
using System.Text.Json.Nodes;

namespace Polhem.AI.Claude
{
    /// <summary>
    /// Rewrites a tool's JSON schema into shapes the Messages API accepts for strict tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two limits were measured on 2026-11-14 with <c>claude-opus-5-5</c>. First, the API rejects
    /// <c>{"type": ["string", "null"], "enum": [...]}</c>, the schema of a nullable .NET enum parameter, with "Enum value
    /// 'X' does not match declared type"; the same choice written as <c>anyOf</c> of the enum and <c>null</c> is accepted.
    /// Second, the strict tools of one request may have at most 16 parameters with a union type (a type array or
    /// <c>anyOf</c>), and every nullable parameter counts as one.
    /// </para>
    /// <para>
    /// So an optional property, one its object does not list in <c>required</c>, loses its <c>null</c> branch: leaving
    /// it out already says "no value", and the function's default fills it in. A required nullable property keeps
    /// <c>null</c>, as <c>anyOf</c> when it is an enum. The elements of an array lose their <c>null</c> branch too: a
    /// <c>null</c> element carries nothing. The SDK adapter applies its own rewriting afterwards.
    /// </para>
    /// </remarks>
    internal static class ToolSchemaRewriter
    {
        public static JsonElement Rewrite(JsonElement schema)
        {
            var node = JsonNode.Parse(schema.GetRawText());
            if (node is null || !Visit(node))
                return schema;
            return JsonSerializer.SerializeToElement(node);
        }

        private static bool Visit(JsonNode node)
        {
            var changed = false;
            switch (node)
            {
                case JsonObject obj:
                    foreach (var child in obj.Select(property => property.Value).OfType<JsonNode>().ToList())
                        changed |= Visit(child);
                    changed |= DropNullFromOptionalProperties(obj);
                    if (obj["items"] is JsonObject items)
                        changed |= DropNull(items);
                    changed |= SplitNullableEnum(obj);
                    break;
                case JsonArray array:
                    foreach (var child in array.OfType<JsonNode>().ToList())
                        changed |= Visit(child);
                    break;
            }
            return changed;
        }

        private static bool DropNullFromOptionalProperties(JsonObject obj)
        {
            if (obj["properties"] is not JsonObject properties)
                return false;

            var required = obj["required"] is JsonArray list
                ? list.Select(item => item?.GetValue<string>()).OfType<string>().ToHashSet(StringComparer.Ordinal)
                : [];
            var changed = false;
            foreach (var (name, value) in properties.ToList())
            {
                if (!required.Contains(name) && value is JsonObject property)
                    changed |= DropNull(property);
            }
            return changed;
        }

        private static bool DropNull(JsonObject property)
        {
            var changed = false;
            if (property["type"] is JsonArray types && types.Any(IsNull))
            {
                var rest = types.Where(type => !IsNull(type)).Select(type => type!.DeepClone()).ToList();
                property["type"] = rest.Count == 1 ? rest[0] : new JsonArray([.. rest]);
                changed = true;
            }
            if (property["enum"] is JsonArray values && values.Any(value => value is null))
            {
                property["enum"] = new JsonArray([.. values.Where(value => value is not null).Select(value => value!.DeepClone())]);
                changed = true;
            }
            if (property["anyOf"] is JsonArray choices && choices.Any(IsNullSchema))
            {
                var rest = choices.Where(choice => !IsNullSchema(choice)).Select(choice => choice!.DeepClone()).ToList();
                property.Remove("anyOf");
                if (rest.Count == 1 && rest[0] is JsonObject only)
                {
                    foreach (var (key, value) in only.ToList())
                        property[key] ??= value?.DeepClone();
                }
                else
                {
                    property["anyOf"] = new JsonArray([.. rest]);
                }
                changed = true;
            }
            return changed;
        }

        private static bool SplitNullableEnum(JsonObject obj)
        {
            if (obj["type"] is not JsonArray types || obj["enum"] is not JsonArray values || !types.Any(IsNull))
                return false;

            var valueTypes = types.Where(type => !IsNull(type)).Select(type => type!.DeepClone()).ToList();
            var choice = new JsonObject
            {
                ["type"] = valueTypes.Count == 1 ? valueTypes[0] : new JsonArray([.. valueTypes]),
                ["enum"] = new JsonArray([.. values.Where(value => value is not null).Select(value => value!.DeepClone())]),
            };

            obj.Remove("type");
            obj.Remove("enum");
            obj["anyOf"] = new JsonArray(choice, new JsonObject { ["type"] = "null" });
            return true;
        }

        private static bool IsNull(JsonNode? type)
            => type is JsonValue value && value.TryGetValue(out string? name) && name == "null";

        private static bool IsNullSchema(JsonNode? choice)
            => choice is JsonObject obj && obj.Count == 1 && IsNull(obj["type"]);
    }
}
