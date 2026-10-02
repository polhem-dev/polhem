using System.Text.Json.Nodes;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Compares two JSON answers as JSON, not as text: the same value can be written with different escaping (the
    /// dispatcher writes <c>+</c> as <c>+</c> where the executor wrote it as is), which every JSON reader decodes
    /// alike.
    /// </summary>
    internal static class ParityAssert
    {
        public static void SameJson(string expected, string actual) =>
            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)),
                $"The answers differ.{Environment.NewLine}Executor:   {expected}{Environment.NewLine}Dispatcher: {actual}");
    }
}
