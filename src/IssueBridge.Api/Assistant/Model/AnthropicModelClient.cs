using System.Net.Http.Json;
using System.Text.Json;
using IssueBridge.Api.Assistant.Tools;
using Microsoft.Extensions.Options;

namespace IssueBridge.Api.Assistant.Model;

public class AnthropicModelClient : IAssistantModelClient
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;

    public AnthropicModelClient(HttpClient httpClient, IOptions<AnthropicOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ModelResponse> SendAsync(
        IReadOnlyList<ModelMessage> messages,
        IReadOnlyList<IAssistantTool> tools,
        CancellationToken cancellationToken)
    {
        var request = new AnthropicRequestDto
        {
            Model = _options.Model,
            Tools = tools.Select(t => new AnthropicToolDefinitionDto
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToList(),
            Messages = messages.Select(m => new AnthropicRequestMessageDto
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("messages", request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return ModelResponse.Failed($"Network error calling the Anthropic API: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ModelResponse.Failed("The Anthropic API request timed out.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await TryReadErrorMessageAsync(response, cancellationToken);
            return ModelResponse.Failed(
                $"Anthropic API returned {(int)response.StatusCode} ({response.StatusCode}): {errorMessage}");
        }

        AnthropicResponseDto? dto;
        try
        {
            dto = await response.Content.ReadFromJsonAsync<AnthropicResponseDto>(cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            return ModelResponse.Failed($"Failed to parse the Anthropic API response: {ex.Message}");
        }

        if (dto is null)
        {
            return ModelResponse.Failed("Anthropic API returned an empty response body.");
        }

        return ParseResponse(dto);
    }

    private static async Task<string> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var errorDto = await response.Content.ReadFromJsonAsync<AnthropicErrorResponseDto>(cancellationToken: cancellationToken);
            return errorDto?.Error?.Message ?? "(no error detail)";
        }
        catch
        {
            return "(unreadable error body)";
        }
    }

    internal static ModelResponse ParseResponse(AnthropicResponseDto dto)
    {
        var toolCalls = new List<ModelToolCall>();
        string? text = null;

        foreach (var block in dto.Content)
        {
            if (block.ValueKind != JsonValueKind.Object || !block.TryGetProperty("type", out var typeProp))
            {
                continue;
            }

            switch (typeProp.GetString())
            {
                case "text" when block.TryGetProperty("text", out var textProp):
                    text = (text ?? string.Empty) + textProp.GetString();
                    break;

                case "tool_use":
                    var id = block.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var name = block.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var input = block.TryGetProperty("input", out var inputProp) ? inputProp : default;

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                    {
                        toolCalls.Add(new ModelToolCall { Id = id, Name = name, Arguments = input });
                    }
                    break;
            }
        }

        if (dto.StopReason == "tool_use" && toolCalls.Count > 0)
        {
            return new ModelResponse
            {
                StopReason = ModelStopReason.ToolUse,
                ToolCalls = toolCalls,
                Text = text,
                RawAssistantContent = dto.Content
            };
        }

        if (!string.IsNullOrEmpty(text))
        {
            return new ModelResponse
            {
                StopReason = ModelStopReason.EndTurn,
                Text = text,
                RawAssistantContent = dto.Content
            };
        }

        return ModelResponse.Empty();
    }
}
