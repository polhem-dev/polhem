
namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Defines standard JSON-RPC error codes used to indicate error conditions during request processing.
    /// </summary>
    /// <remarks>
    /// The HTTP status says where a code was raised, not which code it is. Every code the
    /// <see cref="JsonRpcExecutor"/> raises reaches the caller in a response with HTTP status 200.
    /// The ASP.NET Core transport answers on its own only for a request it refuses before the executor
    /// runs, and picks the status there: <see cref="ParseError"/> and <see cref="InvalidRequest"/> for an
    /// unreadable request (400 or 415) or a rejected API key or <c>Authorization</c> header (401), and
    /// <see cref="InternalError"/> (500) for a failure outside the executor.
    /// </remarks>
    public enum JsonRpcErrorCode
    {
        /// <summary>
        /// Invalid JSON was received by the server, typically due to malformed syntax (-32700).
        /// </summary>
        ParseError = -32700,

        /// <summary>
        /// The JSON request is not a valid request object, possibly due to missing required fields or structural errors (-32600).
        /// </summary>
        InvalidRequest = -32600,

        /// <summary>
        /// The method does not exist or is not available (-32601): the action part of
        /// <c>progId.action</c> names nothing the business object exposes as an action. Sent with a
        /// fixed message.
        /// </summary>
        MethodNotFound = -32601,

        /// <summary>
        /// Invalid method parameters or incorrect format (-32602): a <c>Plain</c> body could not be read
        /// into the type the method takes. Sent with a fixed message.
        /// </summary>
        InvalidParams = -32602,

        /// <summary>
        /// An internal server error occurred that prevented the request from being completed (-32000).
        /// </summary>
        InternalError = -32000,

        /// <summary>
        /// The call needs a signed-in caller and arrived without a usable access token — none, or one
        /// that is unknown, invalid or expired (-32001). Raised via
        /// <see cref="Polhem.Core.Exceptions.AuthenticationRequiredException"/>, which the client
        /// rebuilds from this code. Distinct from <see cref="PermissionDenied"/>, where the caller is
        /// signed in but lacks the right.
        /// </summary>
        Unauthorized = -32001,

        /// <summary>
        /// The session has no company context (EnterCompany was not called or LeaveCompany has cleared it),
        /// but the requested operation requires one (-32002).
        /// </summary>
        CompanyNotEntered = -32002,

        /// <summary>
        /// The caller cannot enter the requested company because the company does not exist
        /// or the user has no permission to access it (-32003).
        /// </summary>
        /// <remarks>
        /// The two cases are intentionally merged into a single error code to prevent
        /// anonymous enumeration of valid company ids via error-code differences.
        /// </remarks>
        CompanyAccessDenied = -32003,

        /// <summary>
        /// An authenticated caller lacks permission for a specific action on a permission
        /// model (-32004) — the layer-1 model+action authorization check. Distinct from <see cref="CompanyAccessDenied"/> (company-level) and
        /// <see cref="Unauthorized"/> (missing/invalid credential); raised via
        /// <see cref="Polhem.Core.Exceptions.ForbiddenException"/>.
        /// </summary>
        PermissionDenied = -32004,

        /// <summary>
        /// The call was refused by the replay-protection gate (-32005): the wire frame is missing,
        /// unreadable, or its timestamp falls outside the accepted window.
        /// </summary>
        /// <remarks>
        /// A distinct code so the caller can tell "retrying will not help" apart from
        /// <see cref="Unauthorized"/>, where a fresh credential would. A client that keeps hitting
        /// this is either out of clock sync with the server or predates the frame requirement.
        /// </remarks>
        ReplayRejected = -32005,

        /// <summary>
        /// A user-facing business message produced by business logic, intended to be
        /// shown to the end user (-32099). Carries the text of a
        /// <see cref="Polhem.Core.Exceptions.UserMessageException"/> verbatim; the BCL exceptions
        /// that <see cref="JsonRpcErrorContract"/> also maps here travel with a fixed, generic message.
        /// </summary>
        /// <remarks>
        /// The value -32099 is deliberately placed at the tail of the server-defined
        /// range (-32000 to -32099) to visually separate generic business messages from
        /// concrete protocol/system errors (-32000) and specific business categories
        /// (-32001 to -32003). Future specific categories may fill the gap in between.
        /// </remarks>
        UserMessage = -32099
    }
}
