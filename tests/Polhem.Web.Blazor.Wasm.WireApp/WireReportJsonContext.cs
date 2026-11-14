using System.Text.Json.Serialization;
using Polhem.Web.Blazor.Wasm.BrowserTests;

namespace Polhem.Web.Blazor.Wasm.WireApp
{
    /// <summary>
    /// Writes the report without reflection, so trimming cannot remove a member of it and a failure the test sees is the
    /// framework's, not the report's.
    /// </summary>
    [JsonSerializable(typeof(List<WireStep>))]
    internal sealed partial class WireReportJsonContext : JsonSerializerContext
    {
    }
}
