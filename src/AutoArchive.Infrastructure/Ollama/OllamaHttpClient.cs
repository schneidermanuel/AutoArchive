using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Ollama;

/// <summary>Minimal typed client for Ollama's /api/chat endpoint, requesting JSON-schema-constrained output.</summary>
public sealed class OllamaHttpClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaHttpClient> logger) : IOllamaClient
{
    public async Task<string> ChatJsonAsync(
        string systemPrompt, string userPrompt, string responseJsonSchema, CancellationToken cancellationToken)
    {
        var request = new ChatRequest(
            options.Value.Model,
            [new ChatMessage("system", systemPrompt), new ChatMessage("user", userPrompt)],
            Format: JsonDocument.Parse(responseJsonSchema).RootElement,
            Stream: false,
            Options: new ChatRequestOptions(0.1, options.Value.ContextSizeTokens));

        logger.LogInformation(
            "Ollama request. Model: {Model}. System prompt:\n{SystemPrompt}\nUser prompt:\n{UserPrompt}",
            request.Model, systemPrompt, userPrompt);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new OllamaUnavailableException("Could not reach Ollama.", ex);
        }

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogInformation("Ollama response (raw body):\n{RawBody}", rawBody);

        if (!response.IsSuccessStatusCode)
        {
            throw new OllamaUnavailableException($"Ollama returned HTTP {(int)response.StatusCode}: {rawBody}");
        }

        ChatResponse? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<ChatResponse>(rawBody);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new OllamaUnavailableException("Ollama response body was not valid JSON.", ex);
        }

        if (string.IsNullOrEmpty(payload?.Message?.Content))
        {
            throw new OllamaUnavailableException("Ollama response did not include message content.");
        }

        return payload.Message.Content;
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("format")] JsonElement Format,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] ChatRequestOptions Options);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatRequestOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_ctx")] int NumCtx);

    private sealed record ChatResponse([property: JsonPropertyName("message")] ChatMessage? Message);
}
