using Avalonia.Controls;

namespace Avalonia.DemoCenter.Modules
{
    /// <summary>
    /// A self-contained demo case: where it sits in the two-level navigation tree
    /// (<see cref="Category"/> theme → <see cref="Title"/> case), a short description, a
    /// factory that builds the live interactive view, and the real source text shown in
    /// the View Source panel.
    /// </summary>
    public interface IDemoModule
    {
        /// <summary>Top-level theme, e.g. "Data Binding" / "Read-only &amp; Required" / "Grid".</summary>
        string Category { get; }

        /// <summary>The case title shown as the navigation leaf, e.g. "Ambient binding".</summary>
        string Title { get; }

        /// <summary>One-line description shown in the case header.</summary>
        string Description { get; }

        /// <summary>Builds the live, interactive view for this scenario.</summary>
        Control BuildView();

        /// <summary>
        /// Returns the module's own source text for the View Source panel. Implementations
        /// read their real <c>.cs</c> from an embedded resource so the displayed source can
        /// never drift from what runs.
        /// </summary>
        string GetSourceText();
    }
}
