using System.Net;
using System.Text.Json;

namespace ServiceTemplate.IntegrationTests.Infrastructure;

internal static class ProblemAssertions
{
    /// <summary>Asserts an RFC 9457 problem response with the expected status and stable error code.</summary>
    public static async Task<JsonDocument> ShouldBeProblem(
        this HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.StatusCode.Should().Be(status, body);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().Should().Be((int)status);
        document.RootElement.GetProperty("code").GetString().Should().Be(code);
        return document;
    }
}
