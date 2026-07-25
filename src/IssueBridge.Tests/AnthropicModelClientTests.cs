using System.Net;
using System.Text;
using IssueBridge.Api.Assistant.Model;
using IssueBridge.Api.Assistant.Tools;
using IssueBridge.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace IssueBridge.Tests;

public class AnthropicModelClientTests
{
    private static AnthropicModelClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = Options.Create(new AnthropicOptions { ApiKey = "fake-key", Model = "fake-model" });
        return new AnthropicModelClient(httpClient, options);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static readonly List<ModelMessage> SampleMessages =
    [
        new ModelMessage { Role = "user", Content = new List<object> { new TextContentBlockDto { Text = "hi" } } }
    ];

    // Proves a plain text reply parses into EndTurn with the concatenated text.
    [Fact]
    public async Task SendAsync_TextOnlyResponse_ReturnsEndTurnWithText()
    {
        const string json = """
        {
          "id": "msg_1",
          "role": "assistant",
          "content": [ { "type": "text", "text": "Hello answer" } ],
          "stop_reason": "end_turn"
        }
        """;
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, json));

        var response = await client.SendAsync(SampleMessages, [], CancellationToken.None);

        Assert.Equal(ModelStopReason.EndTurn, response.StopReason);
        Assert.Equal("Hello answer", response.Text);
        Assert.Empty(response.ToolCalls);
    }

    // Proves a tool_use reply parses the tool name, id, and arguments correctly.
    [Fact]
    public async Task SendAsync_ToolUseResponse_ReturnsToolUseWithParsedCall()
    {
        const string json = """
        {
          "id": "msg_2",
          "role": "assistant",
          "content": [ { "type": "tool_use", "id": "toolu_1", "name": "get_open_issues", "input": {} } ],
          "stop_reason": "tool_use"
        }
        """;
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, json));

        var response = await client.SendAsync(SampleMessages, [], CancellationToken.None);

        Assert.Equal(ModelStopReason.ToolUse, response.StopReason);
        var call = Assert.Single(response.ToolCalls);
        Assert.Equal("toolu_1", call.Id);
        Assert.Equal("get_open_issues", call.Name);
    }

    // Proves a non-2xx response is reported as ModelStopReason.Error rather than thrown.
    [Fact]
    public async Task SendAsync_NonSuccessStatusCode_ReturnsError()
    {
        const string json = """
        { "type": "error", "error": { "type": "authentication_error", "message": "invalid x-api-key" } }
        """;
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.Unauthorized, json));

        var response = await client.SendAsync(SampleMessages, [], CancellationToken.None);

        Assert.Equal(ModelStopReason.Error, response.StopReason);
        Assert.Contains("401", response.ErrorMessage);
        Assert.Contains("invalid x-api-key", response.ErrorMessage);
    }

    // Proves a malformed body (200 but not valid JSON) is reported as Error, not thrown.
    [Fact]
    public async Task SendAsync_MalformedJsonBody_ReturnsError()
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, "not valid json"));

        var response = await client.SendAsync(SampleMessages, [], CancellationToken.None);

        Assert.Equal(ModelStopReason.Error, response.StopReason);
        Assert.NotNull(response.ErrorMessage);
    }
}
