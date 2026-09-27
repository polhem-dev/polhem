using Polhem.Base;

namespace Polhem.Definition.Identity
{
    /// <summary>
    /// Backend session info that records runtime data for the connection established between a user and the server.
    /// </summary>
    public sealed class SessionInfo : IKeyObject, IUserInfo
    {
        private SessionCompanyScope _companyScope = SessionCompanyScope.None;

        #region IKeyObject Interface

        /// <summary>
        /// Gets the item key value.
        /// </summary>
        public string GetKey()
        {
            return this.AccessToken.ToString();
        }

        #endregion

        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        public Guid AccessToken { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the expiration time (UTC) of the access token after a successful login.
        /// </summary>
        public DateTime ExpiredAt { get; set; }

        /// <summary>
        /// Gets or sets the user account ID.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the company-scoped values of this session as one snapshot.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set by <c>EnterCompany</c> and reset to <see cref="SessionCompanyScope.None"/> by
        /// <c>LeaveCompany</c> / <c>Logout</c>, always as one reference write. The instance is shared by
        /// every concurrent request carrying the access token, so a reader that needs more than one of
        /// the company-scoped values reads this property once and takes them all from the snapshot;
        /// reading <see cref="CompanyId"/> and then <see cref="Roles"/> can straddle a company switch.
        /// </para>
        /// <para>
        /// The individual properties below are views of this snapshot. They can be given in an object
        /// initializer, before the session is shared, but not assigned afterwards.
        /// </para>
        /// </remarks>
        public SessionCompanyScope CompanyScope
        {
            get => Volatile.Read(ref _companyScope);
            set => Volatile.Write(ref _companyScope, value ?? throw new ArgumentNullException(nameof(value)));
        }

        /// <summary>
        /// Gets the ID of the company the user has entered for this session.
        /// </summary>
        /// <remarks>
        /// <c>null</c> means the user has logged in but has not yet entered a company.
        /// Business operations that depend on a company context (FormBO, ReportBO, etc.)
        /// must reject requests with a <c>null</c> value via <c>CompanyNotEntered</c>.
        /// The value is set by <c>EnterCompany</c> and cleared by <c>LeaveCompany</c> /
        /// <c>Logout</c>.
        /// </remarks>
        public string? CompanyId
        {
            get => CompanyScope.CompanyId;
            init => _companyScope = Rebuild(companyId: value);
        }

        /// <summary>
        /// Gets the tenant customization code currently in effect for this session.
        /// </summary>
        /// <remarks>
        /// Empty means the standard (non-customized) deployment — every customization overlay
        /// short-circuits to the base layer. The value is derived from
        /// <see cref="CompanyInfo.CustomizeId"/> by <c>EnterCompany</c> and cleared by
        /// <c>LeaveCompany</c> / <c>Logout</c> (in step with <see cref="CompanyId"/>).
        /// </remarks>
        public string CustomizeId
        {
            get => CompanyScope.CustomizeId;
            init => _companyScope = Rebuild(customizeId: value);
        }

        /// <summary>
        /// Gets or sets the user culture (e.g., zh-TW, en-US). An empty value starts the language
        /// fall-back chain at the system default language.
        /// </summary>
        /// <remarks>
        /// The default is deliberately empty rather than a named culture, for the same reason as
        /// <see cref="TimeZone"/>: hard-coding <c>zh-TW</c> made the language service's fallback
        /// path unreachable for every logged-in call and silently bound the framework to one
        /// region. Login fills this from <c>st_user.culture</c>, falling back to
        /// <see cref="Polhem.Definition.Settings.CommonConfiguration.DefaultLanguage"/>, and returns it to
        /// the client in the login response.
        /// </remarks>
        public string Culture { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user time zone (IANA format recommended, e.g., Asia/Taipei).
        /// An empty value means UTC.
        /// </summary>
        /// <remarks>
        /// The default is deliberately empty rather than a named zone. Every layer that consumes
        /// this value — <see cref="Polhem.Base.FrameworkClock"/>, <c>DateTimeZoneConverter</c> and
        /// <c>PayloadZoneConverter</c> — already treats a blank zone as UTC, and hard-coding a
        /// zone here made that path unreachable while silently binding the framework to a single
        /// region. Login fills this from <c>st_user.time_zone</c>, falling back to
        /// <see cref="Polhem.Definition.Settings.BackendConfiguration.DefaultTimeZone"/>.
        /// </remarks>
        public string TimeZone { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the API encryption key used for bidirectional data encryption between the client and server.
        /// This key is dynamically generated by the server and returned to the client at login.
        /// </summary>
        public byte[] ApiEncryptionKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets the role ids assigned to the user within the current company.
        /// </summary>
        /// <remarks>
        /// Populated by <c>EnterCompany</c> after the user enters a company (each id is an
        /// <c>st_role.sys_id</c>); the layer-1 permission check OR-merges these roles' grants.
        /// Cleared by <c>LeaveCompany</c> / <c>Logout</c> in step with <see cref="CompanyId"/>.
        /// </remarks>
        public IReadOnlyList<string> Roles
        {
            get => CompanyScope.Roles;
            init => _companyScope = Rebuild(roles: value);
        }

        /// <summary>
        /// Gets the current user's row id (<c>st_user.sys_rowid</c>) within the entered company.
        /// </summary>
        /// <remarks>
        /// Resolved and snapshotted by <c>EnterCompany</c> for record-scope <c>Own</c> filtering;
        /// <see cref="System.Guid.Empty"/> until a company is entered. Cleared by
        /// <c>LeaveCompany</c> / <c>Logout</c> in step with <see cref="CompanyId"/>.
        /// </remarks>
        public Guid UserRowId
        {
            get => CompanyScope.UserRowId;
            init => _companyScope = Rebuild(userRowId: value);
        }

        /// <summary>
        /// Gets the current user's linked employee row id (<c>st_employee.sys_rowid</c>) in
        /// the entered company.
        /// </summary>
        /// <remarks>
        /// Resolved and snapshotted by <c>EnterCompany</c> for record-scope <c>Own</c> filtering;
        /// <see cref="System.Guid.Empty"/> when the user has no employee in this company (or no
        /// company entered). Cleared by <c>LeaveCompany</c> / <c>Logout</c>.
        /// </remarks>
        public Guid EmployeeRowId
        {
            get => CompanyScope.EmployeeRowId;
            init => _companyScope = Rebuild(employeeRowId: value);
        }

        /// <summary>
        /// Gets the current user's department row id (<c>st_employee.dept_rowid</c>) in the
        /// entered company.
        /// </summary>
        /// <remarks>
        /// Resolved and snapshotted by <c>EnterCompany</c> for record-scope <c>Dept</c> /
        /// <c>DeptAndSub</c> filtering; <see cref="System.Guid.Empty"/> when the user has no employee
        /// or no department (or no company entered). Cleared by <c>LeaveCompany</c> / <c>Logout</c>.
        /// </remarks>
        public Guid DeptRowId
        {
            get => CompanyScope.DeptRowId;
            init => _companyScope = Rebuild(deptRowId: value);
        }

        /// <summary>
        /// Returns the current scope with the given values replaced; used by the init accessors only.
        /// </summary>
        private SessionCompanyScope Rebuild(
            string? companyId = null, string? customizeId = null, IEnumerable<string>? roles = null,
            Guid? userRowId = null, Guid? employeeRowId = null, Guid? deptRowId = null)
        {
            var current = _companyScope;
            return new SessionCompanyScope(
                companyId ?? current.CompanyId,
                customizeId ?? current.CustomizeId,
                roles ?? current.Roles,
                userRowId ?? current.UserRowId,
                employeeRowId ?? current.EmployeeRowId,
                deptRowId ?? current.DeptRowId);
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{UserId} : {UserName}";
        }
    }
}
