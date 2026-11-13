using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared lookup tests on the Fluent UI package, whose dialog stays inline in the form scope.
    /// </summary>
    public class FluentLookupTests : LookupTests, IClassFixture<FluentLookupFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentLookupTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentLookupTests(FluentLookupFixture fixture) : base(fixture, fixture.Source) { }

        /// <inheritdoc />
        protected override string Dialog => "fluent-dialog.polhem-lookup-dialog";

        /// <inheritdoc />
        protected override string LookupButton => ".polhem-button-edit__search";
    }
}
