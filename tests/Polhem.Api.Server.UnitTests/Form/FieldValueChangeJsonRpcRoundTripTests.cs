using System.ComponentModel;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Server.UnitTests.Dispatch;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Api.Server.UnitTests.Form
{
    /// <summary>
    /// <c>Employee.FieldValueChange</c> through the JSON-RPC dispatcher: the action resolves to
    /// <c>FormBusinessObject.FieldValueChange</c>, the request converts to its arguments and the result to
    /// <see cref="FieldValueChangeResponse"/>. What the business object resolves against a database is covered by
    /// <c>FormBusinessObjectFieldValueChangeTests</c>.
    /// </summary>
    public class FieldValueChangeJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public FieldValueChangeJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.FieldValueChange on a field not marked for callbacks dispatches and returns an empty FieldValueChangeResponse")]
        public async Task FieldValueChange_ThroughJsonRpc_ReturnsResponse()
        {
            var response = await Dispatch(new FieldValueChangeRequest
            {
                TableName = "Employee",
                FieldName = "dept_rowid",
                Value = Guid.NewGuid().ToString(),
            });

            Assert.Null(response.Error);
            var result = Assert.IsType<FieldValueChangeResponse>(response.Result!.Value);
            Assert.Null(result.Values);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        [DisplayName("Employee.FieldValueChange on a field the form does not have answers with an error")]
        public async Task FieldValueChange_UnknownField_ReturnsError()
        {
            var response = await Dispatch(new FieldValueChangeRequest { TableName = "Employee", FieldName = "no_such_field" });

            Assert.NotNull(response.Error);
        }

        private Task<TestRpcResponse> Dispatch(FieldValueChangeRequest value)
        {
            var dispatcher = new TestDispatcher(_fx.Provider)
            {
                AccessToken = TestSessionFactory.CreateAccessToken(_fx),
                IsLocalCall = true,
            };
            return dispatcher.ExecuteAsync(new TestRpcRequest
            {
                Method = $"Employee.{FormActions.FieldValueChange}",
                Params = new TestPayload { Value = value },
                Id = Guid.NewGuid().ToString(),
            });
        }
    }
}
