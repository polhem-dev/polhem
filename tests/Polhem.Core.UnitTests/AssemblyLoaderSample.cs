namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// A public type in this test assembly that the <see cref="AssemblyLoader"/> tests resolve and construct by name.
    /// </summary>
    public sealed class AssemblyLoaderSample
    {
        public AssemblyLoaderSample()
        { }

        public AssemblyLoaderSample(string label, bool flag)
        {
            Label = label;
            Flag = flag;
        }

        public string Label { get; } = string.Empty;

        public bool Flag { get; }
    }
}
