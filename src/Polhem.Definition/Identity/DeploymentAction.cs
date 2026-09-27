namespace Polhem.Definition.Identity
{
    /// <summary>
    /// An operation on a deployment-level asset — one that belongs to the installation rather than
    /// to any single company.
    /// </summary>
    /// <remarks>
    /// Deliberately a separate axis from <see cref="Settings.PermissionActions"/>, which is company-scoped:
    /// company roles live in each company's own database, so granting a company administrator any
    /// say over installation-wide assets would let one tenant act for all of them.
    /// <para>
    /// The enumeration lists only the actions the framework performs. Carrying the action in the
    /// signature is the point — a later split into finer grants changes only
    /// <see cref="IDeploymentAuthorizationService"/> implementations, not their callers.
    /// </para>
    /// </remarks>
    public enum DeploymentAction
    {
        /// <summary>
        /// No action: the default value of an unset field.
        /// </summary>
        None = 0,

        /// <summary>
        /// Issue, revoke or inspect API keys (<c>st_api_key</c>).
        /// </summary>
        ManageApiKey = 1,

        /// <summary>
        /// Read the database anomaly log (<c>st_log_anomaly_db</c>). It carries no company and records
        /// every tenant's database ids, SQL command templates and provider error messages, so it is an
        /// installation-wide view rather than part of any one company's audit trail.
        /// </summary>
        ReadDbAnomalyLog = 2
    }
}
