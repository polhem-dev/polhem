using System.Data;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// Form-level API service connector.
    /// </summary>
    public class FormApiConnector : ApiConnector
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FormApiConnector"/> class using a local connection.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        public FormApiConnector(IServiceProvider services, Guid accessToken, string progId) : base(services, accessToken)
        {
            ProgId = progId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FormApiConnector"/> class using a remote connection.
        /// </summary>
        /// <param name="endpoint">The service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        public FormApiConnector(string endpoint, Guid accessToken, string progId) : base(endpoint, accessToken)
        {
            ProgId = progId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FormApiConnector"/> class using a local connection and
        /// the given session state.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        /// <param name="session">The per-session state. Give each user their own in a host that serves several from one
        /// process; omitting it shares <see cref="ApiSessionContext.Ambient"/>.</param>
        public FormApiConnector(IServiceProvider services, Guid accessToken, string progId, ApiSessionContext session) : base(services, accessToken, session)
        {
            ProgId = progId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FormApiConnector"/> class using a remote connection and
        /// the given session state.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        /// <param name="session">The per-session state. This is the overload a multi-user host wants — the remote path is
        /// the one that encrypts payloads with the session key.</param>
        public FormApiConnector(string endpoint, Guid accessToken, string progId, ApiSessionContext session) : base(endpoint, accessToken, session)
        {
            ProgId = progId;
        }

        #endregion

        /// <summary>
        /// Gets or sets the program identifier (ProgId) used to identify the form-level business object.
        /// </summary>
        public string ProgId { get; private set; }

        /// <summary>
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="action">The action name to execute.</param>
        /// <param name="value">The input parameter for the action.</param>
        /// <param name="format">The payload encoding format for transmission.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// Public, unlike the base method and its counterparts on the system and audit-log connectors: a
        /// form's business object can declare actions of its own, such as an <c>Approve</c> on an order,
        /// and this is how a client calls them. The framework's form actions have typed methods.
        /// </remarks>
        public async Task<T> ExecuteAsync<T>(string action, object value, PayloadFormat format = PayloadFormat.Encrypted,
            CancellationToken cancellationToken = default)
        {
            return await base.ExecuteAsync<T>(ProgId, action, value, format, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously executes a custom method; requires authentication.
        /// </summary>
        /// <param name="request">The custom method identifier and its parameters.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// Takes the request message rather than separate arguments because the call is an open
        /// parameter bag whose shape the application's custom method defines.
        /// </remarks>
        public virtual async Task<ExecFuncResponse> ExecFuncAsync(ExecFuncRequest request, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<ExecFuncResponse>(SystemActions.ExecFunc, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously executes a custom method; allows anonymous access.
        /// </summary>
        /// <param name="request">The custom method identifier and its parameters.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<ExecFuncResponse> ExecFuncAnonymousAsync(ExecFuncRequest request, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<ExecFuncResponse>(SystemActions.ExecFuncAnonymous, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously retrieves list-view rows from the master table of <see cref="ProgId"/>.
        /// </summary>
        /// <param name="selectFields">
        /// The comma-separated field names to retrieve; an empty value falls back to
        /// <see cref="Polhem.Definition.Forms.FormSchema.ListFields"/>, then to all fields.
        /// </param>
        /// <param name="filter">The filter condition tree; <c>null</c> for an unfiltered query.</param>
        /// <param name="sortFields">The sort field collection; <c>null</c> uses the default ordering.</param>
        /// <param name="paging">
        /// The paging options; <c>null</c> is served as the first page of
        /// <see cref="PagingOptions.MaxPageSize"/> rows.
        /// </param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// The server never returns more than <see cref="PagingOptions.MaxPageSize"/> rows in one
        /// response. When the response's paging metadata reports more rows, pass a
        /// <see cref="PagingOptions"/> to page through them, or narrow the result with
        /// <paramref name="filter"/>.
        /// </remarks>
        public virtual async Task<GetListResponse> GetListAsync(
            string selectFields = "",
            FilterNode? filter = null,
            SortFieldCollection? sortFields = null,
            PagingOptions? paging = null,
            CancellationToken cancellationToken = default)
        {
            var request = new GetListRequest
            {
                SelectFields = selectFields,
                Filter = filter,
                SortFields = sortFields,
                Paging = paging,
            };
            return await ExecuteAsync<GetListResponse>(FormActions.GetList, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously retrieves lookup candidate rows for picker windows that
        /// reference <see cref="ProgId"/>. The projection is server-resolved from
        /// <see cref="Polhem.Definition.Forms.FormSchema.LookupFields"/> (falling back to <c>sys_id</c> / <c>sys_name</c>)
        /// and always includes <c>sys_rowid</c>; the caller cannot widen it.
        /// </summary>
        /// <param name="searchText">
        /// The search text matched server-side against the string-typed lookup fields;
        /// an empty value applies no search filter.
        /// </param>
        /// <param name="paging">The paging options; <c>null</c> applies the server-side default page size.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<GetLookupResponse> GetLookupAsync(
            string searchText = "",
            PagingOptions? paging = null,
            CancellationToken cancellationToken = default)
        {
            var request = new GetLookupRequest
            {
                SearchText = searchText,
                Paging = paging,
            };
            return await ExecuteAsync<GetLookupResponse>(FormActions.GetLookup, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously requests a blank <c>DataSet</c> skeleton seeded with
        /// FormSchema defaults and a server-issued <c>sys_rowid</c>; step 1 of
        /// the new-and-save flow.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<GetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
        {
            var request = new GetNewDataRequest();
            return await ExecuteAsync<GetNewDataResponse>(FormActions.GetNewData, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously loads the master row (and its details) by
        /// <paramref name="rowId"/>; step 1 of the load-and-save flow.
        /// </summary>
        /// <param name="rowId">The master row identifier (<c>sys_rowid</c>).</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<GetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            var request = new GetDataRequest { RowId = rowId };
            return await ExecuteAsync<GetDataResponse>(FormActions.GetData, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously persists a <c>DataSet</c> by dispatching
        /// INSERT / UPDATE / DELETE based on each row's <c>RowState</c>; step 2
        /// of both the new-and-save and load-and-save flows.
        /// </summary>
        /// <param name="dataSet">The DataSet to persist.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<SaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dataSet);
            var request = new SaveRequest { DataSet = dataSet };
            return await ExecuteAsync<SaveResponse>(FormActions.Save, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously deletes a single master row directly by
        /// <paramref name="rowId"/> without first loading the full
        /// <c>DataSet</c>.
        /// </summary>
        /// <param name="rowId">The master row identifier (<c>sys_rowid</c>).</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<DeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            var request = new DeleteRequest { RowId = rowId };
            return await ExecuteAsync<DeleteResponse>(FormActions.Delete, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

    }
}
