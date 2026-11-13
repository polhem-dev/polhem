namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// A data-driven test that drives the installed Chrome. It skips where <see cref="BrowserFactAttribute"/> does.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BrowserTheoryAttribute : TheoryAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="BrowserTheoryAttribute"/>.
        /// </summary>
        public BrowserTheoryAttribute() => Skip = BrowserFactAttribute.SkipReason();
    }
}
