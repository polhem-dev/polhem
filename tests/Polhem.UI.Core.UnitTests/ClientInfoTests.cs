using System.ComponentModel;
using System.Reflection;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Smoke tests for the default state of <see cref="ClientInfo"/>. Other classes in the <c>ClientInfoState</c>
    /// collection replace these statics and restore them, so this class joins the collection to read the defaults
    /// only while nobody else holds a replacement.
    /// </summary>
    [Collection("ClientInfoState")]
    public class ClientInfoTests
    {
        [Fact]
        [DisplayName("ClientInfo.EndpointStorage defaults to a FileEndpointStorage under LocalApplicationData named after the entry assembly")]
        public void EndpointStorage_Default_IsFileEndpointStorageForEntryAssembly()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Assembly.GetEntryAssembly()?.GetName().Name ?? "Polhem",
                "endpoint.txt");

            var storage = Assert.IsType<FileEndpointStorage>(ClientInfo.EndpointStorage);

            Assert.Equal(expected, storage.FilePath);
        }

        [Fact]
        [DisplayName("ClientInfo.ApiKeyStorage defaults to the same FileEndpointStorage instance as EndpointStorage")]
        public void ApiKeyStorage_Default_IsSameFileEndpointStorageAsEndpointStorage()
        {
            var storage = Assert.IsType<FileEndpointStorage>(ClientInfo.ApiKeyStorage);

            Assert.Same(ClientInfo.EndpointStorage, storage);
        }

        [Fact]
        [DisplayName("ClientInfo.AccessToken defaults to Guid.Empty before login")]
        public void AccessToken_Default_IsEmpty()
        {
            Assert.Equal(Guid.Empty, ClientInfo.AccessToken);
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
