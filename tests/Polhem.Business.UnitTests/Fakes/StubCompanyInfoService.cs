using Polhem.Definition.Identity;

namespace Polhem.Business.UnitTests.Fakes
{
    /// <summary>
    /// An in-memory <see cref="ICompanyInfoService"/> for tests that bind a session to a company.
    /// </summary>
    /// <remarks>
    /// The real service reads through to <c>st_company</c> in the <c>common</c> database, which the fixture always
    /// binds to SQL Server. A test gated on another provider that resolves a company through it would silently need
    /// SQL Server as well, so such tests layer this stub over the fixture's provider instead.
    /// </remarks>
    internal sealed class StubCompanyInfoService : ICompanyInfoService
    {
        private readonly Dictionary<string, CompanyInfo> _companies = new(StringComparer.Ordinal);

        public StubCompanyInfoService(params CompanyInfo[] companies)
        {
            foreach (var company in companies)
            {
                _companies[company.CompanyId] = company;
            }
        }

        public CompanyInfo? Get(string companyId) => _companies.GetValueOrDefault(companyId);

        public void Set(CompanyInfo companyInfo) => _companies[companyInfo.CompanyId] = companyInfo;

        public void Remove(string companyId) => _companies.Remove(companyId);
    }
}
