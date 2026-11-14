using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;

namespace Polhem.AI.Claude.UnitTests
{
    /// <summary>
    /// An HTTP handler that answers each request with the next queued response and records what was sent. Nothing
    /// leaves the process.
    /// </summary>
    internal sealed class FakeAnthropicHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<RecordedRequest> Requests { get; } = [];

        public FakeAnthropicHandler ThenStream(params string[] events)
        {
            var body = string.Concat(events);
            _responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
            });
            return this;
        }

        public FakeAnthropicHandler ThenError(HttpStatusCode status, string type, string message)
        {
            var body = JsonSerializer.Serialize(new { type = "error", error = new { type, message } });
            _responses.Enqueue(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
            return this;
        }

        public AnthropicClient CreateClient() => new()
        {
            HttpClient = new HttpClient(this),
            BaseUrl = "https://api.anthropic.test",
            ApiKey = "test-key",
            MaxRetries = 0,
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            var betas = request.Headers.TryGetValues("anthropic-beta", out var values)
                ? values.SelectMany(value => value.Split(',')).Select(value => value.Trim()).ToList()
                : [];
            Requests.Add(new RecordedRequest(JsonDocument.Parse(body).RootElement.Clone(), betas));
            if (_responses.Count == 0)
                throw new InvalidOperationException("No response is queued.");
            return _responses.Dequeue()();
        }
    }

    internal sealed record RecordedRequest(JsonElement Body, IReadOnlyList<string> Betas);
}
