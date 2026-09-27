using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.Fakes
{
    /// <summary>
    /// An <see cref="IAuditRuleService"/> for which no company has any per-form audit rule, so every audit axis
    /// inherits the deployment default.
    /// </summary>
    /// <remarks>
    /// The real service reads <c>st_audit_rule</c> from the company database, which it locates through
    /// <c>st_company</c> in <c>common</c> (always SQL Server in the fixture). Tests gated on another provider use this
    /// stub so that a company-bound session does not add a hidden SQL Server dependency.
    /// </remarks>
    internal sealed class NoAuditRuleService : IAuditRuleService
    {
        public CompanyAuditRules? Get(string companyId) => null;

        public void Remove(string companyId) { }
    }
}
