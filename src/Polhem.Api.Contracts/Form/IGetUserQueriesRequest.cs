namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the GetUserQueries request: lists the user queries the caller can apply on the form.
    /// </summary>
    /// <remarks>
    /// The form is identified by the <c>ProgId</c> embedded in the JSON-RPC method name, so the request carries no properties.
    /// </remarks>
    public interface IGetUserQueriesRequest
    {    }
}
