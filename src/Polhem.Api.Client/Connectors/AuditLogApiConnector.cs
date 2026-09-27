using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Definition;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// Audit-log API service connector (<c>AuditLog</c> axis): read-only queries over the
    /// <c>st_log_*</c> audit tables.
    /// </summary>
    public class AuditLogApiConnector : ApiConnector
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditLogApiConnector"/> class using a local connection.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="accessToken">The access token.</param>
        public AuditLogApiConnector(IServiceProvider services, Guid accessToken) : base(services, accessToken)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditLogApiConnector"/> class using a remote connection.
        /// </summary>
        /// <param name="endpoint">The service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        public AuditLogApiConnector(string endpoint, Guid accessToken) : base(endpoint, accessToken)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditLogApiConnector"/> class using a local connection and
        /// the given session state.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="session">The per-session state. Give each user their own in a host that serves several from one
        /// process; omitting it shares <see cref="ApiSessionContext.Ambient"/>.</param>
        public AuditLogApiConnector(IServiceProvider services, Guid accessToken, ApiSessionContext session) : base(services, accessToken, session)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditLogApiConnector"/> class using a remote connection and
        /// the given session state.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="session">The per-session state. This is the overload a multi-user host wants — the remote path is
        /// the one that encrypts payloads with the session key.</param>
        public AuditLogApiConnector(string endpoint, Guid accessToken, ApiSessionContext session) : base(endpoint, accessToken, session)
        {
        }

        #endregion

        /// <summary>
        /// Asynchronously executes an API method on the <c>AuditLog</c> business object.
        /// </summary>
        /// <param name="action">The action name to execute.</param>
        /// <param name="value">The input parameter for the action.</param>
        /// <param name="format">The payload encoding format for transmission.</param>
        public async Task<T> ExecuteAsync<T>(string action, object value, PayloadFormat format = PayloadFormat.Encrypted)
        {
            return await base.ExecuteAsync<T>(SysProgIds.AuditLog, action, value, format).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets a filtered, paged list of <c>st_log_change</c> event headers across records
        /// (e.g. a form's changes over a period, a user's changes over a period, or one record's history via
        /// <c>ProgId</c> + <c>RowKey</c>). Fetch a single event's before/after detail with
        /// <see cref="GetChangeDetailAsync"/>.
        /// </summary>
        /// <param name="request">The change-log list request (typed filter + optional paging).</param>
        public virtual async Task<AuditLogListResponse> GetChangeLogAsync(GetChangeLogRequest request)
        {
            return await ExecuteAsync<AuditLogListResponse>(AuditLogActions.GetChangeLog, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets one change event's restored field-level before/after detail, by its log
        /// row id (<c>st_log_change.sys_rowid</c>).
        /// </summary>
        /// <param name="sysRowId">The change event's log row id.</param>
        public virtual async Task<GetChangeDetailResponse> GetChangeDetailAsync(Guid sysRowId)
        {
            var request = new GetChangeDetailRequest { SysRowId = sysRowId };
            return await ExecuteAsync<GetChangeDetailResponse>(AuditLogActions.GetChangeDetail, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets a filtered, paged list of <c>st_log_login</c> event headers.
        /// </summary>
        /// <param name="request">The login-log list request (typed filter + optional paging).</param>
        public virtual async Task<AuditLogListResponse> GetLoginLogAsync(GetLoginLogRequest request)
        {
            return await ExecuteAsync<AuditLogListResponse>(AuditLogActions.GetLoginLog, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets a filtered, paged list of <c>st_log_access</c> record-view headers.
        /// </summary>
        /// <param name="request">The access-log list request (typed filter + optional paging).</param>
        public virtual async Task<AuditLogListResponse> GetAccessLogAsync(GetAccessLogRequest request)
        {
            return await ExecuteAsync<AuditLogListResponse>(AuditLogActions.GetAccessLog, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets a filtered, paged list of <c>st_log_anomaly_api</c> API-anomaly headers.
        /// </summary>
        /// <param name="request">The API-anomaly list request (typed filter + optional paging).</param>
        public virtual async Task<AuditLogListResponse> GetApiAnomalyLogAsync(GetApiAnomalyLogRequest request)
        {
            return await ExecuteAsync<AuditLogListResponse>(AuditLogActions.GetApiAnomalyLog, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets a filtered, paged list of <c>st_log_anomaly_db</c> DB-anomaly headers.
        /// </summary>
        /// <param name="request">The DB-anomaly list request (typed filter + optional paging).</param>
        public virtual async Task<AuditLogListResponse> GetDbAnomalyLogAsync(GetDbAnomalyLogRequest request)
        {
            return await ExecuteAsync<AuditLogListResponse>(AuditLogActions.GetDbAnomalyLog, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets API-anomaly counts grouped by anomaly kind (monitoring summary).
        /// </summary>
        /// <param name="request">The summary request (optional time window).</param>
        public virtual async Task<AuditLogAggregateResponse> GetApiAnomalySummaryAsync(GetApiAnomalySummaryRequest request)
        {
            return await ExecuteAsync<AuditLogAggregateResponse>(AuditLogActions.GetApiAnomalySummary, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets DB-anomaly counts grouped by anomaly kind (monitoring summary).
        /// </summary>
        /// <param name="request">The summary request (optional time window).</param>
        public virtual async Task<AuditLogAggregateResponse> GetDbAnomalySummaryAsync(GetDbAnomalySummaryRequest request)
        {
            return await ExecuteAsync<AuditLogAggregateResponse>(AuditLogActions.GetDbAnomalySummary, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets the top API methods by anomaly count (monitoring hot-spots).
        /// </summary>
        /// <param name="request">The top-N request (optional time window + <c>TopN</c>).</param>
        public virtual async Task<AuditLogAggregateResponse> GetTopApiMethodsAsync(GetTopApiMethodsRequest request)
        {
            return await ExecuteAsync<AuditLogAggregateResponse>(AuditLogActions.GetTopApiMethods, request).ConfigureAwait(false);
        }

    }
}
