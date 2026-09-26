using System.ComponentModel;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Polhem.Api.AspNetCore.UnitTests
{
    /// <summary>
    /// Tests for error-path branches in <see cref="Controllers.ApiServiceController"/>.
    /// Error paths only (415/400/401), with no dependency on PolhemTestFixture or the backend DI container.
    /// </summary>
    public class ApiServiceControllerTests
    {
        private sealed class TestController : Controllers.ApiServiceController { }

        private static async Task<IActionResult> PostAsync(
            string contentType,
            string body,
            string? apiKey = "valid-api-key",
            string? authorization = null)
        {
            var requestBody = new MemoryStream(Encoding.UTF8.GetBytes(body));
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Api-Key"] = apiKey ?? string.Empty;
            if (authorization != null)
                context.Request.Headers.Authorization = authorization;
            context.Request.Headers.ContentType = contentType;
            context.Request.Body = requestBody;

            var controller = new TestController
            {
                ControllerContext = new ControllerContext { HttpContext = context }
            };

            return await controller.PostAsync(apiKey, authorization);
        }

        [Fact]
        [DisplayName("PostAsync returns 415 for a Content-Type other than application/json")]
        public async Task PostAsync_WrongContentType_Returns415()
        {
            var result = await PostAsync("text/plain", "{}");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, obj.StatusCode);
        }

        [Fact]
        [DisplayName("PostAsync returns 415 when Content-Type is missing")]
        public async Task PostAsync_NullContentType_Returns415()
        {
            var result = await PostAsync("", "{}");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, obj.StatusCode);
        }

        [Fact]
        [DisplayName("PostAsync returns 400 for an empty request body")]
        public async Task PostAsync_EmptyBody_Returns400()
        {
            var result = await PostAsync("application/json", "   ");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        }

        [Fact]
        [DisplayName("PostAsync returns 400 ParseError for invalid JSON")]
        public async Task PostAsync_InvalidJson_Returns400ParseError()
        {
            var result = await PostAsync("application/json", "not-valid-json{{{");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        }

        [Fact]
        [DisplayName("PostAsync returns 400 when the JSON has no method field")]
        public async Task PostAsync_MissingMethod_Returns400()
        {
            var result = await PostAsync("application/json", "{\"id\":\"1\",\"params\":{}}");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        }

        [Fact]
        [DisplayName("PostAsync returns 401 without an API key")]
        public async Task PostAsync_MissingApiKey_Returns401()
        {
            // Uses ExecFunc rather than Ping. Ping deliberately needs no key (a health check must still answer
            // when the database is unavailable), so it is no longer an example of "missing key means 401".
            const string body = "{\"method\":\"System.ExecFunc\",\"id\":\"1\",\"params\":{}}";
            var result = await PostAsync("application/json", body, apiKey: null);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, obj.StatusCode);
        }

        [Fact]
        [DisplayName("PostAsync returns 401 for a method that requires authentication without an Authorization header")]
        public async Task PostAsync_AuthRequiredButNoAuthorization_Returns401()
        {
            const string body = "{\"method\":\"System.ExecFunc\",\"id\":\"1\",\"params\":{}}";
            var result = await PostAsync("application/json", body, apiKey: "valid-api-key");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, obj.StatusCode);
        }
    
        [Theory]
        [InlineData(1_000)]        // Well below the threshold.
        [InlineData(40_000)]       // Past the 30 KB threshold of `EnableBuffering` (it used to spill to a temp file).
        [InlineData(300_000)]
        [DisplayName("A request body larger than the buffering threshold still parses correctly (regression guard for reading the stream directly)")]
        public async Task PostAsync_LargeBody_ParsesCorrectly(int payloadSize)
        {
            // This is the real risk of deserializing straight from the stream instead of reading the whole body
            // into a string first: a failure shows up as a partial read or an encoding problem, and only with a
            // large body.
            var large = await PostAsync("application/json", BuildPingBody(new string('a', payloadSize)));
            var small = await PostAsync("application/json", BuildPingBody("x"));

            // The invariant is that the body size must not change the result. This bare test environment has no
            // backend DI, so both stop at the same downstream failure. The point is that a large body does not
            // turn into a 400 because parsing broke.
            var largeResult = Assert.IsType<ObjectResult>(large);
            var smallResult = Assert.IsType<ObjectResult>(small);

            Assert.NotEqual(StatusCodes.Status400BadRequest, largeResult.StatusCode);
            Assert.Equal(smallResult.StatusCode, largeResult.StatusCode);
        }

        [Fact]
        [DisplayName("Multi-byte characters across a read buffer boundary still parse correctly")]
        public async Task PostAsync_MultiByteCharactersAcrossBufferBoundary_ParsesCorrectly()
        {
            // When reading straight from the stream, a UTF-8 multi-byte character can straddle an internal buffer
            // boundary. A large amount of CJK text makes that happen.
            var payload = string.Concat(Enumerable.Repeat("測試字串", 20_000));
            var multiByte = await PostAsync("application/json", BuildPingBody(payload));
            var ascii = await PostAsync("application/json", BuildPingBody("x"));

            var multiByteResult = Assert.IsType<ObjectResult>(multiByte);
            Assert.NotEqual(StatusCodes.Status400BadRequest, multiByteResult.StatusCode);
            Assert.Equal(Assert.IsType<ObjectResult>(ascii).StatusCode, multiByteResult.StatusCode);
        }

        /// <summary>Builds a valid JSON-RPC request carrying the given string payload.</summary>
        private static string BuildPingBody(string payload)
            => "{\"id\":\"1\",\"method\":\"System.Ping\",\"params\":{\"value\":\"" + payload + "\"}}";
}
}
