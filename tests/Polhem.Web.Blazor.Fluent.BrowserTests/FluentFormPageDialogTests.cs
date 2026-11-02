using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared form page dialog tests on the Fluent UI UI package.
    /// </summary>
    public class FluentFormPageDialogTests : FormPageDialogTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentFormPageDialogTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentFormPageDialogTests(FluentSiteFixture fixture) : base(fixture)
        {
        }

        /// <inheritdoc />
        protected override string Dialog => "fluent-dialog";

        /// <inheritdoc />
        protected override string CommandButton => ".polhem-form-page__toolbar fluent-button";

        /// <inheritdoc />
        protected override string EnabledCommandButton => ".polhem-form-page__toolbar fluent-button:not([disabled])";
    }
}
