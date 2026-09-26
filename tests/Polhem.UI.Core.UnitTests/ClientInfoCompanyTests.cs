using System.ComponentModel;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Identity;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// <see cref="ClientInfo.Company"/> cache tests: <see cref="ClientInfo.ApplyEnterCompanyResult"/> stores
    /// the company from the EnterCompany response and <see cref="ClientInfo.ClearCompanyContext"/> clears it. It mutates
    /// static state, so it runs serially in the <c>ClientInfoState</c> collection and restores the state at the end.
    /// </summary>
    [Collection("ClientInfoState")]
    public class ClientInfoCompanyTests
    {
        [Fact]
        [DisplayName("ApplyEnterCompanyResult caches the company and ClearCompanyContext clears it")]
        public void ApplyEnterCompanyResult_CachesCompany_ClearResets()
        {
            try
            {
                var company = new CompanyInfo { CompanyId = "C001", DefaultCurrency = "USD" };
                ClientInfo.ApplyEnterCompanyResult(new EnterCompanyResponse { Company = company });

                Assert.NotNull(ClientInfo.Company);
                Assert.Equal("C001", ClientInfo.Company!.CompanyId);
                Assert.Equal("USD", ClientInfo.Company!.DefaultCurrency);

                ClientInfo.ClearCompanyContext();
                Assert.Null(ClientInfo.Company);
            }
            finally
            {
                // Never leak a company into other ClientInfoState tests.
                ClientInfo.ClearCompanyContext();
            }
        }
    }
}
