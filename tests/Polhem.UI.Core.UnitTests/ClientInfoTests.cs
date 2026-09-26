using System.ComponentModel;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Smoke tests for the default state of <see cref="ClientInfo"/>.
    /// <para>
    /// Most tests only read default getters, but <c>ClientSettings_NoFile_ReturnsNonNull</c> triggers the lazy getter
    /// of <see cref="ClientInfo.ClientSettings"/>, which creates and **caches** an empty <c>ClientSettings</c> in a static
    /// field when there is no file. That is a write to process-wide static state.
    /// So this class joins the other tests that touch <see cref="ClientInfo.ClientSettings"/> in
    /// <c>[Collection("ClientInfoState")]</c> to run serially. Otherwise, on a 2-core CI runner, it races with
    /// <c>EndpointStorageTests</c> (the empty instance overwrites the Endpoint just written).
    /// </para>
    /// </summary>
    [Collection("ClientInfoState")]
    public class ClientInfoTests
    {
        [Fact]
        [DisplayName("ClientInfo.EndpointStorage defaults to an EndpointStorage instance")]
        public void EndpointStorage_Default_IsEndpointStorageInstance()
        {
            Assert.NotNull(ClientInfo.EndpointStorage);
            Assert.IsType<EndpointStorage>(ClientInfo.EndpointStorage);
        }

        [Fact]
        [DisplayName("ClientInfo.AccessToken defaults to Guid.Empty before login")]
        public void AccessToken_Default_IsEmpty()
        {
            Assert.Equal(Guid.Empty, ClientInfo.AccessToken);
        }

        [Fact]
        [DisplayName("ClientInfo.ClientSettings returns an empty ClientSettings without throwing when there is no file")]
        public void ClientSettings_NoFile_ReturnsNonNull()
        {
            // The test process has no `{ExeName}.Settings.xml`, so the `ClientSettings` getter
            // falls back to creating a new empty `ClientSettings`.
            var settings = ClientInfo.ClientSettings;
            Assert.NotNull(settings);
        }

        [Fact]
        [DisplayName("ClientInfo.UserInfo defaults to null before login")]
        public void UserInfo_Default_IsNull()
        {
            Assert.Null(ClientInfo.UserInfo);
        }

        [Fact]
        [DisplayName("ClientInfo.AllowGenerateSettings defaults to false")]
        public void AllowGenerateSettings_Default_IsFalse()
        {
            Assert.False(ClientInfo.AllowGenerateSettings);
        }

        [Fact]
        [DisplayName("ClientInfo.UIViewService defaults to null")]
        public void UIViewService_Default_IsNull()
        {
            Assert.Null(ClientInfo.UIViewService);
        }

        [Fact]
        [DisplayName("ClientInfo.Arguments defaults to null")]
        public void Arguments_Default_IsNull()
        {
            Assert.Null(ClientInfo.Arguments);
        }

        [Fact]
        [DisplayName("ClientInfo.ApplyLoginResult throws ArgumentNullException for null")]
        public void ApplyLoginResult_NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ClientInfo.ApplyLoginResult(null!));
        }
    }
}
