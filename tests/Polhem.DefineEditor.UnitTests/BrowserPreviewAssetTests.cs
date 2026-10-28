using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.DefineEditor.BrowserPreview;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The scripts and stylesheets of the browser preview page. Without <c>_framework/blazor.web.js</c> the page still
    /// renders once but never connects, so edits stop reaching the browser; the project file's OutputType switch is
    /// what puts it in, and this is the test that notices when that switch stops working.
    /// </summary>
    public sealed class BrowserPreviewAssetTests : IAsyncLifetime
    {
        private static readonly Regex s_assetReference =
            new("(?:href|src)=\"(?<path>[^\"]+\\.(?:css|js))\"", RegexOptions.None, TimeSpan.FromSeconds(1));

        private readonly BrowserPreviewHub _hub = new();
        private readonly BrowserPreviewHost _host;
        private readonly HttpClient _client = new();

        public BrowserPreviewAssetTests()
        {
            _host = new BrowserPreviewHost(_hub, static () => CultureInfo.GetCultureInfo("en"));
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _host.DisposeAsync();
        }

        private async Task<(Uri Url, string Html)> GetPageAsync()
        {
            var documentId = _hub.GetDocumentId("Order.FormLayout.xml");
            var url = await _host.GetDocumentUrlAsync(documentId);
            _hub.Publish(documentId, new BrowserPreviewSnapshot("Order", XmlCodec.Serialize(BrowserPreviewHostTests.CreateLayout()),
                SingleFormMode.Edit, IsCompact: false, ShowFieldNames: true));
            return (url, await _client.GetStringAsync(url));
        }

        [Fact]
        [DisplayName("The page loads the Blazor script, the framework stylesheet and the preview's own assets")]
        public async Task Page_ReferencesTheAssetsItNeeds()
        {
            var (_, html) = await GetPageAsync();

            var paths = s_assetReference.Matches(html).Select(m => m.Groups["path"].Value).ToList();

            Assert.Contains(paths, p => p.StartsWith("_framework/blazor.web", StringComparison.Ordinal));
            Assert.Contains(paths, p => p.StartsWith("_content/Polhem.Web.Blazor.Server/polhem", StringComparison.Ordinal));
            Assert.Contains(paths, p => p.StartsWith("browser-preview", StringComparison.Ordinal) && p.EndsWith(".js", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Every script and stylesheet the page references is served")]
        public async Task Page_EveryReferencedAssetIsServed()
        {
            var (url, html) = await GetPageAsync();
            var paths = s_assetReference.Matches(html).Select(m => m.Groups["path"].Value).ToList();
            Assert.NotEmpty(paths);

            foreach (var path in paths)
            {
                using var response = await _client.GetAsync(new Uri(url, "/" + path));
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} returned {(int)response.StatusCode}.");
            }
        }
    }
}
