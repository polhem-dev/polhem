using System.ComponentModel;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition.Filters;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Verifies that the <see cref="Polhem.Api.Core.JsonRpc.DateTimeWireGuard"/> check that
    /// <see cref="Polhem.Api.Client.Connectors.ApiConnector"/> runs before sending a request is not defeated by the
    /// user time zone conversion (ADR-032 D6).
    /// </summary>
    /// <remarks>
    /// The time zone conversion changes filter values to <see cref="DateTimeKind.Unspecified"/>. If the guard ran
    /// after the conversion, every <see cref="DateTimeKind.Local"/> value would pass once the user is logged in
    /// (with a user time zone). <c>DateTimeWireGuardTests</c>, which tests only the guard itself, cannot see this
    /// ordering.
    /// </remarks>
    public class ApiConnectorDateTimeGuardTests
    {
        private static readonly DateTime s_sample = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        [Theory]
        [InlineData("")]
        [InlineData("Asia/Taipei")]
        [DisplayName("A filter value with Kind=Local throws whether or not a user time zone is set")]
        public async Task ExecuteAsync_FilterWithLocalKind_ThrowsRegardlessOfUserTimeZone(string userTimeZoneId)
        {
            var request = Request(DateTime.SpecifyKind(s_sample, DateTimeKind.Local));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ApiConnectorTestHost.ExecuteAsUserAsync(request, userTimeZoneId));

            Assert.Contains("created_at", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With a user time zone set, a filter value with Kind=Unspecified is sent normally")]
        public async Task ExecuteAsync_FilterWithUnspecifiedKindAndUserTimeZone_Succeeds()
        {
            var result = await ApiConnectorTestHost.ExecuteAsUserAsync(Request(s_sample), "Asia/Taipei");

            Assert.Equal("ok", result);
        }

        private static GetListRequest Request(DateTime value)
            => new GetListRequest { Filter = FilterCondition.Equal("created_at", value) };
    }
}
