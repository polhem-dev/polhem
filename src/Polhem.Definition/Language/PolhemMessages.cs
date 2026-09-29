namespace Polhem.Definition.Language
{
    /// <summary>
    /// The language keys of the framework's own user-facing messages — sign-in, session, company,
    /// permission and concurrency failures — in the language namespace <see cref="Namespace"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A throw site passes one of these keys with the English text to an exception that implements
    /// <see cref="Polhem.Core.Exceptions.ILocalizableMessage"/>; the JSON-RPC executor then resolves
    /// the key in the session's culture. Translations are looked up in the host's own
    /// language resource of that namespace first, then in the ones shipped inside the framework
    /// (<see cref="FrameworkLanguageService"/>); the English text is the fall-back.
    /// </para>
    /// <para>
    /// Messages meant for developers — a malformed request, a missing argument, a plugin that
    /// cannot be loaded — carry no key and stay English. <c>PolhemTextResourceTests</c> checks that
    /// every key here has a shipped translation and that the translations name no key that is not
    /// here.
    /// </para>
    /// </remarks>
    public static class PolhemMessages
    {
        /// <summary>The language namespace of the framework's messages.</summary>
        public const string Namespace = nameof(PolhemMessages);

        /// <summary>The account is locked after too many failed sign-ins.</summary>
        public const string LoginAccountLocked = Namespace + ".Login.AccountLocked";
        /// <summary>The user id or password is wrong.</summary>
        public const string LoginInvalidCredentials = Namespace + ".Login.InvalidCredentials";

        /// <summary>A call that needs a session carried no access token.</summary>
        public const string SessionAccessTokenRequired = Namespace + ".Session.AccessTokenRequired";
        /// <summary>The access token is missing or not valid.</summary>
        public const string SessionAccessTokenInvalid = Namespace + ".Session.AccessTokenInvalid";
        /// <summary>No session, or no session key, exists for the access token.</summary>
        public const string SessionNotFound = Namespace + ".Session.NotFound";
        /// <summary>The session has expired.</summary>
        public const string SessionExpired = Namespace + ".Session.Expired";
        /// <summary>An executable function requires a signed-in caller; argument 0 is the function id.</summary>
        public const string FuncAuthenticationRequired = Namespace + ".Func.AuthenticationRequired";

        /// <summary>The user may not enter the requested company.</summary>
        public const string CompanyAccessDenied = Namespace + ".Company.AccessDenied";
        /// <summary>The call needs a company and the session has not entered one.</summary>
        public const string CompanyNotEntered = Namespace + ".Company.NotEntered";
        /// <summary>The entered company's information cannot be read.</summary>
        public const string CompanyInfoUnavailable = Namespace + ".Company.InfoUnavailable";

        /// <summary>The action is not granted; arguments are the action and the permission model.</summary>
        public const string PermissionDenied = Namespace + ".Permission.Denied";
        /// <summary>A detail save lacks its master row; arguments are the master table and the model.</summary>
        public const string PermissionMasterRowMissing = Namespace + ".Permission.MasterRowMissing";
        /// <summary>A saved record would leave the caller's scope; arguments are the action and the model.</summary>
        public const string PermissionSavedRecordOutOfScope = Namespace + ".Permission.SavedRecordOutOfScope";
        /// <summary>A record is outside the caller's scope; arguments are the action and the model.</summary>
        public const string PermissionRecordOutOfScope = Namespace + ".Permission.RecordOutOfScope";
        /// <summary>A row changes its key; arguments are the table, the key field and the model.</summary>
        public const string PermissionRowKeyChanged = Namespace + ".Permission.RowKeyChanged";
        /// <summary>A detail row belongs to a record the save does not carry; arguments are the table and the model.</summary>
        public const string PermissionDetailOutOfScope = Namespace + ".Permission.DetailOutOfScope";

        /// <summary>Someone else changed or deleted the record while it was open.</summary>
        public const string SaveConcurrencyConflict = Namespace + ".Save.ConcurrencyConflict";
        /// <summary>A required master field is empty; argument 0 is the field caption.</summary>
        public const string SaveFieldRequired = Namespace + ".Save.FieldRequired";
        /// <summary>A required detail field is empty; arguments are the field caption and the table name.</summary>
        public const string SaveDetailFieldRequired = Namespace + ".Save.DetailFieldRequired";

        /// <summary>The caller may not read the audit log.</summary>
        public const string AuditLogReadDenied = Namespace + ".AuditLog.ReadDenied";
        /// <summary>The caller may not read the database anomaly log.</summary>
        public const string AuditLogAnomalyReadDenied = Namespace + ".AuditLog.AnomalyReadDenied";
        /// <summary>The requested change record does not exist.</summary>
        public const string AuditLogChangeNotFound = Namespace + ".AuditLog.ChangeNotFound";

        /// <summary>The API key id is malformed; arguments are the minimum and maximum length.</summary>
        public const string ApiKeyInvalidId = Namespace + ".ApiKey.InvalidId";
        /// <summary>An API key needs an application name.</summary>
        public const string ApiKeyNameRequired = Namespace + ".ApiKey.NameRequired";
        /// <summary>An API key's expiry time is not in the future.</summary>
        public const string ApiKeyExpiryInPast = Namespace + ".ApiKey.ExpiryInPast";
        /// <summary>An API key with the id already exists; argument 0 is the id.</summary>
        public const string ApiKeyAlreadyExists = Namespace + ".ApiKey.AlreadyExists";
        /// <summary>No API key has the id; argument 0 is the id.</summary>
        public const string ApiKeyNotFound = Namespace + ".ApiKey.NotFound";
        /// <summary>The caller may not manage API keys.</summary>
        public const string ApiKeyManagementDenied = Namespace + ".ApiKey.ManagementDenied";

        /// <summary>A deployment administrator operation needs a user id.</summary>
        public const string DeploymentAdminUserIdRequired = Namespace + ".DeploymentAdmin.UserIdRequired";
        /// <summary>No user has the id; argument 0 is the id.</summary>
        public const string DeploymentAdminUserNotFound = Namespace + ".DeploymentAdmin.UserNotFound";
    }
}
