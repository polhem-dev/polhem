using System.ComponentModel;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Identity;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// <see cref="ClientInfo.Company"/> 快取測試：<see cref="ClientInfo.ApplyEnterCompanyResult"/> 存入
    /// EnterCompany 回應的公司，<see cref="ClientInfo.ClearCompanyContext"/> 清除。修改靜態狀態，故納入
    /// <c>ClientInfoState</c> collection 串行執行，並於結尾還原。
    /// </summary>
    [Collection("ClientInfoState")]
    public class ClientInfoCompanyTests
    {
        [Fact]
        [DisplayName("ApplyEnterCompanyResult 快取公司；ClearCompanyContext 清除")]
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
