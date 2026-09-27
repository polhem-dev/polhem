using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// Wire-level round-trip serialization tests of EnterCompanyRequest / EnterCompanyResponse through
    /// <see cref="MessagePackCodec"/>. The focus: the nested CompanyInfo object restores its fields correctly under
    /// the framework's composite resolver.
    /// </summary>
    public class EnterCompanyMessagePackTests
    {
        [Fact]
        [DisplayName("EnterCompanyRequest with a CompanyId round-trips intact")]
        public void EnterCompanyRequest_RoundTrip_PreservesCompanyId()
        {
            var request = new EnterCompanyRequest { CompanyId = "C001" };

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<EnterCompanyRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal("C001", restored!.CompanyId);
        }

        [Fact]
        [DisplayName("EnterCompanyRequest with the default CompanyId round-trips as an empty string")]
        public void EnterCompanyRequest_DefaultValue_RoundTrip()
        {
            var request = new EnterCompanyRequest();

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<EnterCompanyRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(string.Empty, restored!.CompanyId);
        }

        [Fact]
        [DisplayName("EnterCompanyResponse.Company round-trips CompanyId, CompanyName and CompanyDatabaseId of CompanyInfo")]
        public void EnterCompanyResponse_RoundTrip_PreservesCompanyInfo()
        {
            var response = new EnterCompanyResponse
            {
                Company = new CompanyInfo
                {
                    CompanyId = "C001",
                    CompanyName = "Acme",
                    CompanyDatabaseId = "biz_shared_01"
                }
            };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<EnterCompanyResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Equal("C001", restored!.Company.CompanyId);
            Assert.Equal("Acme", restored.Company.CompanyName);
            Assert.Equal("biz_shared_01", restored.Company.CompanyDatabaseId);
        }

        [Fact]
        [DisplayName("EnterCompanyResponse.Capabilities round-trips the action mask of every model")]
        public void EnterCompanyResponse_RoundTrip_PreservesCapabilities()
        {
            var response = new EnterCompanyResponse
            {
                Company = new CompanyInfo { CompanyId = "C001" },
                Capabilities = new Dictionary<string, PermissionActions>
                {
                    ["PurchaseOrder"] = PermissionActions.Read | PermissionActions.Update,
                    ["Cost"] = PermissionActions.Read,
                }
            };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<EnterCompanyResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(PermissionActions.Read | PermissionActions.Update, restored!.Capabilities["PurchaseOrder"]);
            Assert.Equal(PermissionActions.Read, restored.Capabilities["Cost"]);
        }

        [Fact]
        [DisplayName("EnterCompanyResponse with default Capabilities round-trips as an empty dictionary")]
        public void EnterCompanyResponse_DefaultCapabilities_RoundTripEmpty()
        {
            var response = new EnterCompanyResponse { Company = new CompanyInfo { CompanyId = "C001" } };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<EnterCompanyResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Empty(restored!.Capabilities);
        }
    }
}
