namespace AutoArchive.Core.Abstractions;

/// <summary>Thin client for a single Ollama chat completion request expected to return JSON.</summary>
public interface IOllamaClient
{
    /// <summary>Sends the prompt and returns the raw text content of the model's reply. <paramref name="responseJsonSchema"/> is
    /// a JSON Schema document constraining the reply's shape - smaller models otherwise tend to drift into
    /// unrelated JSON shapes (e.g. echoing patterns from the prompt) even with plain "respond with JSON" instructions.</summary>
    Task<string> ChatJsonAsync(string systemPrompt, string userPrompt, string responseJsonSchema, CancellationToken cancellationToken);
}
