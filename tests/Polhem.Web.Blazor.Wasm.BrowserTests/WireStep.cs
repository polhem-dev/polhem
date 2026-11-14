namespace Polhem.Web.Blazor.Wasm.BrowserTests
{
    /// <summary>One step of a <see cref="WireScenario"/> report.</summary>
    /// <remarks>Compiled into <c>Polhem.Web.Blazor.Wasm.WireApp</c> as well, which writes the report as JSON.</remarks>
    public sealed class WireStep
    {
        /// <summary>Gets or sets the step's name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the step completed.</summary>
        public bool Ok { get; set; }

        /// <summary>Gets or sets the exception the step ended with, or <c>null</c>.</summary>
        public string? Error { get; set; }

        /// <summary>Gets the values the step reports, formatted by <see cref="WireSample.Format"/>.</summary>
        public SortedDictionary<string, string> Values { get; init; } = new(StringComparer.Ordinal);
    }
}
