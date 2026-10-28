using System.ComponentModel;
using System.Globalization;
using System.Net;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.DefineEditor.BrowserPreview;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The browser preview's web server: what a browser gets for a preview URL, and what it gets without the token.
    /// </summary>
    public sealed class BrowserPreviewHostTests : IAsyncLifetime
    {
        private readonly BrowserPreviewHub _hub = new();
        private readonly BrowserPreviewHost _host;
        private readonly HttpClient _client = new();

        public BrowserPreviewHostTests()
        {
            _host = new BrowserPreviewHost(_hub, static () => CultureInfo.GetCultureInfo("en"));
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _host.DisposeAsync();
        }

        internal static FormLayout CreateLayout()
        {
            var layout = new FormLayout { LayoutId = "Order", ProgId = "Order", Caption = "Order", ColumnCount = 2 };
            var section = new LayoutSection { Name = "Main", Caption = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "Order No" });
            section.Fields.Add(new LayoutField { FieldName = "remark", Caption = "Remark", ColumnSpan = 2 });
            layout.Sections!.Add(section);
            return layout;
        }

        private async Task<Uri> PublishAsync(string documentKey, BrowserPreviewSnapshot snapshot)
        {
            var documentId = _hub.GetDocumentId(documentKey);
            var url = await _host.GetDocumentUrlAsync(documentId);
            _hub.Publish(documentId, snapshot);
            return url;
        }

        private static BrowserPreviewSnapshot Snapshot(FormLayout layout)
            => new("Order", XmlCodec.Serialize(layout), SingleFormMode.Edit, IsCompact: false, ShowFieldNames: true);

        [Fact]
        [DisplayName("A preview URL renders the published layout with the Blazor Server presenter")]
        public async Task GetDocumentUrlAsync_PublishedLayout_RendersItsFields()
        {
            var url = await PublishAsync("Order.FormLayout.xml", Snapshot(CreateLayout()));

            var html = await _client.GetStringAsync(url);

            Assert.Contains("polhem-form-layout", html, StringComparison.Ordinal);
            Assert.Contains("id=\"preview-sys_id\"", html, StringComparison.Ordinal);
            Assert.Contains("grid-column:span 2", html, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A later snapshot replaces the earlier one for the next request")]
        public async Task Publish_NewSnapshot_ReplacesTheLayout()
        {
            var layout = CreateLayout();
            var url = await PublishAsync("Order.FormLayout.xml", Snapshot(layout));
            layout.Sections![0].Fields!.Add(new LayoutField { FieldName = "freight", Caption = "Freight" });

            _hub.Publish(_hub.GetDocumentId("Order.FormLayout.xml"), Snapshot(layout));
            var html = await _client.GetStringAsync(url);

            Assert.Contains("id=\"preview-freight\"", html, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Phone width renders the compact single-column form")]
        public async Task GetDocumentUrlAsync_CompactSnapshot_RendersCompactForm()
        {
            var url = await PublishAsync("Order.FormLayout.xml", Snapshot(CreateLayout()) with { IsCompact = true });

            var html = await _client.GetStringAsync(url);

            Assert.Contains("polhem-form-layout--compact", html, StringComparison.Ordinal);
            Assert.Contains("browser-preview--compact", html, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A layout that could not be serialized shows the reason instead of a form")]
        public async Task GetDocumentUrlAsync_SnapshotWithError_ShowsTheError()
        {
            var snapshot = new BrowserPreviewSnapshot("Order", string.Empty, SingleFormMode.Edit, false, true, "broken layout");
            var url = await PublishAsync("Order.FormLayout.xml", snapshot);

            var html = await _client.GetStringAsync(url);

            Assert.Contains("broken layout", html, StringComparison.Ordinal);
            Assert.DoesNotContain("polhem-form-layout__grid", html, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A preview path with the wrong token is not found")]
        public async Task Get_WrongToken_ReturnsNotFound()
        {
            var url = await PublishAsync("Order.FormLayout.xml", Snapshot(CreateLayout()));
            var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var forged = new Uri(url, $"/preview/{new string('0', segments[1].Length)}/{segments[2]}");

            using var response = await _client.GetAsync(forged);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [DisplayName("A preview path without a token is not found")]
        public async Task Get_NoToken_ReturnsNotFound()
        {
            var url = await PublishAsync("Order.FormLayout.xml", Snapshot(CreateLayout()));
            var documentId = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[2];

            using var response = await _client.GetAsync(new Uri(url, $"/preview/{documentId}"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [DisplayName("The server listens on the loopback address only")]
        public async Task StartAsync_ListensOnLoopback()
        {
            var address = await _host.StartAsync();

            Assert.Equal("127.0.0.1", address.Host);
        }

        [Fact]
        [DisplayName("The same document key keeps its id, and the id does not reveal the file path")]
        public void GetDocumentId_SameKey_ReturnsSameOpaqueId()
        {
            var first = _hub.GetDocumentId("/projects/app/Define/FormLayout/Order.FormLayout.xml");
            var second = _hub.GetDocumentId("/projects/app/Define/FormLayout/Order.FormLayout.xml");

            Assert.Equal(first, second);
            Assert.DoesNotContain("Order", first, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(first, _hub.GetDocumentId("/projects/app/Define/FormLayout/Customer.FormLayout.xml"));
        }
    }
}
